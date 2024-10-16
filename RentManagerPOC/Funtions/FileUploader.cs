using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using Azure.Storage.Blobs;
using RentManagerPOC.Models;
using System.Collections.Generic;

namespace RentManagerPOC.Funtions
{
    public static class FileUploader
    {
        private static readonly string BaseUrl = Environment.GetEnvironmentVariable("RentManagerBaseURL") ?? throw new InvalidOperationException("Base URL not configured.");
        private static readonly string ConnectionString = Environment.GetEnvironmentVariable("ClientStorageConnectionString") ?? throw new InvalidOperationException("Storage connection string not configured.");
        private static readonly string ContainerName = Environment.GetEnvironmentVariable("ClientContainerName") ?? throw new InvalidOperationException("Container name not configured.");


        [FunctionName("FileUploader")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "FileUploader/{ReportId}")] HttpRequest req,
            ILogger log,
            int ReportId)
        {

            if (ReportId <= 0)
            {
                log.LogWarning("Invalid ReportId received: {ReportId}", ReportId);
                return new BadRequestObjectResult("Invalid ReportId.");
            }

            log.LogInformation("-------------Process Started.--------------");

            BlobContainerClient blobContainerClient;

            try
            {
                blobContainerClient = await CreateBlobContainerClient(log);
            }
            catch (Exception ex)
            {
                log.LogError($"Failed to create blob container client: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            try
            {
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(BaseUrl);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.Timeout = TimeSpan.FromMinutes(30);

                    // Get authentication token
                    string authToken = await GetAuthToken(client, log);

                    if (string.IsNullOrEmpty(authToken))
                    {
                        log.LogError("Retrying to Generate Auth Token.");
                        authToken = await GetAuthToken(client, log);

                        if (string.IsNullOrEmpty(authToken))
                        {
                            log.LogError("Failed to generate Auth Token.");
                            return new BadRequestObjectResult("Failed to generate Auth Token.");
                        }

                        log.LogError($"Token Generated: {authToken}.");
                    }


                    // Get the CSV file download URL
                    string downloadUrl = await GetCsvDownloadUrl(client, ReportId, authToken, log);

                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        log.LogError($"Failed to retrieve CSV File Download URL for ReportId {ReportId}.");
                        return new BadRequestObjectResult(new { Status = false, Message = $"CSV download URL for ReportId {ReportId} Not Found." });
                    }


                    // Download the CSV file content
                    byte[] fileContent = await DownloadCsvFile(client, downloadUrl, ReportId, log);

                    if (fileContent == null)
                    {
                        log.LogError($"Failed to download the CSV file for ReportId {ReportId}.");
                        return new BadRequestObjectResult(new { Status = false, Message = $"Failed to download the CSV file for ReportId {ReportId}." });
                    }


                    // Upload the CSV file to Azure Blob Storage
                    await UploadToBlobStorage(blobContainerClient, ReportId, fileContent, log);
                    log.LogInformation($"Report Uploaded to Blob Storage: '{ReportId}'");
                }
            }
            catch (Exception ex)
            {
                log.LogError($"File Download Failed for ReportId {ReportId}: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            return new OkObjectResult(new { Status = true, Message = "CSV Files Downloaded Successfully." });
        }


        // ----------------HELPER METHODS-------------------
        public static async Task<string> GetAuthToken(HttpClient client, ILogger log)
        {
            try
            {
                var authModel = new UserAuthorizationModel { username = "Ephraim", password = "Goose3734", locationid = "1" };
                var response = await client.PostAsJsonAsync("authentication/AuthorizeUser/", authModel);

                if (!response.IsSuccessStatusCode)
                {
                    log.LogError($"Failed to retrieve authentication token. Message: {response.ReasonPhrase}");
                    return null;
                }

                return (await response.Content.ReadAsStringAsync()).Trim('"');
            }
            catch (Exception ex)
            {
                log.LogError($"Error getting auth token: {ex.Message}");
                return null;
            }
        }


        private static async Task<string> GetCsvDownloadUrl(HttpClient client, int reportId, string authToken, ILogger log)
        {
            var requestUrl = $"{BaseUrl}/ReportWriterReports/{reportId}/RunReportWriterReport?GetOptions=ReturnCSVUrl";

            try
            {
                client.DefaultRequestHeaders.Add("X-RM12Api-ApiToken", authToken);
                var response = await client.GetAsync(requestUrl);

                if (!response.IsSuccessStatusCode)
                {
                    log.LogError($"Failed to get CSV download URL for ReportId {reportId}. Message: {response.ReasonPhrase}");
                    return null;
                }

                // Get the CSV file download URL from the response
                string downloadUrl = await response.Content.ReadAsStringAsync();
                return downloadUrl.Trim('"');
            }
            catch (Exception ex)
            {
                log.LogError($"Error getting CSV download URL: {ex.Message}");
                return null;
            }
        }


        private static async Task<byte[]> DownloadCsvFile(HttpClient client, string downloadUrl, int reportId, ILogger log)
        {
            try
            {
                var downloadResponse = await client.GetAsync(downloadUrl);

                if (!downloadResponse.IsSuccessStatusCode)
                {
                    log.LogError($"Failed to download CSV file for ReportId {reportId}. Message: {downloadResponse.ReasonPhrase}");
                    return null;
                }

                return await downloadResponse.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                log.LogError($"Error downloading CSV file for ReportId {reportId}: {ex.Message}");
                return null;
            }
        }


        private static readonly Dictionary<int, string> ReportIdToFolderName = new Dictionary<int, string>
        {
            { 137, "Legal in Action" },
            { 62, "Current Vacancy BI Use" },
            { 61, "The No Pay For BI" },
            { 59, "Actionable Report" },
            { 58, "2 Month Bal Report For BI Use" },
            { 66, "Department Rep Report" }
        };


        private static async Task UploadToBlobStorage(BlobContainerClient blobContainerClient, int reportId, byte[] fileContent, ILogger log)
        {
            try
            {
                if (ReportIdToFolderName.TryGetValue(reportId, out string folderName))
                {
                    string blobName = $"{folderName}/{folderName}.csv";
                    BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);

                    using (var stream = new MemoryStream(fileContent))
                    {
                        await blobClient.UploadAsync(stream, overwrite: true);
                    }

                    log.LogInformation($"Successfully uploaded ReportId {reportId} to folder '{folderName}' in Blob Storage.");
                }
                else
                {
                    log.LogWarning($"No folder name mapped for ReportId {reportId}. Uploading to default 'Reports' folder.");

                    string blobName = $"Reports/{reportId}.csv";
                    BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);

                    using (var stream = new MemoryStream(fileContent))
                    {
                        await blobClient.UploadAsync(stream, overwrite: true);
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Error uploading file to Blob Storage for ReportId {reportId}: {ex.Message}");
            }
        }


        private static async Task<BlobContainerClient> CreateBlobContainerClient(ILogger log)
        {
            try
            {
                var blobServiceClient = new BlobServiceClient(ConnectionString);
                var blobContainerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
                await blobContainerClient.CreateIfNotExistsAsync();

                log.LogInformation($"Blob container '{ContainerName}' found or created successfully.");
                return blobContainerClient;
            }
            catch (Exception ex)
            {
                log.LogError($"Error creating or accessing blob container '{ContainerName}': {ex.Message}");
                throw; // Rethrow the exception to allow the caller to handle it
            }
        }
    }
}

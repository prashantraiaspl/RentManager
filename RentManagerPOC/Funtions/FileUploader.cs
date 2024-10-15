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
        private static readonly string BaseUrl = "https://goosepm.api.rentmanager.com";

        // Azure Blob Storage connection string and container name
        private static readonly string connectionString = "DefaultEndpointsProtocol=https;AccountName=aspl1710;AccountKey=VjyEEoO4A+ADkyo4F1S6D6BFL++jxubiK9rbcuSJBt2p46bfjC4j4i5y4cMgB2cswM9TGHWr1Zit+AStKEBrRA==;EndpointSuffix=core.windows.net";
        private static readonly string containerName = "aspl1710";


        [FunctionName("FileUploader")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "FileUploader/{reportId}")] HttpRequest req,
            ILogger _logger,
            int reportId)
        {
            if (reportId <= 0)
            {
                return new BadRequestObjectResult("Invalid ReportId.");
            }

            _logger.LogInformation("Process Started.");

            // Create a BlobContainerClient
            var blobServiceClient = new BlobServiceClient(connectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
            await blobContainerClient.CreateIfNotExistsAsync();

            _logger.LogInformation($"Blob container '{containerName}' Found or Created Successfully.");

            try
            {
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(BaseUrl);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.Timeout = TimeSpan.FromMinutes(30);

                    // Get authentication token
                    string authToken = await GetAuthToken(client, _logger);

                    if (string.IsNullOrEmpty(authToken))
                    {
                        _logger.LogError("Retrying to Generate Auth Token.");

                        authToken = await GetAuthToken(client, _logger);
                    }

                    // Get the CSV file download URL
                    string downloadUrl = await GetCsvDownloadUrl(client, reportId, authToken, _logger);

                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        _logger.LogError($"Failed to retrieve CSV File Download URL for ReportId {reportId}.");
                        return new BadRequestObjectResult(new { Status = false, Message = $"CSV download URL for ReportId {reportId} not found." });
                    }


                    // Download the CSV file content
                    byte[] fileContent = await DownloadCsvFile(client, downloadUrl, reportId, _logger);

                    if (fileContent == null)
                    {
                        _logger.LogError($"Failed to download the CSV file for ReportId {reportId}.");
                        return new BadRequestObjectResult(new { Status = false, Message = $"Failed to download the CSV file for ReportId {reportId}." });
                    }


                    // Upload the CSV file to Azure Blob Storage
                    await UploadToBlobStorage(blobContainerClient, reportId, fileContent, _logger);

                    _logger.LogInformation($"Report uploaded to Blob Storage: {reportId}.csv");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"File Download Failed for ReportId {reportId}: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            return new OkObjectResult(new { Status = true, Message = "CSV Files Downloaded Successfully." });
        }


        // ----------------HELPER METHODS-------------------
        public static async Task<string> GetAuthToken(HttpClient client, ILogger _logger)
        {
            try
            {
                var authModel = new UserAuthorizationModel { username = "Ephraim", password = "Goose3734", locationid = "1" };
                var response = await client.PostAsJsonAsync("authentication/AuthorizeUser/", authModel);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to retrieve authentication token. Message: {response.ReasonPhrase}");
                    return null;
                }

                return (await response.Content.ReadAsStringAsync()).Trim('"');
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting auth token: {ex.Message}");
                return null;
            }
        }

        private static async Task<string> GetCsvDownloadUrl(HttpClient client, int reportId, string authToken, ILogger _logger)
        {
            var requestUrl = $"{BaseUrl}/ReportWriterReports/{reportId}/RunReportWriterReport?GetOptions=ReturnCSVUrl";

            try
            {
                client.DefaultRequestHeaders.Add("X-RM12Api-ApiToken", authToken);
                var response = await client.GetAsync(requestUrl);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to get CSV download URL for ReportId {reportId}. Message: {response.ReasonPhrase}");
                    return null;
                }

                // Get the CSV file download URL from the response
                string downloadUrl = await response.Content.ReadAsStringAsync();
                return downloadUrl.Trim('"');
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting CSV download URL: {ex.Message}");
                return null;
            }
        }


        private static async Task<byte[]> DownloadCsvFile(HttpClient client, string downloadUrl, int reportId, ILogger _logger)
        {
            try
            {
                var downloadResponse = await client.GetAsync(downloadUrl);

                if (!downloadResponse.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to download CSV file for ReportId {reportId}. Message: {downloadResponse.ReasonPhrase}");
                    return null;
                }

                return await downloadResponse.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error downloading CSV file for ReportId {reportId}: {ex.Message}");
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

        private static async Task UploadToBlobStorage(BlobContainerClient blobContainerClient, int reportId, byte[] fileContent, ILogger _logger)
        {
            try
            {
                // Check if the reportId exists in the dictionary and get the corresponding folder name
                if (ReportIdToFolderName.TryGetValue(reportId, out string folderName))
                {
                    // Use the folder name in the blob path
                    string blobName = $"{folderName}/{reportId}.csv";
                    BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);

                    using (var stream = new MemoryStream(fileContent))
                    {
                        await blobClient.UploadAsync(stream, overwrite: true);
                    }

                    _logger.LogInformation($"Successfully uploaded ReportId {reportId} to folder '{folderName}' in Blob Storage.");
                }
                else
                {
                    _logger.LogWarning($"No folder name mapped for ReportId {reportId}. Uploading to default 'Reports' folder.");

                    // Default to Reports folder if no mapping exists
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
                _logger.LogError($"Error uploading file to Blob Storage for ReportId {reportId}: {ex.Message}");
            }
        }
    }
}

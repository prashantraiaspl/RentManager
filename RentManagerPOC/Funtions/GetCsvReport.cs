using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure;
using System.Net.Http;
using System.Net.Http.Headers;

namespace RentManagerPOC.Funtions
{
    public static class GetCsvReport
    {
        private static readonly string BaseUrl = "https://goosepm.api.rentmanager.com";
        private static readonly HttpClient client = new HttpClient();

        static GetCsvReport()
        {
            client.BaseAddress = new Uri(BaseUrl);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromMinutes(10);
        }

        [FunctionName("GetCsvReport")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "GetCsvReport/{ReportId}")] HttpRequest req,
            ILogger _logger,
            int ReportId)
        {
            _logger.LogInformation("Process Started.");

            // Validate the request data
            if (ReportId <= 0)
            {
                return new BadRequestObjectResult("Invalid ReportId.");
            }

            // Extract the apiToken from the request headers
            if (!req.Headers.TryGetValue("X-RM12Api-ApiToken", out var apiToken))
            {
                return new UnauthorizedObjectResult(new { Status = false, Message = "Missing ApiToken in request headers." });
            }

            try
            {
                // Check if the apiToken header already exists
                if (client.DefaultRequestHeaders.Contains("X-RM12Api-ApiToken"))
                {
                    // If the header exists, remove it to avoid duplicates
                    client.DefaultRequestHeaders.Remove("X-RM12Api-ApiToken");
                }

                // Add the apiToken as a custom header (not Authorization)
                client.DefaultRequestHeaders.Add("X-RM12Api-ApiToken", apiToken.ToString());

                // Construct the URL with the dynamic ID
                string requestUrl = $"{BaseUrl}/ReportWriterReports/{ReportId}/RunReportWriterReport?GetOptions=ReturnCSVUrl";

                // Send GET request to the API
                HttpResponseMessage response = await client.GetAsync(requestUrl);

                if (!response.IsSuccessStatusCode)
                {
                    var errorResponse = new
                    {
                        Status = false,
                        Message = $"{response.ReasonPhrase}",
                        StatusCode = (int)response.StatusCode
                    };

                    _logger.LogError($"Failed to retrieve CSV file. Status code: {response.StatusCode}");

                    return new UnauthorizedObjectResult(errorResponse);
                }

                // Read the CSV download URL from the response
                string downloadUrl = await response.Content.ReadAsStringAsync();
                downloadUrl = downloadUrl.Trim('"');

                var responseBody = new
                {
                    Status = true,
                    Message = "CSV File Downloaded Successfully.",
                    StatusCode = (int)response.StatusCode,
                    DownloadUrl = downloadUrl
                };

                // Return the download URL along with success message
                return new OkObjectResult(responseBody);
            }
            catch (Exception ex)
            {
                _logger.LogError($"File Download Failed: {ex.Message}");
                return new StatusCodeResult(500);
            }
        }
    }
}

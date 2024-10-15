using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace RentManagerPOC.Functions
{
    public class ReportSender
    {
        // Define an array of report IDs
        private readonly int[] reportIds = new int[]
        {
            137, // Legal in Action
            62,  // Current Vacancy BI Use
            61,  // The No Pay For BI
            59,  // Actionable Report
            58,  // 2 Month Bal Report For BI Use
            66   // Department Rep Report
        };

        [FunctionName("ReportSender")]
        //public async Task Run([TimerTrigger("0 0 5,11,13,15,17 * * *")] TimerInfo myTimer, ILogger _logger)
        public async Task Run([TimerTrigger("0 0 */1 * * *")] TimerInfo myTimer, ILogger _logger)
        {
            _logger.LogInformation($"ReportSender function triggered at: {DateTime.Now}");

            foreach (var reportId in reportIds)
            {
                // Create a new HttpClient instance for each request
                using (HttpClient client = new HttpClient())
                {
                    // Set the timeout to 30 minutes
                    client.Timeout = TimeSpan.FromMinutes(30);

                    // Construct the API URL with the dynamic reportId
                    var apiUrl = $"https://rentmanagerpoc.azurewebsites.net/api/FileUploader/{reportId}?code=sP8hmtu7q3t2z7DOnSx2Uk1m4Eb3hcn8N9yhcsHvEAFDAzFuf8tuvQ==";

                    try
                    {
                        _logger.LogInformation($"Processing Report ID: {reportId}");

                        // Make the HTTP GET request
                        HttpResponseMessage response = await client.GetAsync(apiUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation($"Report '{reportId}.csv' Processed Successfully.");
                        }
                        else
                        {
                            _logger.LogError($"Failed to process Report ID {reportId}. Status Code: {response.ReasonPhrase}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error while sending request for Report ID {reportId}: {ex.Message}");
                    }

                    // Delay between each request (e.g., 30 seconds between requests)
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
            }
        }
    }
}

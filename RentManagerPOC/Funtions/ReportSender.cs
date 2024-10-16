using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace RentManagerPOC.Functions
{
    public class ReportSender
    {
        private static readonly string API_BASEURL = Environment.GetEnvironmentVariable("ClientFileUploaderApiURL");
        private static readonly string CRON_TIME = Environment.GetEnvironmentVariable("TEST_CRON_CANADIAN_CENTRAL_TIMEZONE") ?? "0 */30 * * * *";

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
        public async Task Run([TimerTrigger("0 0 5,11,13,15,17 * * *")] TimerInfo myTimer, ILogger log)
        {
            // Get current UTC time
            var utcNow = DateTime.UtcNow;

            // Convert to IST
            var NewYork_Now = TimeZoneInfo.ConvertTimeFromUtc(utcNow, TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));


            log.LogInformation("ReportSender function triggered at: {Time}", NewYork_Now);

            using (HttpClient client = new HttpClient())
            {
                // Set the timeout to 30 minutes for the HTTP client
                client.Timeout = TimeSpan.FromMinutes(30);

                foreach (var reportId in reportIds)
                {
                    var apiUrl = BuildApiUrl(reportId);
                    await ProcessReportAsync(client, reportId, apiUrl, log);

                    // Delay between each request (e.g., 30 seconds)
                    await Task.Delay(TimeSpan.FromSeconds(30));
                }
            }
        }


        // ----------------HELPER METHODS-------------------
        private string BuildApiUrl(int reportId)
        {
            return $"{API_BASEURL}/{reportId}?code={GetApiKey()}";
        }


        private string GetApiKey()
        {
            // Consider storing sensitive data securely
            return Environment.GetEnvironmentVariable("ClientDefaultApiKey");
        }


        private async Task ProcessReportAsync(HttpClient client, int reportId, string apiUrl, ILogger log)
        {
            log.LogInformation("Processing Report ID: {ReportId}", reportId);

            try
            {
                HttpResponseMessage response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    log.LogInformation("Report '{ReportId}.csv' processed successfully.", reportId);
                }
                else
                {
                    log.LogError("Failed to process Report ID {ReportId}. Status Code: {StatusCode}, Reason: {ReasonPhrase}",
                        reportId, response.StatusCode, response.ReasonPhrase);
                }
            }
            catch (HttpRequestException httpEx)
            {
                log.LogError("HTTP request error while processing Report ID {ReportId}: {Message}", reportId, httpEx.Message);
            }
            catch (Exception ex)
            {
                log.LogError("An error occurred while processing Report ID {ReportId}: {Message}", reportId, ex.Message);
            }
        }
    }
}

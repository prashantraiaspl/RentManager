using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace RentManagerPOC.Funtions
{
    public class TimerTest
    {
        [FunctionName("TimerTest")]
        public void Run([TimerTrigger("0 */11 * * * *")]TimerInfo myTimer, ILogger log)
        {
            // Get current UTC time
            var utcNow = DateTime.UtcNow;

            // Convert to IST
            var istNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"));


            log.LogInformation($"C# Timer trigger function executed at: {istNow}");
        }
    }
}

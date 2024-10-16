using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace RentManagerPOC.Funtions
{
    public class TimerTest
    {
        [FunctionName("TimerTest")]
        public void Run([TimerTrigger("*/5 * * * * *")]TimerInfo myTimer, ILogger log)
        {
            // Get current UTC time
            var utcNow = DateTime.UtcNow;

            // Convert to IST
            var NewYork_Now = TimeZoneInfo.ConvertTimeFromUtc(utcNow, TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));


            log.LogInformation($"C# Timer trigger function executed at: {NewYork_Now}");
        }
    }
}

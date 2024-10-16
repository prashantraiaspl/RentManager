using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;

namespace RentManagerPOC.Funtions
{
    public static class test
    {
        [FunctionName("test")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            string connectionString = Environment.GetEnvironmentVariable("AsplStorageConnectionString");
            string containerName = Environment.GetEnvironmentVariable("AsplContainerName");

            log.LogInformation($"Using connectionString: {connectionString}");
            log.LogInformation($"Using container: {containerName}");

            var responseMessage = new
            {
                success = true,
                message = "Azure Function API is Working",
                timestamp = DateTime.UtcNow
            };

            return new OkObjectResult(responseMessage);
        }
    }
}

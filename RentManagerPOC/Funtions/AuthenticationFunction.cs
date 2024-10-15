using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using RentManagerPOC.Models;
using System.Net.Http.Headers;

namespace RentManagerPOC.Funtions
{
    public static class AuthenticationFunction
    {
        private static readonly string RentManagerBaseUrl = "https://goosepm.api.rentmanager.com/";
        private static readonly HttpClient client = new HttpClient();

        static AuthenticationFunction()
        {
            client.BaseAddress = new Uri(RentManagerBaseUrl);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }


        [FunctionName("AuthenticateUser")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger _logger)
        {
            _logger.LogInformation("Processing authentication request.");

            // Read request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            UserAuthorizationModel authModel = JsonConvert.DeserializeObject<UserAuthorizationModel>(requestBody);


            // Validate the request data
            if (authModel == null || string.IsNullOrEmpty(authModel.username) || string.IsNullOrEmpty(authModel.password))
            {
                return new BadRequestObjectResult("Invalid request. Please provide valid username, password, and location ID.");
            }


            try
            {
                // Send POST request to Rent Manager API
                HttpResponseMessage response = await client.PostAsJsonAsync("authentication/AuthorizeUser/", authModel);


                if (!response.IsSuccessStatusCode)
                {
                    var errorResponse = new
                    {
                        Status = false,
                        Message = $"{response.ReasonPhrase}, Please Check Your Credentials.",
                        StatusCode = (int)response.StatusCode
                    };

                    _logger.LogError($"Authentication failed with status code: {response.StatusCode}");

                    return new UnauthorizedObjectResult(errorResponse);
                }

                // Read the response token
                string apiToken = await response.Content.ReadAsStringAsync();
                apiToken = apiToken.Trim('"');

                _logger.LogInformation($"Authentication Successful. Token Received:  {apiToken}");

                var responseBody = new
                {
                    Status = true,
                    Message = "Authentication Successful.",
                    StatusCode = (int)response.StatusCode,
                    token = apiToken
                };

                // Return the token in the response
                return new OkObjectResult(responseBody);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Authentication Failed: {ex.Message}");
                return new StatusCodeResult(500);
            }
        }
    }
}

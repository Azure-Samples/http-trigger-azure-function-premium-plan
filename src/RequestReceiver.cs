#region Copyright
//=======================================================================================
// This sample is supplemental to the technical guidance published on the community
// blog at https://github.com/paolosalvatori. 
// 
// Author: Paolo Salvatori
//=======================================================================================
// Copyright � 2021 Microsoft Corporation. All rights reserved.
// 
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EITHER 
// EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES OF 
// MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE. YOU BEAR THE RISK OF USING IT.
//=======================================================================================
#endregion

#region Using Directives
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
#endregion

namespace Microsoft.Azure.Samples
{
    public class RequestReceiver
    {
        #region Private Constants
        private const string IpifyUrl = "https://api.ipify.org";
        private const string Unknown = "UNKNOWN";
        private const string Dude = "dude";
        private const string NameParameter = "name";
        #endregion

        #region Private Instance Fields
        private readonly HttpClient httpClient;
        #endregion

        #region Public Constructor
        public RequestReceiver(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }
        #endregion

        [OpenApiOperation(operationId: "GetPublicIpAddress", tags: new[] { "name" }, Summary = "Gets the outbound public IP address of the Azure Function", Description = "This method calls the Ipify external site to get the outbound public IP address of the Azure Function app", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiParameter(name: "name", In = ParameterLocation.Query, Required = false, Type = typeof(string), Summary = "Enter your name", Description = "Specifies the name of the user that sends the message.", Visibility = OpenApiVisibilityType.Important)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Summary = "The response", Description = "This returns the response")]
        [FunctionName("ProcessRequest")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest request,
                                             [CosmosDB(databaseName: "%CosmosDbName%", collectionName:"%CosmosDbCollectionName%", ConnectionStringSetting = "CosmosDBConnection")] IAsyncCollector<Request> items,
                                             ILogger log,
                                             ExecutionContext executionContext)
        {
            try
            {
                // Read the name parameter
                var name = request.Query[NameParameter].FirstOrDefault() ?? Dude;

                // Log message
                log.LogInformation($"Started '{executionContext.FunctionName}' " +
                                   $"(Running, Id={executionContext.InvocationId}) " +
                                   $"A request has been received from {name}");

                // Retrieve the public IP from Ipify site
                var publicIpAddress = Unknown;
                try
                {
                    var response = await httpClient.GetAsync(IpifyUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        publicIpAddress = await response.Content.ReadAsStringAsync();

                        // Log message
                        log.LogInformation($"Running '{executionContext.FunctionName}' " +
                                           $"(Running, Id={executionContext.InvocationId}) " +
                                           $"Call to {IpifyUrl} returned {publicIpAddress}");
                    }
                }
                catch (Exception ex)
                {
                    log.LogError(ex, $"Error '{executionContext.FunctionName}' " +
                                     $"(Running, Id={executionContext.InvocationId}) " +
                                     $"An error occurred while calling {IpifyUrl}: {ex.Message}");
                }

                // Create response message
                var responseMessage = $"Hi {name}, the HTTP triggered function invoked " +
                                      $"the external service using the {publicIpAddress} public IP address.";

                // Initialize message
                var customMessage = new Request
                {
                    Id = executionContext.InvocationId.ToString(),
                    PublicIpAddress = publicIpAddress,
                    ResponseMessage = responseMessage,
                    RequestHeaders = request.Headers
                };

                // Store the message to Cosmos DB
                await items.AddAsync(customMessage);
                log.LogInformation($"Completed '{executionContext.FunctionName}' " +
                                   $"(Running, Id={executionContext.InvocationId}) "+
                                   $"The response has been successfully stored to Cosmos DB");
                // Return 
                return new OkObjectResult(responseMessage);
            }
            catch (Exception ex)
            {
                log.LogError(ex, $"Failed '{executionContext.FunctionName}' " +
                                 $"(Running, Id={executionContext.InvocationId}) {ex.Message}");
                return new BadRequestObjectResult("An error occurred while processing the request.");
                throw;
            }
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace QueueWorkAndCheckStatusDurableFunc
{
    public static class DoWorkAndCheckStatusFunction
    {
        [FunctionName("DoWorkAndCheckStatusFunction")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            var toQueueQork = context.GetInput<TodoWork>();
            log.LogInformation($"Starting queue work activity - name:{toQueueQork.name}");

            // Replace "hello" with the name of your Durable Activity Function.
            //  outputs.Add(await context.CallActivityAsync<string>("DoWorkAndCheckStatusFunction_QueueWork", "Tokyo"));


            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return new List<string>() { "Hello Tokyo!", "Hello Seattle!", "Hello London!" };
        }

        [FunctionName("DoWorkAndCheckStatusFunction_Hello")]
        public static string SayHello([ActivityTrigger] TodoWork work, ILogger log)
        {
            log.LogInformation($"Welcoming new work {work.name}.");
            return $"Welcoming work {work.name}!";
        }

        [FunctionName("DoWorkAndCheckStatusFunction_QueueWork")]
        public static async Task<HttpResponseMessage> QueueWork(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.

            HttpContent requestContent = req.Content;
            string jsonContent = requestContent.ReadAsStringAsync().Result;
            var toDoWork = JsonSerializer.Deserialize<TodoWork>(jsonContent);

            log.LogInformation($"Queue work request received, work name is = {toDoWork.name}");

            string instanceId = await starter.StartNewAsync("DoWorkAndCheckStatusFunction", toDoWork);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            string checkStatusLocacion = string.Format("{0}://{1}/api/status/{2}", req.RequestUri.Scheme, req.RequestUri.Host, instanceId); // To inform the client where to check the status
            string message = $"Your submission has been received. To get the status, go to: {checkStatusLocacion}";

            // Create an Http Response with Status Accepted (202) to let the client know that the request has been accepted but not yet processed. 
            ActionResult response = new AcceptedResult(checkStatusLocacion, message); // The GET status location is returned as an http header
            req.HttpContext.Response.Headers.Add("retry-after", "20"); // To inform the client how long to wait in seconds before checking the status

            return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
        }

        public class TodoWork
        {
            public string name { get; set; }
            public bool isComplete { get; set; }
        }
    }
}
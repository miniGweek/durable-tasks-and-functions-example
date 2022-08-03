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
            var outputs = new List<string>();

            // Replace "hello" with the name of your Durable Activity Function.
            outputs.Add(await context.CallActivityAsync<string>("DoWorkAndCheckStatusFunction_QueueWork", "Tokyo"));
          

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
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
            var workItem = JsonSerializer.Deserialize<TodoWork>(jsonContent);
            string instanceId = await starter.StartNewAsync("DoWorkAndCheckStatusFunction", workItem);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        public class TodoWork
        {
            public string name { get; set; }
            public bool isComplete { get; set; }
        }
    }
}
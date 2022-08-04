using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;


namespace QueueWorkAndCheckStatusDurableFunc
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHttpClient();
            builder.Services.AddLogging();
            builder.Services.AddSingleton<DoWorkAndCheckStatusFunction>();
            builder.Services.AddSingleton<ILogger, ILogger<DoWorkAndCheckStatusFunction>>();
        }
    }
    public class DoWorkAndCheckStatusFunction
    {
        private readonly HttpClient _client;
        private readonly ILogger<DoWorkAndCheckStatusFunction> _log;

        public DoWorkAndCheckStatusFunction(IHttpClientFactory httpClientFactory,
            ILogger<DoWorkAndCheckStatusFunction> logger)
        {
            this._client = httpClientFactory.CreateClient();
            this._log = logger;
        }
        [FunctionName("DoWorkAndCheckStatusFunction")]
        public async Task<bool> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var toQueueQork = context.GetInput<TodoWork>();
            
            if (!context.IsReplaying)
            {
                _log.LogWarning($"Starting queue work activity - name:{toQueueQork.name}");
            }
            
            var toDoWork = await context.CallActivityAsync<TodoWork>("SubmitWork_To_API", toQueueQork);

            if (!context.IsReplaying)
            {
                _log.LogWarning($"Work queued - name:{toQueueQork.name}, id: {toDoWork.id}");
            }

            // monitoring logic
            DateTime endTime = context.CurrentUtcDateTime.AddMinutes(30);
            if (!context.IsReplaying) { _log.LogWarning($"Instantiating monitor for Work queued - name:{toQueueQork.name}, id: {toDoWork.id}. Expires checking at {endTime}."); }
            while (context.CurrentUtcDateTime < endTime)
            {
                // check work status
                if (!context.IsReplaying) { _log.LogWarning($"Checking work status for work queued - name:{toQueueQork.name}, id: {toDoWork.id} at {context.CurrentUtcDateTime}."); }
                var isWorkDone = await context.CallActivityAsync<bool>("CheckWorkStatus_In_API", toDoWork);

                if (isWorkDone)
                {
                    // Work is done
                    if (!context.IsReplaying) { _log.LogWarning($"Work status is DONE for work queued - name:{toQueueQork.name}, id: {toDoWork.id} at {context.CurrentUtcDateTime}."); }
                    context.SetCustomStatus("WorkDone");
                    break;
                }
                else
                {
                    // Wait for the next checkpoint
                    var nextCheckpoint = context.CurrentUtcDateTime.AddSeconds(30);
                    if (!context.IsReplaying) { _log.LogWarning($"Next check for work queued - name:{toQueueQork.name}, id: {toDoWork.id} at {nextCheckpoint}."); }
                    await context.CreateTimer(nextCheckpoint, CancellationToken.None);
                }
            }
            return true;
        }

        [FunctionName("SubmitWork_To_API")]
        public async Task<TodoWork> SubmitWorkToAPI(
            [ActivityTrigger] IDurableActivityContext context)
        {

            // Makes an HTTP GET request to the specified endpoint
            var todoWork = context.GetInput<TodoWork>();
            var response = await _client.PostAsync("http://localhost:5000/todoworkitems", JsonContent.Create(todoWork));
            _log.LogWarning($"Work {todoWork.name} submitted");
            return JsonSerializer.Deserialize<TodoWork>(await response.Content.ReadAsStringAsync());
        }

        [FunctionName("CheckWorkStatus_In_API")]
        public async Task<bool> CheckWorkStatusInApi(
            [ActivityTrigger] IDurableActivityContext context)
        {

            // Makes an HTTP GET request to the specified endpoint
            var todoWork = context.GetInput<TodoWork>();
            var response = await _client.GetAsync($"http://localhost:5000/todoworkitems/{todoWork.id}");
            var responseWork = JsonSerializer.Deserialize<TodoWork>(await response.Content.ReadAsStringAsync());
            return responseWork.isComplete;
        }

        [FunctionName("queuework")]
        public async Task<HttpResponseMessage> QueueWork(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "queuework")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter)
        {
            // Function input comes from the request content.

            var requestContent = req.Content;
            var jsonContent = requestContent.ReadAsStringAsync().Result;
            var toDoWork = JsonSerializer.Deserialize<TodoWork>(jsonContent);

            _log.LogInformation($"Work queued initiated - This is Information.");
            _log.LogWarning($"Queue work request received, work name is = {toDoWork.name}");

            var instanceId = await starter.StartNewAsync("DoWorkAndCheckStatusFunction", toDoWork);

            _log.LogWarning($"Started orchestration with ID = '{instanceId}'.");

            var checkStatusLocation = string.Format("{0}://{1}:{2}/api/status/{3}", req.RequestUri.Scheme, req.RequestUri.Port, req.RequestUri.Host, instanceId); // To inform the client where to check the status
            var message = $"Your submission has been received. To get the status, go to: {checkStatusLocation}";

            var response = new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = JsonContent.Create(new AcceptedResult
                {
                    location = checkStatusLocation,
                    value = message
                }),
                Headers = { RetryAfter = RetryConditionHeaderValue.Parse("20") }
            };
            return response;
        }

        [FunctionName("getstatus")]
        public async Task<HttpResponseMessage> GetStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get",Route = "status/{instanceId}")]
            HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient orchestrationClient,
            string instanceId)
        {
            var status = await orchestrationClient.GetStatusAsync(instanceId);

            if (status != null)
            {
                // Get the custom status of the orchestration intance. This status is set by our code. 
                // This can be any serialisable object. In this case, just a string. 
                string customStatus = (string)status.CustomStatus;
                _log.LogWarning($"For InstanceId:{instanceId}, Orchestrator status is {status.RuntimeStatus} and customStatus is {customStatus}");
                if (status.RuntimeStatus == OrchestrationRuntimeStatus.Running || status.RuntimeStatus == OrchestrationRuntimeStatus.Pending)
                {
                    //The URL (location header) is prepared so the client know where to get the status later. 
                    var checkStatusLocation = string.Format("{0}://{1}:{2}/api/status/{3}", req.RequestUri.Scheme, req.RequestUri.Host, req.RequestUri.Port, instanceId); // To inform the client where to check the status
                    var message = $"Work has been queued. To get the status, go to: {checkStatusLocation}";

                    // Create an Http Response with Status Accepted (202) to let the client know that the original request hasn't yet been fully processed. 
                    var response = new HttpResponseMessage(HttpStatusCode.Accepted)
                    {
                        Content = JsonContent.Create(new AcceptedResult
                        {
                            location = checkStatusLocation,
                            value = message
                        }),
                        Headers = { RetryAfter = RetryConditionHeaderValue.Parse("20") }
                    };
                    return response;
                }
                else if (status.RuntimeStatus == OrchestrationRuntimeStatus.Completed)
                {
                    // Once the orchestration has been completed, an Http Response with Status OK (200) is created to inform the client that the original request has been fully processed. 
                    if (customStatus == "WorkDone")
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent($"Congratulations, your presentation with id '{instanceId}' has been accepted!")

                        };
                    else
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent($"We are sorry! Unfortunately your presentation with id '{instanceId}' has not been accepted.")

                        };
                }
            }
            var requestContent = req.Content;
            var response2 = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("Ok")
            };
            return response2;
        }

        public class TodoWork
        {
            public int id { get; set; }
            public string name { get; set; }
            public bool isComplete { get; set; }
        }

        public class AcceptedResult
        {
            public string location { get; set; }
            public string value { get; set; }
        }
    }
}
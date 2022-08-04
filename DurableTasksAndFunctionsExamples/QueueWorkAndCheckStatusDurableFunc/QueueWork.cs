using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace QueueWorkAndCheckStatusDurableFunc;

public partial class DoWorkAndCheckStatusFunction
{
    [FunctionName("queuework")]
    public async Task<HttpResponseMessage> QueueWork(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "queuework")]
        HttpRequestMessage req,
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

        var checkStatusLocation = string.Format("{0}://{1}:{2}/api/status/{3}",
            req.RequestUri.Scheme,
           req.RequestUri.Host,
            req.RequestUri.Port,
            instanceId); // To inform the client where to check the status
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
}
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace QueueWorkAndCheckStatusDurableFunc;

public partial class DoWorkAndCheckStatusFunction
{
    [FunctionName("getstatus")]
    public async Task<HttpResponseMessage> GetStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "status/{instanceId}")]
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
            _log.LogWarning(
                $"For InstanceId:{instanceId}, Orchestrator status is {status.RuntimeStatus} and customStatus is {customStatus}");

            if (status.RuntimeStatus == OrchestrationRuntimeStatus.Running ||
                status.RuntimeStatus == OrchestrationRuntimeStatus.Pending)
            {
                //The URL (location header) is prepared so the client know where to get the status later. 
                var checkStatusLocation = string.Format("{0}://{1}:{2}/api/status/{3}", req.RequestUri.Scheme,
                    req.RequestUri.Host, req.RequestUri.Port,
                    instanceId); // To inform the client where to check the status
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
                        Content = new StringContent(
                            $"Work is done for the '{instanceId}'!")
                    };
                else
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            $"Orchestration finished, but Work for the '{instanceId}' hasn't been done.")
                    };
            }
        }

        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}
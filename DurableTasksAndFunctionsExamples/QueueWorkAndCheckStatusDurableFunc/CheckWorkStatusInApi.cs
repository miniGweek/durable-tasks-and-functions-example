using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace QueueWorkAndCheckStatusDurableFunc;

public partial class DoWorkAndCheckStatusFunction
{
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
}
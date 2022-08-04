using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace QueueWorkAndCheckStatusDurableFunc;

public partial class DoWorkAndCheckStatusFunction
{
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
}
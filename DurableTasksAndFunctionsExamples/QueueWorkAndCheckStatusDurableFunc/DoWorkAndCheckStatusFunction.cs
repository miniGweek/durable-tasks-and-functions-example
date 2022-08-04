using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
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

    public partial class DoWorkAndCheckStatusFunction
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
            if (!context.IsReplaying)
            {
                _log.LogWarning(
                    $"Instantiating monitor for Work queued - name:{toQueueQork.name}, id: {toDoWork.id}. Expires checking at {endTime}.");
            }

            int counter = 0;

            while (context.CurrentUtcDateTime < endTime)
            {
                counter++;
                if (!context.IsReplaying)
                {
                    _log.LogWarning(
                        $"### Iteration: {counter} Checking work status for work queued - name:{toQueueQork.name}, id: {toDoWork.id}.");
                }

                // check work status
                if (!context.IsReplaying)
                {
                    _log.LogWarning(
                        $"### Iteration: {counter} Checking work status for work queued - name:{toQueueQork.name}, id: {toDoWork.id} at {context.CurrentUtcDateTime}.");
                }

                var isWorkDone = await context.CallActivityAsync<bool>("CheckWorkStatus_In_API", toDoWork);

                if (isWorkDone)
                {
                    // Work is done
                    if (!context.IsReplaying)
                    {
                        _log.LogWarning(
                            $"### Iteration: {counter} Work status is DONE for work queued -name:{ toQueueQork.name}, id: { toDoWork.id} at { context.CurrentUtcDateTime}.");
                    }
                    
                    context.SetCustomStatus("WorkDone");
                    break;
                }
                else
                {
                    // Wait for the next checkpoint
                    int nextInterval = GetInterval(10, counter);
                    if (!context.IsReplaying)
                    {
                        _log.LogWarning(
                            $"### Iteration: {counter} Next check for work queued - name:{toQueueQork.name}, id: {toDoWork.id} after {nextInterval} seconds.");
                    }

                    var nextCheckpoint = context.CurrentUtcDateTime.AddSeconds(nextInterval);
                    if (!context.IsReplaying)
                    {
                        _log.LogWarning(
                            $"### Iteration: {counter} Next check for work queued - name:{toQueueQork.name}, id: {toDoWork.id} at {nextCheckpoint}.");
                    }

                    await context.CreateTimer(nextCheckpoint, CancellationToken.None);
                }
            }

            return true;
        }

        private int GetInterval(int seed, int index)
        {
            var interval = seed + index * 10;

            return interval > 60 ? 60 : interval;
        }
    }
}
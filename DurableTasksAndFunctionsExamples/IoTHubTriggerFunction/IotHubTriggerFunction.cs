using Microsoft.Azure.WebJobs;
using Azure.Messaging.EventHubs;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace IoTHubTriggerFunction
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddLogging();
            builder.Services.AddSingleton<IotHubTriggerFunction>();
            builder.Services.AddSingleton<ILogger, ILogger<IotHubTriggerFunction>>();
        }
    }
    public class IotHubTriggerFunction
    {
        private readonly ILogger<IotHubTriggerFunction> log;
       

        public IotHubTriggerFunction(ILogger<IotHubTriggerFunction> logger)
        {
            this.log = logger;
        }
        [FunctionName("Function1")]
        public void Run([EventHubTrigger(
            eventHubName:  "iot-d-captis-iotedgepoc-a",
            Connection = "IoTHubConnectionString",
            ConsumerGroup = "$Default"
        )]EventData message)
        {
            string messageBody = message.EventBody.ToString();

            log.LogInformation($"C# IoT Hub trigger function processed a message: {messageBody}");
            log.LogInformation($"EventBody: {messageBody}");
            log.LogInformation($"EnqueuedTimeUtc: {message.EnqueuedTime}");
            log.LogInformation($"Offset: {message.Offset}");
            log.LogInformation($"PartitionKey: {message.PartitionKey}");
            log.LogInformation($"SequenceNumber: {message.SequenceNumber}");
            log.LogInformation($"Properties: {JsonSerializer.Serialize(message.Properties)}");
        }
    }
}
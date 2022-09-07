using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Microsoft.Extensions.DependencyInjection;

namespace IotEdgeModule1
{
    internal class Program
    {
        static int counter;
        static int temperatureThreshold { get; set; } = 20;

        public static ServiceProvider ServiceProvider { get; private set; }

        static void Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            // Build the our IServiceProvider and set our static reference to it
            ServiceProvider = serviceCollection.BuildServiceProvider();

            // Initialize module
            ServiceProvider.GetRequiredService<TestModule>()
                .InitializeAsync()
                .GetAwaiter()
                .GetResult();


            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        private static void ConfigureServices(ServiceCollection serviceCollection)
        {
            serviceCollection.AddLogging();
            serviceCollection.AddModuleClient(new AmqpTransportSettings(TransportType.Amqp_Tcp_Only));
            serviceCollection.AddSingleton<TestModule>();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            // Register callback to be called when a message is received by the module
            //await ioTHubModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, ioTHubModuleClient);

            // Read the TemperatureThreshold value from the module twin's desired properties
            var moduleTwin = await ioTHubModuleClient.GetTwinAsync();
            await OnDesiredPropertiesUpdate(moduleTwin.Properties.Desired, ioTHubModuleClient);

            // Attach a callback for updates to the module twin's desired properties.
            await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, null);

            // Register a callback for messages that are received by the module. Messages received on the inputFromSensor endpoint are sent to the FilterMessages method.
            await ioTHubModuleClient.SetInputMessageHandlerAsync("inputFromAnotherModule", FilterMessages, ioTHubModuleClient);

            //Update module twin locally and expect to see trigger azure function
            // await UpdateModuleTwin(ioTHubModuleClient, moduleTwin);

            while (true)
            {
                await Task.Delay(10000);
                var deviceEvent = new DeviceEventMessageBody()
                {
                    deviceId = "80001212",
                    deviceType = "Captis Multi",
                    note = "This is a test"

                };
                var deviceEventString = System.Text.Json.JsonSerializer.Serialize(deviceEvent);
                var eventMessage = new Message(Encoding.UTF8.GetBytes(deviceEventString));
                await ioTHubModuleClient.SendEventAsync("outputs", eventMessage);
            }
        }

        static async Task UpdateModuleTwin(ModuleClient ioTHubModuleClient, Twin moduleTwin)
        {
            var allowedEvents = new List<string>() { "WakeUp", "DoWork", "Sleep", "Shutdown", "FinishWork" };
            while (true)
            {
                Random rnd = new Random();
                var randomNumber = rnd.Next(5);
                var allowedEvent = allowedEvents[randomNumber];
                await Task.Delay(10000);

                Console.WriteLine(JsonConvert.SerializeObject(moduleTwin.Properties));

                TwinCollection reportedProperties = new TwinCollection();
                reportedProperties["EventThatHappened"] = allowedEvent;
                reportedProperties["EventId"] = randomNumber;

                Console.WriteLine($"Sending EventThatHappened reported property: {allowedEvent}, EventId: {randomNumber}");
                await ioTHubModuleClient.UpdateReportedPropertiesAsync(reportedProperties);
            }
        }

        static Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                Console.WriteLine($"Desired property change: {JsonConvert.SerializeObject(desiredProperties)}");

                if (desiredProperties["TemperatureThreshold"] != null)
                    temperatureThreshold = desiredProperties["TemperatureThreshold"];

            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error when receiving desired property: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error when receiving desired property: {0}", ex.Message);
            }
            return Task.CompletedTask;
        }

        static async Task<MessageResponse> FilterMessages(Message message, object userContext)
        {
            var counterValue = Interlocked.Increment(ref counter);
            try
            {
                ModuleClient moduleClient = (ModuleClient)userContext;
                var messageBytes = message.GetBytes();
                var messageString = Encoding.UTF8.GetString(messageBytes);
                var timeStamp = DateTime.UtcNow;
                Console.WriteLine($"Received message {counterValue}: [{messageString}] at {timeStamp}");

                // Get the message body.
                var messageBody = JsonConvert.DeserializeObject<MessageBody>(messageString);

                if (messageBody != null && messageBody.machine.temperature > temperatureThreshold)
                {
                    Console.WriteLine($"Machine temperature {messageBody.machine.temperature} " +
                        $"exceeds threshold {temperatureThreshold}");
                    using (var filteredMessage = new Message(messageBytes))
                    {
                        foreach (KeyValuePair<string, string> prop in message.Properties)
                        {
                            filteredMessage.Properties.Add(prop.Key, prop.Value);
                        }

                        filteredMessage.Properties.Add("MessageType", "Alert");
                        await moduleClient.SendEventAsync("outputs", filteredMessage);
                    }
                }

                // Indicate that the message treatment is completed.
                return MessageResponse.Completed;
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error in sample: {0}", exception);
                }
                // Indicate that the message treatment is not completed.
                var moduleClient = (ModuleClient)userContext;
                return MessageResponse.Abandoned;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error in sample: {0}", ex.Message);
                // Indicate that the message treatment is not completed.
                ModuleClient moduleClient = (ModuleClient)userContext;
                return MessageResponse.Abandoned;
            }
        }
    }
}

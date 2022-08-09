using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;

namespace UpdateModuleTwinDesiredProperties
{
    public class Program
    {
        public const string IoTHubConnectionString =
            "HostName=iot-d-captis-iotedgepoc-aue-miot-001.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=3iCud/s9gys6gPdZ+Q8b6qAWcVqW7y+vsv+xwZx7u4s=";

        public const string IoTEdgeDeviceName = "poc-rahul-edge-rasppi-4-05";
        public const string IoTEdgeModuleName = "poc-rahul-edge-rasppi-4-05/IotEdgeModule1";
        public const string ModuleConnectionString = "<Your module connection string>";
        public static ModuleClient Client;
        public static void Main(string[] args)
        {
            Microsoft.Azure.Devices.Client.TransportType transport = Microsoft.Azure.Devices.Client.TransportType.Amqp;

            try

            {
                var deviceClient = ModuleClient.CreateFromConnectionString()
                Client = 
                    ModuleClient.CreateFromConnectionString(ModuleConnectionString, transport);
                Client.SetConnectionStatusChangesHandler(ConnectionStatusChangeHandler);
                Client.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, null).Wait();

                Console.WriteLine("Retrieving twin");
                var twinTask = Client.GetTwinAsync();
                twinTask.Wait();
                var twin = twinTask.Result;
                Console.WriteLine(JsonConvert.SerializeObject(twin.Properties));

                Console.WriteLine("Sending app start time as reported property");
                TwinCollection reportedProperties = new TwinCollection();
                reportedProperties["DateTimeLastAppLaunch"] = DateTime.Now;

                Client.UpdateReportedPropertiesAsync(reportedProperties);
            }
            catch

                (AggregateException ex)
            {
                Console.WriteLine("Error in sample: {0}", ex);
            }

            Console.WriteLine("Waiting for Events.  Press enter to exit...");
            Console.ReadLine();
            Client.CloseAsync().Wait();
        }

        static void ConnectionStatusChangeHandler(ConnectionStatus status,
            ConnectionStatusChangeReason reason)
        {
            Console.WriteLine("Connection Status Changed to {0}; the reason is {1}",
                status, reason);
        }

        private static async Task OnDesiredPropertyChanged(TwinCollection desiredProperties,
            object userContext)
        {
            Console.WriteLine("desired property change:");
            Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));
            Console.WriteLine("Sending current time as reported property");
            TwinCollection reportedProperties = new TwinCollection
            {
                ["DateTimeLastDesiredPropertyChangeReceived"] = DateTime.Now
            };

            await Client.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);
        }

    }

}
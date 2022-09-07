using Microsoft.Azure.Devices.Client;
using System;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IotEdgeModule1
{
    internal class Program
    {
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
            serviceCollection.AddLogging( s => s.AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.SingleLine = true;
                    options.TimestampFormat = "hh:mm:ss ";
                }));
            //using ILoggerFactory loggerFactory =
            //    LoggerFactory.Create(builder =>
            //        builder.AddSimpleConsole(options =>
            //        {
            //            options.IncludeScopes = true;
            //            options.SingleLine = true;
            //            options.TimestampFormat = "hh:mm:ss ";
            //        }));
            //ILogger<Program> logger = loggerFactory.CreateLogger<Program>();
            //using (logger.BeginScope("[scope is enabled]"))
            //{
            //    logger.LogInformation("Hello World!");
            //    logger.LogInformation("Logs contain timestamp and log level.");
            //    logger.LogInformation("Each log message is fit in a single line.");
            //}
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

    }
}

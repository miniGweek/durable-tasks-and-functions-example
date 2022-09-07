using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IotEdgeModule1
{
    internal class TestModule
    {
        private readonly IModuleClient _moduleClient;
        private readonly ILogger<TestModule> _logger;
        double temperatureThreshold = 25;
        public double TemperatureThreshold => this.temperatureThreshold;

        public TestModule(IModuleClient moduleClient, ILogger<TestModule> logger)
        {
            this._moduleClient = moduleClient;
            this._logger = logger;
        }

        public async Task InitializeAsync()
        {
            await this._moduleClient.OpenAsync();
            _logger.LogInformation("My module client initialized.");

            // Resolve temperature thresold from module twin
            var moduleTwin = await this._moduleClient.GetTwinAsync();
            if (moduleTwin.Properties.Desired != null && moduleTwin.Properties.Desired.Contains("TemperatureThreshold"))
            {
                var tempThreshold = moduleTwin.Properties.Desired["TemperatureThreshold"]?.ToString() ?? string.Empty;
                if (double.TryParse(tempThreshold, out double newTemperatureThreshold))
                {
                    _logger.LogInformation("Using temperature threshold from module twin: {newTemperatureThreshold}", newTemperatureThreshold);
                    this.temperatureThreshold = newTemperatureThreshold;
                }

            }

            // Register callback for twin changes
            await this._moduleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, null);


            // Register callback to be called when a message is received by the module
            //await this._moduleClient.SetInputMessageHandlerAsync("input1", PipeMessage, null);
        }

        private Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            if (desiredProperties != null && desiredProperties.Contains("TemperatureThreshold"))
            {
                var tempThreshold = desiredProperties["TemperatureThreshold"]?.ToString() ?? string.Empty;
                if (double.TryParse(tempThreshold, out double newTemperatureThreshold))
                {
                    this._logger.LogInformation("Temperature threshold updated from {actualTemperature} to {newTemperature}", this.temperatureThreshold, newTemperatureThreshold);
                    this.temperatureThreshold = newTemperatureThreshold;
                }
            }

            return Task.FromResult(0);
        }
    }
}

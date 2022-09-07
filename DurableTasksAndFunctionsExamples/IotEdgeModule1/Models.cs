using System;
using System.Collections.Generic;
using System.Text;

namespace IotEdgeModule1
{
    //MessageBody arriving from the SimulatedTemperature sensor module
    //Indicative of module to module communication
    class MessageBody
    {
        public Machine machine { get; set; }
        public Ambient ambient { get; set; }
        public string timeCreated { get; set; }
    }
    class Machine
    {
        public double temperature { get; set; }
        public double pressure { get; set; }
    }
    class Ambient
    {
        public double temperature { get; set; }
        public int humidity { get; set; }
    }

    class DeviceEventMessageBody
    {
        public string deviceId { get; set; }
        public string deviceType { get; set; }
        public string note { get; set; }
        public string correlationId { get; set; }
        public DateTime generatedTimeStamp { get; set; }
    }
}

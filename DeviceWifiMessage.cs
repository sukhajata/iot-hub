using System;
using System.IO;
using Newtonsoft.Json;

namespace DeviceWifiToKafka
{
    public partial class DeviceWifiMessage
    {
        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("deveui")]
        public string DeviceEUI { get; set; }

        [JsonProperty("data")]
        public string Data { get; set; }

        public static DeviceWifiMessage FromJson(string json) => JsonConvert.DeserializeObject<DeviceWifiMessage>(json, Converter.Settings);
    }


    public static class Serialize
    {
        public static string ToJson(this DeviceWifiMessage self) => JsonConvert.SerializeObject(self, Converter.Settings);

    }

    public class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None
        };
    }
}

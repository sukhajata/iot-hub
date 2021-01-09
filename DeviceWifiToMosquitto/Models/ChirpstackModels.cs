using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

/*
{
    "applicationID": "123",
    "applicationName": "temperature-sensor",
    "deviceName": "garden-sensor",
    "devEUI": "0202020202020202",
    "rxInfo": [
        {
            "gatewayID": "0303030303030303",          // ID of the receiving gateway
            "name": "rooftop-gateway",                 // name of the receiving gateway
            "time": "2016-11-25T16:24:37.295915988Z",  // time when the package was received (GPS time of gateway, only set when available)
            "rssi": -57,                               // signal strength (dBm)
            "loRaSNR": 10,                             // signal to noise ratio
            "location": {
                "latitude": 52.3740364,  // latitude of the receiving gateway
                "longitude": 4.9144401,  // longitude of the receiving gateway
                "altitude": 10.5,        // altitude of the receiving gateway
            }
        }
    ],
    "txInfo": {
        "frequency": 868100000,  // frequency used for transmission
        "dr": 5                  // data-rate used for transmission
    },
    "adr": false,                  // device ADR status
    "fCnt": 10,                    // frame-counter
    "fPort": 5,                    // FPort
    "data": "...",                 // base64 encoded payload (decrypted)
    "object": {                    // decoded object (when application coded has been configured)
        "temperatureSensor": {"1": 25},
        "humiditySensor": {"1": 32}
    }
}

    */

namespace DeviceWifiToMosquitto.Models
{
    public class Location
    {
        [JsonProperty("latitude")]
        public double Latitude { get; set; }
        [JsonProperty("longitude")]
        public double Longitude { get; set; }
        [JsonProperty("altitude")]
        public double Altitude { get; set; }
    }

    public class RxInfo
    {
        [JsonProperty("gatewayID")]
        public string GatewayID { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("time")]
        public string Time { get; set; }
        [JsonProperty("rssi")]
        public double Rssi { get; set; }
        [JsonProperty("loRaSNR")]
        public double LoRaSNR { get; set; }
        [JsonProperty("location")]
        public Location Location { get; set; }
    }

    public class TxInfo
    {
        [JsonProperty("frequency")]
        public long Frequency { get; set; }
        [JsonProperty("dr")]
        public int Dr { get; set; }
    }

    public partial class ChirpStackioMessage
    {
        [JsonProperty("applicationID")]
        public string ApplicationID { get; set; }
        [JsonProperty("applicationName")]
        public string ApplicationName { get; set; }
        [JsonProperty("deviceName")]
        public string DeviceName { get; set; }
        [JsonProperty("devEUI")]
        public string DevEUI { get; set; }
        [JsonProperty("rxInfo")]
        public List<RxInfo> RxInfo { get; set; }
        [JsonProperty("txInfo")]
        public TxInfo TxInfo { get; set; }
        [JsonProperty("adr")]
        public bool Adr { get; set; }
        [JsonProperty("fCnt")]
        public long FCnt { get; set; }
        [JsonProperty("fPort")]
        public int FPort { get; set; }
        [JsonProperty("data")]
        public string Data { get; set; }
    }



    public partial class ChirpStackioMessage
    {
        public static ChirpStackioMessage FromJson(string json) => JsonConvert.DeserializeObject<ChirpStackioMessage>(json, Converter.Settings);
    }

    public static class Serialize
    {
        public static string ToJson(this ChirpStackioMessage self) => JsonConvert.SerializeObject(self, Converter.Settings);
    }

    public class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Error = delegate (object sender, ErrorEventArgs args)
            {
                //errors.Add(args.ErrorContext.Error.Message);
                args.ErrorContext.Handled = true;
            },
        };
    }
}

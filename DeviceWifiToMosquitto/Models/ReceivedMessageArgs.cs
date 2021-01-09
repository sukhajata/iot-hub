using System;

namespace DeviceWifiToMosquitto.Models
{
	public class ReceivedMessageArgs : EventArgs
	{
		public string Payload { get; set; }

		public ReceivedMessageArgs(string payload)
		{
			Payload = payload;
		}
	}

}

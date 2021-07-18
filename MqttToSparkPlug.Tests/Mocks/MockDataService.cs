using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PowerPilotCommon.Interfaces;
using PowerPilotCommon.Models.PostgreSQL;
using Ppuplink;

namespace MqttToSparkPlug.Tests.Mocks
{
    public class MockDataService : IDataServiceClient
    {
        public EnergyMessage Energy { get; set; }
        public bool Status { get; set; }
        public PQMessage PQ { get; set; }
        public UplinkMessage Uplink { get; set; }
        public VoltageStatsMessage VoltageStats { get; set; }
        
        public MockDataService()
        {
        }

        public Task<Connection> GetConnection(string deviceEUI)
        {
            return Task.FromResult(new Connection()
            {
                ConnectionType = 1,
                DeviceEUI = "123",
                Line1 = 1,
                Line2 = 0,
                Line3 = 0
            });
        }

        public Task<bool> GetDeviceStatus(string deviceEUI)
        {
            return Task.FromResult(Status);
        }

        public Task<EnergyMessage> GetEnergy(string deviceEUI)
        {
            return Task.FromResult(Energy);
        }

        public Task<PQMessage> GetPQ(string deviceEUI, int phaseID)
        {
            return Task.FromResult(PQ);
        }

        public Task<UplinkMessage> GetUplink(string deviceEUI)
        {
            return Task.FromResult(Uplink);
        }

        public Task<VoltageStatsMessage> GetVoltageStats(string deviceEUI, int phaseID)
        {
            return Task.FromResult(VoltageStats);
        }

        public Task<int> GetMeterStatus(string deviceEUI)
        {
            throw new NotImplementedException();
        }

        public Task<List<ControlGroupMapping>> GetCommandPoints()
        {
            throw new NotImplementedException();
        }

        public Task<List<ControlGroupMember>> GetDevicesInControlGroup(string controlPointId)
        {
            throw new NotImplementedException();
        }
    }
}

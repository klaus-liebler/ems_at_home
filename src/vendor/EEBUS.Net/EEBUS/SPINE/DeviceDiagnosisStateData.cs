using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net;
using EEBUS.Net.EEBUS.Models.Data;
using System.Text.Json.Serialization;

namespace EEBUS.SPINE.Commands
{
    /// <summary>
    /// deviceDiagnosisStateData is a single-instance Function (not a list) - the remote's current
    /// operating state (normalOperation/failure/off/...), last error, power-supply condition. Read
    /// on demand only, same reasoning as DeviceClassificationManufacturerData.
    /// </summary>
    public class DeviceDiagnosisStateData : SpineCmdPayload<CmdDeviceDiagnosisStateDataType>
    {
        static DeviceDiagnosisStateData()
        {
            Register("deviceDiagnosisStateData", new Class());
        }

        public new class Class : SpineCmdPayload<CmdDeviceDiagnosisStateDataType>.Class
        {
            public override SpineCmdPayloadBase? CreateRead(Connection connection)
            {
                return new DeviceDiagnosisStateData();
            }

            public override async Task ReadDataAsync(Connection connection, DeviceData deviceData)
            {
                if (connection.Remote == null || deviceData.DiagnosisState == null) return;

                AddressType? featureSourceAddress = connection.Local.GetFeatureAddress("DeviceDiagnosis", false);
                AddressType? featureDestinationAddress = connection.Remote.GetFeatureAddress("DeviceDiagnosis", true);
                if (featureSourceAddress == null || featureDestinationAddress == null) return;

                var reply = await ReadFunctionFromRemoteAsync<DeviceDiagnosisStateData>(
                    connection, "DeviceDiagnosis", "deviceDiagnosisStateData");
                var data = reply?.cmd.First().deviceDiagnosisStateData;
                if (data == null) return;

                deviceData.DiagnosisState.OperatingState = data.operatingState;
                deviceData.DiagnosisState.LastErrorCode = data.lastErrorCode;
                deviceData.DiagnosisState.PowerSupplyCondition = data.powerSupplyCondition;
                deviceData.DiagnosisState.UpTime = data.upTime;
            }
        }
    }

    [System.SerializableAttribute()]
    public class CmdDeviceDiagnosisStateDataType : CmdType
    {
        [JsonPropertyName("deviceDiagnosisStateData")]
        public DeviceDiagnosisStateDataType deviceDiagnosisStateData { get; set; } = new();
    }

    [System.SerializableAttribute()]
    public class DeviceDiagnosisStateDataType
    {
        public string? timestamp { get; set; }
        public string? operatingState { get; set; }
        public string? vendorStateCode { get; set; }
        public string? lastErrorCode { get; set; }
        public string? upTime { get; set; }
        public string? totalUpTime { get; set; }
        public string? powerSupplyCondition { get; set; }
    }
}

using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net;
using EEBUS.Net.EEBUS.Models.Data;
using System.Text.Json.Serialization;

namespace EEBUS.SPINE.Commands
{
    /// <summary>
    /// deviceClassificationManufacturerData is a single-instance Function (not a list) - brand,
    /// model, serial number etc. of the remote device itself. Read on demand only (via
    /// ReadDataAsync/ReadFunctionFromRemoteAsync) - not wired into the generic per-UseCase
    /// auto-read (Connection.ReadAndSubscribe), since no UseCase's GetFeatures() currently
    /// associates a DeviceClassification feature with itself; see EebusDemo's "read" command.
    /// </summary>
    public class DeviceClassificationManufacturerData : SpineCmdPayload<CmdDeviceClassificationManufacturerDataType>
    {
        static DeviceClassificationManufacturerData()
        {
            Register("deviceClassificationManufacturerData", new Class());
        }

        public new class Class : SpineCmdPayload<CmdDeviceClassificationManufacturerDataType>.Class
        {
            public override SpineCmdPayloadBase? CreateRead(Connection connection)
            {
                return new DeviceClassificationManufacturerData();
            }

            public override async Task ReadDataAsync(Connection connection, DeviceData deviceData)
            {
                if (connection.Remote == null || deviceData.Manufacturer == null) return;

                AddressType? featureSourceAddress = connection.Local.GetFeatureAddress("DeviceClassification", false);
                AddressType? featureDestinationAddress = connection.Remote.GetFeatureAddress("DeviceClassification", true);
                if (featureSourceAddress == null || featureDestinationAddress == null) return;

                var reply = await ReadFunctionFromRemoteAsync<DeviceClassificationManufacturerData>(
                    connection, "DeviceClassification", "deviceClassificationManufacturerData");
                var data = reply?.cmd.First().deviceClassificationManufacturerData;
                if (data == null) return;

                deviceData.Manufacturer.DeviceName = data.deviceName;
                deviceData.Manufacturer.SerialNumber = data.serialNumber;
                deviceData.Manufacturer.SoftwareRevision = data.softwareRevision;
                deviceData.Manufacturer.HardwareRevision = data.hardwareRevision;
                deviceData.Manufacturer.VendorName = data.vendorName;
                deviceData.Manufacturer.BrandName = data.brandName;
                deviceData.Manufacturer.PowerSource = data.powerSource;
            }
        }
    }

    [System.SerializableAttribute()]
    public class CmdDeviceClassificationManufacturerDataType : CmdType
    {
        [JsonPropertyName("deviceClassificationManufacturerData")]
        public DeviceClassificationManufacturerDataType deviceClassificationManufacturerData { get; set; } = new();
    }

    [System.SerializableAttribute()]
    public class DeviceClassificationManufacturerDataType
    {
        public string? deviceName { get; set; }
        public string? deviceCode { get; set; }
        public string? serialNumber { get; set; }
        public string? softwareRevision { get; set; }
        public string? hardwareRevision { get; set; }
        public string? vendorName { get; set; }
        public string? vendorCode { get; set; }
        public string? brandName { get; set; }
        public string? powerSource { get; set; }
        public string? manufacturerNodeIdentification { get; set; }
        public string? manufacturerLabel { get; set; }
        public string? manufacturerDescription { get; set; }
    }
}

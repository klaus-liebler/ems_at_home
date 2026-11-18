using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net;
using EEBUS.Net.EEBUS.Models.Data;
using System;
using System.Text.Json.Serialization;

namespace EEBUS.SPINE.Commands
{
    /// <summary>
    /// electricalConnectionPermittedValueSetListData: the permitted value range(s) for a given
    /// electricalConnection/parameter (e.g. min/max charging current or power a wallbox accepts).
    /// Read on demand only, same reasoning as DeviceClassificationManufacturerData.
    /// </summary>
    public class ElectricalConnectionPermittedValueSetListData : SpineCmdPayload<CmdElectricalConnectionPermittedValueSetListDataType>
    {
        static ElectricalConnectionPermittedValueSetListData()
        {
            Register("electricalConnectionPermittedValueSetListData", new Class());
        }

        public new class Class : SpineCmdPayload<CmdElectricalConnectionPermittedValueSetListDataType>.Class
        {
            public override SpineCmdPayloadBase? CreateRead(Connection connection)
            {
                return new ElectricalConnectionPermittedValueSetListData();
            }

            public override async Task ReadDataAsync(Connection connection, DeviceData deviceData)
            {
                if (connection.Remote == null || deviceData.PermittedValueRanges == null) return;

                AddressType? featureSourceAddress = connection.Local.GetFeatureAddress("ElectricalConnection", false);
                AddressType? featureDestinationAddress = connection.Remote.GetFeatureAddress("ElectricalConnection", true);
                if (featureSourceAddress == null || featureDestinationAddress == null) return;

                var reply = await ReadFunctionFromRemoteAsync<ElectricalConnectionPermittedValueSetListData>(
                    connection, "ElectricalConnection", "electricalConnectionPermittedValueSetListData");
                var entries = reply?.cmd.First().electricalConnectionPermittedValueSetListData.electricalConnectionPermittedValueSetData;
                if (entries == null) return;

                foreach (var entry in entries)
                {
                    var range = new PermittedValueRangeData
                    {
                        ElectricalConnectionId = entry.electricalConnectionId,
                        ParameterId = entry.parameterId,
                    };

                    foreach (var set in entry.permittedValueSet ?? [])
                    {
                        foreach (var value in set.value ?? [])
                            range.Values.Add(value.number * Math.Pow(10, value.scale ?? 0));

                        foreach (var r in set.range ?? [])
                        {
                            if (r.min == null || r.max == null) continue;
                            double min = r.min.number * Math.Pow(10, r.min.scale ?? 0);
                            double max = r.max.number * Math.Pow(10, r.max.scale ?? 0);
                            range.Ranges.Add((min, max));
                        }
                    }

                    deviceData.PermittedValueRanges.Add(range);
                }
            }
        }
    }

    [System.SerializableAttribute()]
    public class CmdElectricalConnectionPermittedValueSetListDataType : CmdType
    {
        [JsonPropertyName("electricalConnectionPermittedValueSetListData")]
        public ElectricalConnectionPermittedValueSetListDataType electricalConnectionPermittedValueSetListData { get; set; } = new();
    }

    [System.SerializableAttribute()]
    public class ElectricalConnectionPermittedValueSetListDataType
    {
        public ElectricalConnectionPermittedValueSetDataType[]? electricalConnectionPermittedValueSetData { get; set; }
    }

    [System.SerializableAttribute()]
    public class ElectricalConnectionPermittedValueSetDataType
    {
        public uint? electricalConnectionId { get; set; }
        public uint? parameterId { get; set; }
        public ScaledNumberSetType[]? permittedValueSet { get; set; }
    }

    [System.SerializableAttribute()]
    public class ScaledNumberSetType
    {
        public ScaledNumberType[]? value { get; set; }
        public ScaledNumberRangeType[]? range { get; set; }
    }

    [System.SerializableAttribute()]
    public class ScaledNumberRangeType
    {
        public ScaledNumberType? min { get; set; }
        public ScaledNumberType? max { get; set; }
    }
}

using EEBUS.Features;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net;
using System.Text.Json.Serialization;

namespace EEBUS.SPINE.Commands
{
    public class ElectricalConnectionParameterDescriptionListData : SpineCmdPayload<CmdElectricalConnectionParameterDescriptionListDataType>
    {
        static ElectricalConnectionParameterDescriptionListData()
        {
            Register("electricalConnectionParameterDescriptionListData", new Class());
        }

        static public ulong counter = 1;

        public new class Class : SpineCmdPayload<CmdElectricalConnectionParameterDescriptionListDataType>.Class
        {
            public override SpineCmdPayloadBase? CreateRead(Connection connection)
            {
                return new ElectricalConnectionParameterDescriptionListData();
            }

            public override async ValueTask<SpineCmdPayloadBase?> CreateAnswerAsync(DatagramType datagram, HeaderType header, Connection connection)
            {
                if (datagram.header.cmdClassifier == "read")
                {
                    AddressType? address = connection.Local.GetFeatureAddress("Measurement", true);
                    if (address == null) return null;

                    Entity? entity = connection.Local.Entities.FirstOrDefault(e => e.Index.SequenceEqual(address.entity));
                    MeasurementServerFeature? measurementFeature = entity?.Features.FirstOrDefault(f => f.Index == address.feature) as MeasurementServerFeature;
                    if (measurementFeature == null) return null;

                    ElectricalConnectionParameterDescriptionListData electricalConnectionParameterDescriptionListData = new ElectricalConnectionParameterDescriptionListData();
                    List<ElectricalConnectionParameterDescriptionDataType> descriptionData = new();
                    foreach (var data in measurementFeature.measurementData)
                    {
                        if (data.electricalConnectionParameterDescriptionData != null)
                        {
                            descriptionData.Add(data.electricalConnectionParameterDescriptionData);
                        }
                    }
                    electricalConnectionParameterDescriptionListData.cmd[0].electricalConnectionParameterDescriptionListData = new()
                    {
                        electricalConnectionParameterDescriptionData = descriptionData.ToArray()
                    };
                    return electricalConnectionParameterDescriptionListData;
                }
                else if (datagram.header.cmdClassifier == "write")
                {
                    return new ResultData();
                }
                else
                {
                    return null;
                }
            }

            public override async ValueTask EvaluateAsync(Connection connection, DatagramType datagram)
            {
                if (datagram.header.cmdClassifier != "reply" && datagram.header.cmdClassifier != "notify")
                    return;

                ElectricalConnectionParameterDescriptionListData? command = datagram.payload == null
                    ? null
                    : JsonHelper.FromJsonNode<ElectricalConnectionParameterDescriptionListData>(datagram.payload);
                if (command == null || command.cmd == null || command.cmd.Length == 0)
                    return;

                // This reply comes FROM the peer's ElectricalConnection feature (addressSource.feature
                // is that feature's own index, e.g. 9), but the per-channel phase/voltage-type info it
                // carries describes measurement channels that live on the peer's *Measurement* feature
                // (e.g. index 8) - a different feature entirely. The previous lookup matched
                // addressSource.feature directly against MeasurementServerFeature, which can only work
                // by coincidence (that index is never the Measurement feature's own) and in practice
                // always returned null, silently dropping every phase/voltage-type update. Find the
                // entity's Measurement feature by type instead.
                Entity? entity = connection.Remote?.Entities.FirstOrDefault(e => e.Index.SequenceEqual(datagram.header.addressSource.entity));
                MeasurementServerFeature? measurementFeature = entity?.Features.OfType<MeasurementServerFeature>().FirstOrDefault();
                if (measurementFeature == null) return;

                foreach (ElectricalConnectionParameterDescriptionDataType parameterDescription in command.cmd.First().electricalConnectionParameterDescriptionListData?.electricalConnectionParameterDescriptionData ?? [])
                {
                    MeasurementData.MeasurementData? corresponding = measurementFeature.measurementData.FirstOrDefault(data => data.measurementId == parameterDescription.measurementId);
                    if (corresponding == null)
                    {
                        measurementFeature.measurementData.Add(new MeasurementData.MeasurementData
                        {
                            measurementId = parameterDescription.measurementId,
                            electricalConnectionParameterDescriptionData = parameterDescription
                        });
                    }
                    else
                    {
                        corresponding.electricalConnectionParameterDescriptionData = parameterDescription;
                    }
                }
            }
        }
    }

    [System.SerializableAttribute()]
    public class CmdElectricalConnectionParameterDescriptionListDataType : CmdType
    {
        public ElectricalConnectionParameterDescriptionListDataType? electricalConnectionParameterDescriptionListData { get; set; } = new();
    }

    [System.SerializableAttribute()]
    public class ElectricalConnectionParameterDescriptionListDataType
    {
        public ElectricalConnectionParameterDescriptionDataType[]? electricalConnectionParameterDescriptionData { get; set; }
    }

    [System.SerializableAttribute()]
    public class ElectricalConnectionParameterDescriptionDataType
    {
        public uint electricalConnectionId { get; set; }
        public uint parameterId { get; set; }
        public uint measurementId { get; set; }
        public string voltageType { get; set; }

        // previously had [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? acMeasuredPhases { get; set; }

        public string? acMeasuredInReferenceTo { get; set; }

        public string? acMeasurementType { get; set; }

        public string? acMeasurementVariant { get; set; }
    }
}
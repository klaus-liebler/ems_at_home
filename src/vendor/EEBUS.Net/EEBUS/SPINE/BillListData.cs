using EEBUS.Features;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net;
using System.Text.Json.Serialization;

namespace EEBUS.SPINE.Commands
{
    public class BillListData : SpineCmdPayload<CmdBillListDataType>
    {
        static BillListData()
        {
            Register("billListData", new Class());
        }

        public new class Class : SpineCmdPayload<CmdBillListDataType>.Class
        {
            public override SpineCmdPayloadBase? CreateRead(Connection connection)
            {
                return new BillListData();
            }

            public override async ValueTask EvaluateAsync(Connection connection, DatagramType datagram)
            {
                if (datagram.header.cmdClassifier != "reply" && datagram.header.cmdClassifier != "notify")
                    return;

                BillListData? command = datagram.payload == null
                    ? null
                    : JsonHelper.FromJsonNode<BillListData>(datagram.payload);
                if (command == null || command.cmd == null || command.cmd.Length == 0)
                    return;

                Entity? entity = connection.Remote?.Entities.FirstOrDefault(e => e.Index.SequenceEqual(datagram.header.addressSource.entity));
                BillServerFeature? billFeature = entity?.Features.FirstOrDefault(f => f.Index == datagram.header.addressSource.feature) as BillServerFeature;
                if (billFeature == null) return;

                billFeature.bills = (command.cmd.First().billListData.billData ?? []).ToList();
            }
        }
    }

    [System.SerializableAttribute()]
    public class CmdBillListDataType : CmdType
    {
        [JsonPropertyName("billListData")]
        public BillListDataType billListData { get; set; } = new();
    }

    [System.SerializableAttribute()]
    public class BillListDataType
    {
        public BillDataType[]? billData { get; set; }
    }

    [System.SerializableAttribute()]
    public class BillDataType
    {
        public uint? billId { get; set; }
        public string? billType { get; set; }
        public string? scopeType { get; set; }
        public BillTotalType? total { get; set; }
        public BillPositionDataType[]? position { get; set; }
    }

    [System.SerializableAttribute()]
    public class BillTotalType
    {
        public TimePeriodType? timePeriod { get; set; }
        public BillValueType[]? value { get; set; }
        public BillCostType[]? cost { get; set; }
    }

    [System.SerializableAttribute()]
    public class BillPositionDataType
    {
        public uint? positionId { get; set; }
        public string? positionType { get; set; }
        public BillValueType[]? value { get; set; }
        public BillCostType[]? cost { get; set; }
    }

    [System.SerializableAttribute()]
    public class BillValueType
    {
        public uint? valueId { get; set; }
        public string? unit { get; set; }
        public ScaledNumberType? value { get; set; }
        public ScaledNumberType? valuePercentage { get; set; }
    }

    [System.SerializableAttribute()]
    public class BillCostType
    {
        public uint? costId { get; set; }
        public string? costType { get; set; }
        public uint? valueId { get; set; }
        public string? unit { get; set; }
        public string? currency { get; set; }
        public ScaledNumberType? cost { get; set; }
        public ScaledNumberType? costPercentage { get; set; }
    }
}

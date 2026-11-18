using EEBUS.Features;
using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net;
using System.Text.Json.Serialization;

namespace EEBUS.SPINE.Commands
{
    public class BillDescriptionListData : SpineCmdPayload<CmdBillDescriptionListDataType>
    {
        static BillDescriptionListData()
        {
            Register("billDescriptionListData", new Class());
        }

        public new class Class : SpineCmdPayload<CmdBillDescriptionListDataType>.Class
        {
            public override SpineCmdPayloadBase? CreateRead(Connection connection)
            {
                return new BillDescriptionListData();
            }

            public override async ValueTask EvaluateAsync(Connection connection, DatagramType datagram)
            {
                if (datagram.header.cmdClassifier != "reply" && datagram.header.cmdClassifier != "notify")
                    return;

                BillDescriptionListData? command = datagram.payload == null
                    ? null
                    : JsonHelper.FromJsonNode<BillDescriptionListData>(datagram.payload);
                if (command == null || command.cmd == null || command.cmd.Length == 0)
                    return;

                Entity? entity = connection.Remote?.Entities.FirstOrDefault(e => e.Index.SequenceEqual(datagram.header.addressSource.entity));
                BillServerFeature? billFeature = entity?.Features.FirstOrDefault(f => f.Index == datagram.header.addressSource.feature) as BillServerFeature;
                if (billFeature == null) return;

                billFeature.descriptions = (command.cmd.First().billDescriptionListData.billDescriptionData ?? []).ToList();
            }
        }
    }

    [System.SerializableAttribute()]
    public class CmdBillDescriptionListDataType : CmdType
    {
        [JsonPropertyName("billDescriptionListData")]
        public BillDescriptionListDataType billDescriptionListData { get; set; } = new();
    }

    [System.SerializableAttribute()]
    public class BillDescriptionListDataType
    {
        public BillDescriptionDataType[]? billDescriptionData { get; set; }
    }

    [System.SerializableAttribute()]
    public class BillDescriptionDataType
    {
        public uint? billId { get; set; }
        public bool? billWriteable { get; set; }
        public bool? updateRequired { get; set; }
        public string[]? supportedBillType { get; set; }
        public string? sessionId { get; set; }
    }
}

using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net;
using EEBUS.Net.EEBUS.Data.DataStructures;
using EEBUS.Net.EEBUS.Models.Data;
using System.Text.Json.Serialization;

namespace EEBUS.SPINE.Commands
{
	public class ElectricalConnectionDescriptionListData : SpineCmdPayload<CmdElectricalConnectionDescriptionListDataType>
	{
		static ElectricalConnectionDescriptionListData()
		{
			Register( "electricalConnectionDescriptionListData", new Class() );
		}

		static public ulong counter = 1;

		public new class Class : SpineCmdPayload<CmdElectricalConnectionDescriptionListDataType>.Class
		{
            public override SpineCmdPayloadBase? CreateRead(Connection connection)
            {
				return new ElectricalConnectionDescriptionListData();
            }

			public override async ValueTask<SpineCmdPayloadBase?> CreateAnswerAsync( DatagramType datagram, HeaderType header, Connection connection )
			{
				if ( datagram.header.cmdClassifier == "read")
				{
					ElectricalConnectionDescriptionListData payload = new ElectricalConnectionDescriptionListData();
					payload.cmd = [new()];
					ElectricalConnectionDescriptionListDataType data = payload.cmd[0].electricalConnectionDescriptionListData = new();
					data.electricalConnectionDescriptionData = [new()];

					data.electricalConnectionDescriptionData[0].electricalConnectionId	= 0;    // ToDo: source??
					data.electricalConnectionDescriptionData[0].powerSupplyType			= "ac";
					data.electricalConnectionDescriptionData[0].acConnectedPhases		= 3;
					data.electricalConnectionDescriptionData[0].positiveEnergyDirection	= "consume";

					return payload;
				}
				else if ( datagram.header.cmdClassifier == "write" )
				{
					return new ResultData();
				}
				else
				{
					return null;
				}
			}

            public override async Task ReadDataAsync(Connection connection, DeviceData deviceData)
            {
                if (connection.Remote == null || deviceData.ElectricalConnectionInfo == null) return;

                AddressType? featureSourceAddress = connection.Local.GetFeatureAddress("ElectricalConnection", false);
                AddressType? featureDestinationAddress = connection.Remote.GetFeatureAddress("ElectricalConnection", true);
                if (featureSourceAddress == null || featureDestinationAddress == null) return;

                var reply = await ReadFunctionFromRemoteAsync<ElectricalConnectionDescriptionListData>(
                    connection, "ElectricalConnection", "electricalConnectionDescriptionListData");
                var data = reply?.cmd.First().electricalConnectionDescriptionListData.electricalConnectionDescriptionData?.FirstOrDefault();
                if (data == null) return;

                deviceData.ElectricalConnectionInfo.PowerSupplyType = data.powerSupplyType;
                deviceData.ElectricalConnectionInfo.AcConnectedPhases = data.acConnectedPhases;
                deviceData.ElectricalConnectionInfo.PositiveEnergyDirection = data.positiveEnergyDirection;
            }
        }
	}

	[System.SerializableAttribute()]
	public class CmdElectricalConnectionDescriptionListDataType : CmdType
	{
		public ElectricalConnectionDescriptionListDataType electricalConnectionDescriptionListData { get; set; } = new();
	}

	[System.SerializableAttribute()]
	public class ElectricalConnectionDescriptionListDataType
	{
		[JsonPropertyName("electricalConnectionDescriptionData")]
		public ElectricalConnectionDescriptionDataType[]? electricalConnectionDescriptionData { get; set; }
	}

	[System.SerializableAttribute()]
	public class ElectricalConnectionDescriptionDataType
	{
		public uint?   electricalConnectionId  { get; set; }
		
		public string? powerSupplyType		  { get; set; }
		
		public uint?   acConnectedPhases		  { get; set; }
		
		public string? positiveEnergyDirection { get; set; }
	}
}

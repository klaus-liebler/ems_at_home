using EEBUS.Models;
using EEBUS.SPINE.Commands;

namespace EEBUS.Features
{
    /// <summary>
    /// Our own local side of the "EV Charging Summary" Use Case (Actor CEM). Only exists so
    /// Connection.ReadAndSubscribe/ReadFunctionFromRemoteAsync can resolve a local "Bill" client
    /// address to read a remote's BillServerFeature - holds no data itself.
    /// </summary>
    public class BillClientFeature : Feature
    {
        static BillClientFeature()
        {
            Register("Bill-client", new Class());
        }

        public BillClientFeature(Entity owner)
            : base("Bill", "client", owner)
        {
        }

        public BillClientFeature(int index, Entity owner, FeatureInformationType featureInfo)
            : base(index, "Bill", "client", owner, featureInfo)
        {
        }

        public new class Class : Feature.Class
        {
            public override Feature Create(Entity owner)
            {
                return new BillClientFeature(owner);
            }

            public override Feature Create(int index, Entity owner, FeatureInformationType featureInfo)
            {
                return new BillClientFeature(index, owner, featureInfo);
            }
        }

        public override string Description
        {
            get
            {
                return "Bill Client";
            }
        }
    }
}

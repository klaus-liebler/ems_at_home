using EEBUS.Models;
using EEBUS.SPINE.Commands;

namespace EEBUS.Features
{
    /// <summary>
    /// Models a remote's Bill feature (role=server), used by the "EV Charging Summary" (EVCS) Use
    /// Case. Per that Use Case's spec, billListData is nominally written by an external "Energy
    /// Broker" - but a device MAY still self-populate it from its own charging history and answer
    /// reads with real data. descriptions/bills below hold only whatever the remote actually replied
    /// with (via BillDescriptionListData/BillListData.EvaluateAsync) - never seeded/fabricated.
    /// </summary>
    public class BillServerFeature : Feature
    {
        static BillServerFeature()
        {
            Register("Bill-server", new Class());
        }

        public BillServerFeature(Entity owner)
            : base("Bill", "server", owner)
        {
            this.Functions.Add(new Function("billDescriptionListData", true, false));
            this.Functions.Add(new Function("billListData", true, false));
        }

        public BillServerFeature(int index, Entity owner, FeatureInformationType featureInfo)
            : base(index, "Bill", "server", owner, featureInfo)
        {
        }

        public new class Class : Feature.Class
        {
            public override Feature Create(Entity owner)
            {
                return new BillServerFeature(owner);
            }

            public override Feature Create(int index, Entity owner, FeatureInformationType featureInfo)
            {
                return new BillServerFeature(index, owner, featureInfo);
            }
        }

        public override string Description
        {
            get
            {
                return "Bill Server";
            }
        }

        public List<BillDescriptionDataType> descriptions = new();
        public List<BillDataType> bills = new();
    }
}

using EEBUS.Features;
using EEBUS.Models;
using EEBUS.SPINE.Commands;

namespace EEBUS.UseCases.CEM
{
    /// <summary>
    /// "EV Charging Summary" (EEBus_UC_TS_EVChargingSummary_V1.0.1.pdf), Actor CEM: our own local
    /// side (Energy Guard and Energy Broker are the same CEM entity here, so per the Use Case's
    /// "Use Case Discovery rules" the actor string is "CEM", not "EnergyBroker"). Only declares the
    /// client-role Bill feature so we can address and read a remote EVSE's Bill server - we don't
    /// write summaries back (see EvChargingSummary-EVSE for the read side).
    /// </summary>
    public class EvChargingSummary : UseCase
    {
        static EvChargingSummary()
        {
            Register("evChargingSummary-CEM", new Class());
        }

        public EvChargingSummary(UseCaseSettings usecaseSettings, Entity entity)
            : base(usecaseSettings, entity)
        {
        }

        protected override IEnumerable<Feature?> GetFeatures(Entity entity)
        {
            return [
                Feature.Create("Bill", "client", entity)
            ];
        }

        protected override List<Scenario> GetScenarios()
        {
            return [
                new Scenario(1, true, "Energy Broker sends Charging Session Summary to EVSE"),
            ];
        }

        public new class Class : UseCase.Class
        {
            public override UseCase Create(UseCaseSettings usecaseSettings, Entity entity)
            {
                return new EvChargingSummary(usecaseSettings, entity);
            }
        }

        public override string Actor { get { return "CEM"; } }

        public override UseCaseSupportType Information
        {
            get
            {
                List<uint> scenarios = new();
                foreach (var scenario in Scenarios)
                    scenarios.Add(scenario.Index);

                UseCaseSupportType support = new();
                support.useCaseName = "evChargingSummary";
                support.useCaseVersion = "1.0.1";
                support.useCaseAvailable = true;
                support.scenarioSupport = scenarios.ToArray();
                support.useCaseDocumentSubRevision = "release";

                return support;
            }
        }
    }
}

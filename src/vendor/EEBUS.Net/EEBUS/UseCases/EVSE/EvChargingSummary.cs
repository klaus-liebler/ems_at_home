using EEBUS.Features;
using EEBUS.Models;
using EEBUS.SPINE.Commands;

namespace EEBUS.UseCases.EVSE
{
    /// <summary>
    /// "EV Charging Summary" (EEBus_UC_TS_EVChargingSummary_V1.0.1.pdf), Actor EVSE: models a
    /// remote wallbox's Bill feature (role=server), holding whatever charging-summary data it
    /// actually reports (billDescriptionListData/billListData). Per the Use Case spec this data is
    /// nominally written by an external "Energy Broker", but a device may self-populate it from its
    /// own charging history - only a real read/reply proves which is the case for a given remote.
    /// </summary>
    public class EvChargingSummary : UseCase
    {
        static EvChargingSummary()
        {
            Register("evChargingSummary-EVSE", new Class());
        }

        public EvChargingSummary(UseCaseSettings usecaseSettings, Entity entity)
            : base(usecaseSettings, entity)
        {
        }

        protected override IEnumerable<Feature?> GetFeatures(Entity entity)
        {
            return [
                Feature.Create("Bill", "server", entity)
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

        public override string Actor { get { return "EVSE"; } }

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

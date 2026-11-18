using EEBUS.Models;
using EEBUS.SPINE.Commands;

namespace EEBUS.Features
{
    /// <summary>
    /// Our own local side, so Connection.ReadAndSubscribe/ReadFunctionFromRemoteAsync can resolve a
    /// local "DeviceClassification" client address to read a remote's manufacturer data. Holds no
    /// data itself - see EEBUS.UseCases.MonitoringAppliance.MonitoringOfPowerConsumption, which adds
    /// this feature to its GetFeatures() for exactly this reason.
    /// </summary>
    public class DeviceClassificationClientFeature : Feature
    {
        static DeviceClassificationClientFeature()
        {
            Register("DeviceClassification-client", new Class());
        }

        public DeviceClassificationClientFeature(Entity owner)
            : base("DeviceClassification", "client", owner)
        {
        }

        public DeviceClassificationClientFeature(int index, Entity owner, FeatureInformationType featureInfo)
            : base(index, "DeviceClassification", "client", owner, featureInfo)
        {
        }

        public new class Class : Feature.Class
        {
            public override Feature Create(Entity owner)
            {
                return new DeviceClassificationClientFeature(owner);
            }

            public override Feature Create(int index, Entity owner, FeatureInformationType featureInfo)
            {
                return new DeviceClassificationClientFeature(index, owner, featureInfo);
            }
        }

        public override string Description
        {
            get
            {
                return "DeviceClassification Client";
            }
        }
    }
}

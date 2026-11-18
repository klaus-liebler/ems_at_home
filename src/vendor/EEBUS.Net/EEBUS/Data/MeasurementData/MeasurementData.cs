using EEBUS.Net.Extensions;
using EEBUS.SPINE.Commands;

namespace EEBUS.MeasurementData
{
    public class MeasurementData
    {
        public uint measurementId { get; set; }
        public ElectricalConnectionParameterDescriptionDataType? electricalConnectionParameterDescriptionData { get; set; }
        public MeasurementDataType? measurementDataType { get; set; }
        public MeasurementDescriptionDataType? measurementDescriptionDataType { get; set; }

        /// <summary>
        /// True once a real reply/notify from the remote has actually described this channel
        /// (MeasurementDescriptionListData.EvaluateAsync). EEBUS.UseCases.MonitoringAppliance/
        /// MonitoredUnit's MonitoringOfPowerConsumption use case constructors unconditionally seed 10
        /// hardcoded placeholder channels (guessed type/unit/scope/phase) into this same list before
        /// any wire data arrives - a remote that only ever reports a subset of channels (some devices
        /// only expose one) leaves the rest permanently holding those fabricated placeholders with no
        /// way to tell them apart from real ones without this flag. Consumers that display this data
        /// (e.g. EmsAtHome.EebusCli) should filter on this rather than trust every entry present.
        /// </summary>
        public bool Confirmed { get; set; }

        public float? MeasuredValue
        {
            get
            {
                long? number = measurementDataType?.value?.number;
                short? scale = measurementDataType?.value?.scale;

                if (number == null) return null;
                scale ??= 0;
                if (scale == 0) return number;

                return (float)(number * Math.Pow(10, (double)scale));
            }
        }

        public void UpdateValue(float? value)
        {
            if (value != null && measurementDataType != null)
            {
                measurementDataType.value = value.Value.ToScaledNumber();
            }
        }
    }
}

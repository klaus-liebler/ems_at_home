using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.EEBUS.Models.Data
{
    public class DeviceData
    {
        public LpcLppData? Lpc { get; set; }
        public LpcLppData? Lpp { get; set; }
        public FailSafeData? FailSafe { get; set; }
        public MgcpData? Mgcp { get; set; }
        //public MpcData? Mpc { get; set; }
        public MeasurementsData? Measurements { get; set; }
        public UseCaseSupportData? UseCaseSupport { get; set; }
        public ManufacturerData? Manufacturer { get; set; }
        public DiagnosisStateData? DiagnosisState { get; set; }
        public List<PermittedValueRangeData>? PermittedValueRanges { get; set; }
        public ElectricalConnectionInfoData? ElectricalConnectionInfo { get; set; }
        public string SKI { get; set; } = string.Empty;
        //public string ShipId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ShipId { get; set; } = string.Empty;
    }
}

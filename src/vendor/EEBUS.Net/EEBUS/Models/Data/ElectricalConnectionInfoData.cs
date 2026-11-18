namespace EEBUS.Net.EEBUS.Models.Data
{
    public class ElectricalConnectionInfoData
    {
        public string? PowerSupplyType { get; set; }
        public uint? AcConnectedPhases { get; set; }
        public string? PositiveEnergyDirection { get; set; }
    }
}

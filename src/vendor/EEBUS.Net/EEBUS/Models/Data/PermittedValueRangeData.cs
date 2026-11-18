using System.Collections.Generic;

namespace EEBUS.Net.EEBUS.Models.Data
{
    public class PermittedValueRangeData
    {
        public uint? ElectricalConnectionId { get; set; }
        public uint? ParameterId { get; set; }
        public List<double> Values { get; set; } = new();
        public List<(double Min, double Max)> Ranges { get; set; } = new();
    }
}

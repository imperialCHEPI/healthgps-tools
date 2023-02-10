using System.Diagnostics;

namespace HealthGps.R
{
    [DebuggerDisplay("{Value} SE = {StdError} T = {TValue}, P = {PValue}")]
    public class Coefficient
    {
        public double Value { get; set; }

        public double StdError { get; set; }

        public double TValue { get; set; }

        public double PValue { get; set; }
    }
}

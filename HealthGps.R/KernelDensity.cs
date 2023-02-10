using System.Collections.Generic;

namespace HealthGps.R
{
    public struct KernelDensity
    {
        public IReadOnlyCollection<double> X { get; set; }

        public IReadOnlyCollection<double> Y { get; set; }

        public double Bandwidth { get; set; }

        public int N { get; set; }
    }
}

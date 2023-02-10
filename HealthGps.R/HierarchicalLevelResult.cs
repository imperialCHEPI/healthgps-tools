using System.Collections.Generic;

namespace HealthGps.R
{
    public class HierarchicalLevelResult
    {
        public List<string> Variables { get; set; }

        public Array2D<double> S { get; set; }

        public Array2D<double> W { get; set; }

        public Array2D<double> M { get; set; }

        public IReadOnlyList<double> Variances { get; set; }

        public Array2D<double> Correlation { get; set; }
    }
}

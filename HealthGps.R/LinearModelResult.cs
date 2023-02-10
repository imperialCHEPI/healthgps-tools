using System.Collections.Generic;

namespace HealthGps.R
{
    public class LinearModelResult
    {
        public string Formula { get; set; }

        public IReadOnlyDictionary<string, Coefficient> Coefficients { get; set; }

        public IReadOnlyList<double> Residuals { get; set; }

        public IReadOnlyList<double> FittedValues { get; set; }

        public double ResidualsStandardDeviation { get; set; }

        public double RSquared { get; set; }
    }
}

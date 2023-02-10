using System.Collections.Generic;
using Microsoft.Data.Analysis;

namespace HealthGps.ModelFit
{
    public sealed class RiskFactorModelDefinition
    {
        public RiskFactorModelDefinition(
            DataFrame dataset,
            IReadOnlyDictionary<string, int> factors,
            IReadOnlyDictionary<string, RiskFactorModelType> modelTypes,
            string dynamicFactor)
        {
            Dataset = dataset;
            Factors = factors;
            ModelTypes = modelTypes;
            DynamicFactor = dynamicFactor;
        }

        public DataFrame Dataset { get; }

        public string DynamicFactor { get; }

        public IReadOnlyDictionary<string, int> Factors { get; }

        public IReadOnlyDictionary<string, RiskFactorModelType> ModelTypes { get; }
    }
}

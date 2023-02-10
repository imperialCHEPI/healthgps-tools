using System.Collections.Generic;

using HealthGps.R;

namespace HealthGps.ModelFit
{
    public class RiskFactorModel
    {
        public RiskFactorModel(RiskFactorModelDefinition definition, IReadOnlyDictionary<string, HierarchicalModelResult> models)
        {
            Definition = definition;
            Models = models;
        }

        public RiskFactorModelDefinition Definition { get; }

        public IReadOnlyDictionary<string, HierarchicalModelResult> Models { get; }
    }
}
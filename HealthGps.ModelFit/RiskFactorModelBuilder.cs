using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Analysis;

using HealthGps.R;

namespace HealthGps.ModelFit
{
    public sealed class RiskFactorModelBuilder
    {
        private readonly IStatsProvider provider;
        private readonly RiskFactorModelDefinition definition;

        public RiskFactorModelBuilder(IStatsProvider statsProvider, RiskFactorModelDefinition modelInfo)
        {
            provider = statsProvider;
            definition = modelInfo;
        }

        public RiskFactorModel Build(IProgress<double> progress = null)
        {
            var models = new Dictionary<string, HierarchicalModelResult>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in definition.ModelTypes)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Creating model type: {item.Key}:");
                Console.ResetColor();
                models.Add(item.Key, BuildHierarchy(item.Value.IncludeDynamicFactor, progress));
            }

            return new RiskFactorModel(definition, models);
        }

        private HierarchicalModelResult BuildHierarchy(bool includeDynamicFactor, IProgress<double> progress = null)
        {
            Dictionary<string, int> modelFactors;
            if (includeDynamicFactor)
            {
                modelFactors = new Dictionary<string, int>();
                foreach (var pair in definition.Factors)
                {
                    modelFactors.Add(pair.Key, pair.Value);
                }
            }
            else
            {
                modelFactors = definition.Factors
                    .Where(s => !s.Key.Equals(definition.DynamicFactor, StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(p => p.Key, p => p.Value);
            }

            var data = new Dictionary<string, List<double>>(modelFactors.Count);
            foreach (var item in modelFactors)
            {
                var col = definition.Dataset.Columns[item.Key] as DoubleDataFrameColumn;
                data.Add(item.Key, CreateNaNFilledWithMean(col));
            }

            return provider.FitHierarchicalModel(modelFactors, data, progress);
        }

        private static List<double> CreateNaNFilledWithMean(IEnumerable<double?> values)
        {
            var fulldata = values.Where(s => s.HasValue);
            var mean = fulldata.Average().Value;

            var result = new List<double>(values.Count());
            foreach (var v in values)
            {
                result.Add(v ?? mean);
            }

            return result;
        }
    }
}

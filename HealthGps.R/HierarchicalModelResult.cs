using System.Collections.Generic;

namespace HealthGps.R
{
    public sealed class HierarchicalModelResult
    {
        public IReadOnlyDictionary<string, LinearModelResult> Models { get; set; }

        public IReadOnlyDictionary<int, HierarchicalLevelResult> Levels { get; set; }
    }
}


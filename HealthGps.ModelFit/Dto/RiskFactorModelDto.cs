using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HealthGps.ModelFit.Dto
{
    internal class RiskFactorModelDto
    {
        public DatasetDto Dataset { get; set; }

        public ModellingDto Modelling { get; set; }
    }

    internal class DatasetDto
    {
        public string Filename { get; set; }

        public string Format { get; set; }

        public string Delimiter { get; set; }
    }

    internal class ModellingDto
    {
        [JsonPropertyName("risk_factors")]
        public Dictionary<string, int> RiskFactors { get; set; }

        [JsonPropertyName("dynamic_risk_factor")]
        public string DynamicRiskFactor { get; set; }

        public Dictionary<string, ModelDto> Models { get; set; }
    }

    internal class ModelDto
    {
        public string Filename { get; set; }

        [JsonPropertyName("include_dynamic_factor")]
        public bool IncludeDynamicRiskFactor { get; set; }
    }
}

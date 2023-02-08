using System.Collections.Generic;

namespace HealthGPS.Tools.Dto;

public struct IntervalDto
{
    public int Start { get; set; }
    public int Finish { get; set; }

    public override string ToString()
    {
        return $"{Start} => {Finish}";
    }
}

public class DatasetOptionsDto
{
    public bool IsActive { get; set; }

    public string OutputFilename { get; set; }

    public List<string> FactorsFilter { get; set; }
}

public class RuntimeLogsDto
{
    public bool IsActive { get; set; }

    public string SourceSubFolder { get; set; }

    public string OutputFilename { get; set; }
}

public class OutputToolOptionsDto
{
    public IntervalDto Age { get; set; }

    public IntervalDto Run { get; set; }

    public IntervalDto Year { get; set; }

    public string Delimiter { get; set; }

    public string OutputFilename { get; set; }

    public int? MaxResultFiles { get; set; }

    public DatasetOptionsDto BaselineDataset { get; set; }

    public RuntimeLogsDto RuntimeLogs { get; set; }

    public DatasetOptionsDto HCEDataset { get; set; }
}

using System.Collections.Generic;
using System.IO;

namespace HealthGPS.Tools;

public class DatasetOptions
{
    public bool IsActive { get; set; }

    public string OutputFilename { get; set; }

    public List<string> FactorsFilter { get; set; }
}

public class RuntimeLogs
{
    public bool IsActive { get; set; }

    public string SourceSubFolder { get; set; }

    public string OutputFilename { get; set; }
}

public class OutputToolOptions
{
    public Interval Age { get; set; }

    public Interval Run { get; set; }

    public Interval Year { get; set; }

    public MeasureType Measure { get; set; }

    public string Delimiter { get; set; }

    public string OutputFilename { get; set; }

    public int? MaxResultFiles { get; set; }

    public DatasetOptions BaselineDataset { get; set; }

    public RuntimeLogs RuntimeLogs { get; set; }

    public DatasetOptions HCEDataset { get; set; }

    public DirectoryInfo SourceFolder { get; set; }

    public DirectoryInfo OutputFolder { get; set; }

    public string CreateMeanFilename(MeasureType measureType, int totalRuns)
    {
        return $"{measureType}_time_{Year.Start}_{Year.Finish}_run_{Run.Start}_{totalRuns}_age_{Age.Start}_{Age.Finish}_Mean.csv";
    }

    public string CreateStDevFilename(MeasureType measureType, int totalRuns)
    {
        return $"{measureType}_time_{Year.Start}_{Year.Finish}_run_{Run.Start}_{totalRuns}_age_{Age.Start}_{Age.Finish}_StDev.csv";
    }
}

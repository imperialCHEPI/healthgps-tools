using System;

namespace HealthGPS.Tools.Output;

public readonly record struct RuntimeLog
{
    public int JobId { get; init; }

    public string Source { get; init; }

    public int Run { get; init; }

    public DateTime TimeOfDay { get; init; }

    public double ElapsedMs { get; init; }

    public override string ToString()
    {
        return $"{JobId},{Source},{Run},{TimeOfDay:O},{ElapsedMs:R}";
    }

    public static string CsvHeader()
    {
        return "JobId,Source,Run,TimeOfDay,ElapsedMs,ElapsedMin,ElapsedHour";
    }
}
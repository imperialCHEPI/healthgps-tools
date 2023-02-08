using System.Collections.Generic;

namespace HealthGPS.Tools.Output;

public interface IMeasureAccumulator
{
    MeasureType Measure { get; }

    Gender Gender { get; }

    int Count { get; }

    int TimeStart { get; }

    int TimeFinish { get; }

    bool IsAddAllowed { get; }

    void Add(string factor, int year, double baseSum, double baseCount, double policySum, double policyCount);

    AccumulatorValue this[string factor, int year] { get; set; }

    bool Contains(string factor, int year);

    IReadOnlyCollection<string> Factors { get; }

    void CalculateAverages(int numberOfRuns);

    void Reset();
}

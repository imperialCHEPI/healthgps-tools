using System;
using System.Collections.Generic;

namespace HealthGPS.Tools.Output;

public abstract class MeasureAccumulatorBase : IMeasureAccumulator
{
    private readonly HashSet<string> factors;
    private readonly Dictionary<string, Dictionary<int, AccumulatorValue>> values;
    private bool allowAddOperation;

    public MeasureAccumulatorBase(MeasureType measure, Gender gender, int timeStart, int timeFinish)
    {
        if (timeStart < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeStart), "Start time must not be less than zero.");
        }

        if (timeFinish < timeStart)
        {
            throw new ArgumentOutOfRangeException(nameof(timeFinish), "Start finish value must be greater than time start value.");
        }

        factors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        values = new Dictionary<string, Dictionary<int, AccumulatorValue>>(StringComparer.OrdinalIgnoreCase);

        Measure = measure;
        Gender = gender;
        TimeStart = timeStart;
        TimeFinish = timeFinish;
        allowAddOperation = true;
    }

    public AccumulatorValue this[string factor, int year]
    {
        get => values[factor][year];
        set => values[factor][year] = value;
    }

    public MeasureType Measure { get; }

    public Gender Gender { get; }

    public int Count => values.Count;

    public IReadOnlyCollection<string> Factors => factors;

    public int TimeStart { get; }

    public int TimeFinish { get; }

    public bool IsAddAllowed => allowAddOperation;

    public void Add(string factor, int year, double baseSum, double baseCount, double policySum, double policyCount)
    {
        if (!allowAddOperation)
        {
            throw new InvalidOperationException("Add new values to this accumulator is not permitted.");
        }

        if (factors.Add(factor))
        {
            values.Add(factor, new Dictionary<int, AccumulatorValue>());
            for (int y = TimeStart; y <= TimeFinish; y++)
            {
                values[factor].Add(y, new AccumulatorValue());
            }
        }

        AddInternal(factor, year, baseSum, baseCount, policySum, policyCount);
    }

    public void CalculateAverages(int numberOfRuns)
    {
        if (factors.Contains("deaths"))
        {
            values["deaths"][TimeStart].Mean = values["deaths"][TimeStart + 1].Mean;
            values["deaths"][TimeStart].StDev = values["deaths"][TimeStart + 1].StDev;
        }

        foreach (string factor in factors)
        {
            for (int y = TimeStart; y <= TimeFinish; y++)
            {
                values[factor][y].Mean /= numberOfRuns;

                var meanSquare = values[factor][y].Mean * values[factor][y].Mean;
                values[factor][y].StDev = Math.Sqrt(values[factor][y].StDev / numberOfRuns - meanSquare);
            }
        }

        allowAddOperation = false;
    }

    public bool Contains(string factor, int year)
    {
        if (factors.Contains(factor))
        {
            return values[factor].ContainsKey(year);
        }

        return false;
    }

    public void Reset()
    {
        factors.Clear();
        values.Clear();
        allowAddOperation = true;
    }

    protected abstract void AddInternal(string factor, int year, double baseSum, double baseCount, double policySum, double policyCount);
}

using System;

namespace HealthGPS.Tools.Output;

public record class AccumulatorValue
{
    public double Mean { get; set; }

    public double StDev { get; set; }
}

public record class SummationValue
{
    public int Count { get; set; }

    public double Sum { get; set; }

    public double SumSq { get; set; }

    public double Mean => Sum / Count;

    public double StDev => Math.Sqrt(Variance());

    public void Add(int count, double value)
    {
        Count += count;
        Sum += value;
        SumSq += value * value;
    }

    public double Variance()
    {
        if (Count < 2)
        {
            return double.NaN;
        }

        var localMean = Mean;
        return SumSq / Count - localMean * localMean;
    }
}

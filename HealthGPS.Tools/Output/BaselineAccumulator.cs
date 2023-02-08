using System;

namespace HealthGPS.Tools.Output;

public class BaselineAccumulator : MeasureAccumulatorBase
{
    public BaselineAccumulator(Gender gender, int timeStart, int timeFinish)
        : base(MeasureType.Baseline, gender, timeStart, timeFinish)
    {
    }

    protected override void AddInternal(string factor, int year, double baseSum, double baseCount, double policySum, double policyCount)
    {
        if (factor.Equals("count", StringComparison.OrdinalIgnoreCase))
        {
            this[factor, year].Mean += baseCount;
            this[factor, year].StDev += baseCount;
            return;
        }

        var baseRatio = baseSum / baseCount;
        this[factor, year].Mean += baseRatio;
        this[factor, year].StDev += baseRatio * baseRatio;
    }
}

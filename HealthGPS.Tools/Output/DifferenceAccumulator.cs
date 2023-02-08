using System;

namespace HealthGPS.Tools.Output;

public class DifferenceAccumulator : MeasureAccumulatorBase
{
    public DifferenceAccumulator(Gender gender, int timeStart, int timeFinish)
        : base(MeasureType.Difference, gender, timeStart, timeFinish)
    {
    }

    protected override void AddInternal(string factor, int year, double baseSum, double baseCount, double policySum, double policyCount)
    {
        if (factor.Equals("count", StringComparison.OrdinalIgnoreCase))
        {
            this[factor, year].Mean += 0;
            this[factor, year].StDev += 0;
            return;
        }

        var baseRatio = baseSum / baseCount;
        var policyRatio = policySum / policyCount;
        var diffRatio = policyRatio - baseRatio;

        this[factor, year].Mean += diffRatio;
        this[factor, year].StDev += diffRatio * diffRatio;
    }
}

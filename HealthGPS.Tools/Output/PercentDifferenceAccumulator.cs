using System;

namespace HealthGPS.Tools.Output;

public class PercentDifferenceAccumulator : MeasureAccumulatorBase
{
    public PercentDifferenceAccumulator(Gender gender, int timeStart, int timeFinish)
        : base(MeasureType.PercentDifference, gender, timeStart, timeFinish)
    {
    }

    protected override void AddInternal(string factor, int year, double baseSum, double baseCount, double policySum, double policyCount)
    {
        if (factor.Equals("count", StringComparison.OrdinalIgnoreCase))
        {
            this[factor, year].Mean += 0.0;
            this[factor, year].StDev += 0.0;
            return;
        }

        var baseRatio = baseSum / baseCount;
        var policyRatio = policySum / policyCount;
        var percentRatio = (policyRatio - baseRatio) / baseRatio;
        if (!double.IsFinite(percentRatio))
        {
            percentRatio = 0.0;
        }

        this[factor, year].Mean += percentRatio;
        this[factor, year].StDev += percentRatio * percentRatio;
    }
}

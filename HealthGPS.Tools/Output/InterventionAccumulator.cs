using System;

namespace HealthGPS.Tools.Output;

public class InterventionAccumulator : MeasureAccumulatorBase
{
    public InterventionAccumulator(Gender gender, int timeStart, int timeFinish)
        : base(MeasureType.Intervention, gender, timeStart, timeFinish)
    {
    }

    protected override void AddInternal(string factor, int year, double baseSum, double baseCount, double policySum, double policyCount)
    {
        if (factor.Equals("count", StringComparison.OrdinalIgnoreCase))
        {
            this[factor, year].Mean += policyCount;
            this[factor, year].StDev += policyCount;
            return;
        }

        var policyRatio = policySum / policyCount;
        this[factor, year].Mean += policyRatio;
        this[factor, year].StDev += policyRatio * policyRatio;
    }
}

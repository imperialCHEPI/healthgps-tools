using System;
using System.Collections.Generic;
using System.Text;

namespace HealthGPS.Tools;

public class SampleMoment
{
    private readonly double[] moments;

    public SampleMoment()
    {
        Minimum = double.NaN;
        Maximum = double.NaN;
        moments = new double[5];
    }

    public SampleMoment(IEnumerable<double> values)
        : this()
    {
        AddRange(values);
    }

    public bool IsEmpty => moments[0] < 1.0;

    public int Count => (int)moments[0];

    public double Minimum { get; private set; }

    public double Maximum { get; private set; }

    public double Range => Maximum - Minimum;

    public double Sum => moments[0] * moments[1];

    public double Average => moments[1];

    public double Variance
    {
        get
        {
            if (Count < 2)
            {
                return double.NaN;
            }

            return (moments[2] * moments[0]) / (moments[0] - 1.0);
        }
    }

    public double StdDeviation => Math.Sqrt(Variance);

    public double StdError => Math.Sqrt(Variance / moments[0]);

    public double Kurtosis
    {
        get
        {
            if (moments[0] < 4.0)
            {
                return double.NaN;
            }

            double kFact = (moments[0] - 2.0) * (moments[0] - 3.0);
            double n1 = moments[0] - 1.0;
            double v = Variance;
            return (moments[4] * moments[0] * moments[0] * (moments[0] + 1.0) /
                   (v * v * n1) - n1 * n1 * 3.0) / kFact;
        }
    }

    public double Skewness
    {
        get
        {
            if (moments[0] < 3)
            {
                return double.NaN;
            }

            double v = Variance;
            return moments[3] * moments[0] * moments[0] /
                   (Math.Sqrt(v) * v * (moments[0] - 1.0) * (moments[0] - 2.0));
        }
    }

    public virtual void Clear()
    {
        Minimum = double.NaN;
        Maximum = double.NaN;
        for (int i = 0; i < moments.Length; i++)
        {
            moments[i] = 0.0;
        }
    }

    public void AddRange(IEnumerable<double> values)
    {
        foreach (double value in values)
        {
            Add(value);
        }
    }

    public void Add(double x)
    {
        //Calculates min/max
        if (IsEmpty)
        {
            Minimum = x;
            Maximum = x;
        }
        else
        {
            Minimum = x < Minimum ? x : Minimum;
            Maximum = x > Maximum ? x : Maximum;
        }

        //Moments calculation
        double n = moments[0];
        double n1 = n + 1.0;
        double n2 = n * n;
        double delta = (moments[1] - x) / n1;
        double d2 = delta * delta;
        double d3 = delta * d2;
        double r1 = n / n1;
        moments[4] += 4 * delta * moments[3] + 6 * d2 * moments[2] + (1 + n * n2) * d2 * d2;
        moments[4] *= r1;
        moments[3] += 3 * delta * moments[2] + (1 - n2) * d3;
        moments[3] *= r1;
        moments[2] += (1 + n) * d2;
        moments[2] *= r1;
        moments[1] -= delta;
        moments[0] = n1;
    }

    public override string ToString()
    {
        return Average.ToString();
    }

    public string Report()
    {
        return Report(false);
    }

    public string Report(bool tabular)
    {
        var buffer = new StringBuilder();
        if (tabular)
        {
            buffer.AppendLine("Count, Sum, Minimum, Maximum, Range, Average," +
                              "Variance, Std Deviation, Std Error, Kurtosis, Skewness");
            buffer.AppendFormat($"{Count},{Sum},{Minimum},{Maximum},{Range},{Average}," +
                                $"{Variance},{StdDeviation},{StdError},{Kurtosis},{Skewness}");
            buffer.AppendLine();
        }
        else
        {
            buffer.AppendLine("Count --------= " + Count);
            buffer.AppendLine("Sum ----------= " + Sum);
            buffer.AppendLine("Minimum ------= " + Minimum);
            buffer.AppendLine("Maximum ------= " + Maximum);
            buffer.AppendLine("Range --------= " + Range);
            buffer.AppendLine("Average ------= " + Average);
            buffer.AppendLine("Variance -----= " + Variance);
            buffer.AppendLine("Std Deviation = " + StdDeviation);
            buffer.AppendLine("Std Error ----= " + StdError);
            buffer.AppendLine("Kurtosis -----= " + Kurtosis);
            buffer.AppendLine("Skewness -----= " + Skewness);
        }

        return buffer.ToString();
    }
}
using System;
using System.Collections.Generic;
using System.Linq;

namespace HealthGPS.Tools.Output;

public class FactorMoment
{
    private readonly Dictionary<Gender, Dictionary<int, SampleMoment>> summary;

    public FactorMoment(string factorName)
    {
        if (string.IsNullOrEmpty(factorName))
        {
            throw new ArgumentNullException(nameof(factorName));
        }

        Name = factorName;
        summary = new Dictionary<Gender, Dictionary<int, SampleMoment>>
        {
            { Gender.Male, new Dictionary<int, SampleMoment>() },
            { Gender.Female, new Dictionary<int, SampleMoment>() },
        };
    }

    public string Name { get; }

    public IReadOnlyDictionary<int, SampleMoment> Males => summary[Gender.Male];

    public IReadOnlyDictionary<int, SampleMoment> Females => summary[Gender.Female];

    public SampleMoment this[Gender gender, int age]
    {
        get { return summary[gender][age]; }
    }

    public bool Contains(Gender gender, int age)
    {
        return summary[gender].ContainsKey(age);
    }

    public Interval AgeRange()
    {
        var min = summary.Values.Min(s => s.Keys.Min());
        var max = summary.Values.Max(s => s.Keys.Max());

        return new Interval(min, max);
    }

    public void Add(Gender gender, int age, double value)
    {
        if (!summary[gender].ContainsKey(age))
        {
            summary[gender].Add(age, new SampleMoment());
        }

        summary[gender][age].Add(value);
    }

    public void Clear()
    {
        summary[Gender.Male].Clear();
        summary[Gender.Female].Clear();
    }
}
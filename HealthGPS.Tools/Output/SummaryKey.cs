using System;

namespace HealthGPS.Tools.Output;

public class SummaryKey : IEquatable<SummaryKey>, IComparable<SummaryKey>
{
    public SummaryKey(ScenarioType scenario, int run, int time, Gender gender)
    {
        if (run < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(run), "Must not be a negative number.");
        }

        if (time < 1900)
        {
            throw new ArgumentOutOfRangeException(nameof(time), "Must be a positive number in years greater than 1900.");
        }

        Scenario = scenario;
        Run = run;
        Time = time;
        Gender = gender;
    }

    public ScenarioType Scenario { get; }

    public int Run { get; }

    public int Time { get; }

    public Gender Gender { get; }

    public SummaryKey ToScenario()
    {
        if (Scenario == ScenarioType.Baseline)
        {
            return ToScenario(ScenarioType.Intervention);
        }

        return ToScenario(ScenarioType.Baseline);
    }

    public SummaryKey ToScenario(ScenarioType type)
    {
        return new SummaryKey(type, Run, Time, Gender);
    }

    public int CompareTo(SummaryKey other)
    {
        if (other == null)
        {
            return 1;
        }

        var result = Scenario.CompareTo(other.Scenario);
        if (result != 0)
        {
            return result;
        }

        result = Run.CompareTo(other.Run);
        if (result != 0)
        {
            return result;
        }

        result = Time.CompareTo(other.Time);
        if (result != 0)
        {
            return result;
        }

        return Gender.CompareTo(other.Gender);
    }

    public bool Equals(SummaryKey other)
    {
        if (other == null)
            return false;

        if (Scenario == other.Scenario)
        {
            if (Run == other.Run)
            {
                if (Time == other.Time)
                    return Gender == other.Gender;
            }
        }

        return false;
    }

    public override bool Equals(object obj)
    {
        if (obj == null)
            return false;

        var other = obj as SummaryKey;
        if (other == null)
            return false;
        else
            return Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Scenario, Run, Time, Gender);
    }

    public override string ToString()
    {
        return $"{Scenario},{Run},{Time},{Gender}";
    }

    public static bool operator ==(SummaryKey left, SummaryKey right)
    {
        if ((object)left == null || (object)right == null)
            return Equals(left, right);

        return left.Equals(right);
    }

    public static bool operator !=(SummaryKey left, SummaryKey right)
    {
        if ((object)left == null || (object)right == null)
            return !Equals(left, right);

        return !left.Equals(right);
    }

    public static bool operator >(SummaryKey left, SummaryKey right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <(SummaryKey left, SummaryKey right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >=(SummaryKey left, SummaryKey right)
    {
        return left.CompareTo(right) >= 0;
    }

    public static bool operator <=(SummaryKey left, SummaryKey right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static SummaryKey Parse(string csvText)
    {
        var fields = csvText.Split(',');
        return Parse(fields);
    }

    public static SummaryKey Parse(string[] fields)
    {
        if (fields.Length < 4)
        {
            throw new ArgumentException("Invalid string representation, must have at least 4 fields.");
        }

        var scenario = (ScenarioType)Enum.Parse(typeof(ScenarioType), fields[0].Trim(), true);
        var run = int.Parse(fields[1].Trim());
        var time = int.Parse(fields[2].Trim());
        var gender = (Gender)Enum.Parse(typeof(Gender), fields[3].Trim(), true);
        return new SummaryKey(scenario, run, time, gender);
    }
}

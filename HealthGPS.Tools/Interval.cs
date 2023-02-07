using System;

namespace HealthGPS.Tools
{
    public class Interval
    {
        public Interval() : this(0, 1)
        { }

        public Interval(int from, int to)
        {
            if (from < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(from), "The interval must be positive.");
            }

            if (to < from)
            {
                throw new ArgumentOutOfRangeException(nameof(to), "The interval must not finish before it started.");
            }

            Start = from;
            Finish = to;
        }

        public int Start { get; }

        public int Finish { get; }

        public int Length => Finish - Start;

        public int LengthInc => (Finish - Start) + 1;

        public bool Contains(int value) => value >= Start && value <= Finish;

        public override string ToString()
        {
            return $"[{Start}, {Finish}]";
        }
    }
}

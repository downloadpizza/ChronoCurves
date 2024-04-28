
using System.Globalization;
using SFML.Window;

namespace ChronoCurves {
    public readonly struct OpenClosedRange : IComparable<OpenClosedRange> {
        public readonly bool StartInclusive;
        public readonly double Start;
        public readonly bool EndInclusive;
        public readonly double End;

        public bool Contains(double v) => (StartInclusive ? v >= Start : v > Start) && (EndInclusive ? v <= End : v < End);
        public bool Smaller(double v) => !(StartInclusive ? v >= Start : v > Start);
        public bool Larger(double v) => !(EndInclusive ? v <= End : v < End);
        

        public OpenClosedRange(bool StartInclusive, double Start, bool EndInclusive, double End) {
            if(!(StartInclusive && EndInclusive) && Start.DoubleEquals(End)) throw new ArgumentException("Only Closed-Closed ranges may have the same start and end value");
            if(Start > End) throw new ArgumentException("The start of a range has to be smaller than the end");

            this.Start = Start;
            this.StartInclusive = StartInclusive;
            this.End = End;
            this.EndInclusive = EndInclusive;
        }

        public static OpenClosedRange Parse(string txt) {
            var startInclusive = txt[0] switch
            {
                '[' => true,
                '(' => false,
                _ => throw new ArgumentException("Range must start with ( or ["),
            };
            var endInclusive = txt[^1] switch
            {
                ']' => true,
                ')' => false,
                _ => throw new ArgumentException("Range must end with ) or ]"),
            };

            var numbers = txt[1..^1].Split(",", StringSplitOptions.TrimEntries);
            if(numbers.Length != 2) throw new ArgumentException("Range must include exactly two numbers, did you accidentally use , as a comma?");
            
            var start = double.Parse(numbers[0], CultureInfo.InvariantCulture);
            var end = double.Parse(numbers[1], CultureInfo.InvariantCulture);
            
            if(!(startInclusive && endInclusive) && start.DoubleEquals(end)) throw new ArgumentException("Only a closed-closed (...) range may have the same start and end");

            if(start > end) throw new ArgumentException("Start must be larger than end");

            return new OpenClosedRange(startInclusive, start, endInclusive, end);
        }

        override public string ToString() => $"{(StartInclusive ? "[" : "(") + Start}, {End}{(EndInclusive ? "]" : ")")}";

        public int CompareTo(OpenClosedRange other)
        {
            var dc = this.Start.CompareTo(other.Start);
            var ic = this.StartInclusive.CompareTo(other.StartInclusive);

            return this.Start.DoubleEquals(other.Start) ? -ic : dc;
        }
    }

    public class SnapRegion
    {
        public readonly OpenClosedRange OCRange;
        public readonly double MovePositive;
        public readonly double MoveNeutral;
        public readonly double MoveNegative;

        public SnapRegion(
            OpenClosedRange OpenClosedRange,
            double MovePositive,
            double MoveNeutral,
            double MoveNegative
        )
        {
            this.OCRange = OpenClosedRange;
            this.MovePositive = MovePositive / 1e6;
            this.MoveNeutral = MoveNeutral / 1e6;
            this.MoveNegative = MoveNegative / 1e6;
        }
    }

    public class KBAxis
    {
        public readonly HashSet<HashSet<Keyboard.Key>> NegativeKB;
        public readonly HashSet<HashSet<Keyboard.Key>> PositiveKB;
        public readonly double DefaultValue;
        public readonly double MinInput;
        public readonly double MaxInput;
        public readonly int MinOutput;
        public readonly int MaxOutput;
        public readonly SnapRegion[] SnapRegions;
        private int _currentRegionIndex = 0;
        public double Value;
        public double FractionValue => (Value - MinInput)/(MaxInput - MinInput);

        public void Apply(Direction dir, double time) // time = second/1e6
        {
            var currentRegion = SnapRegions[_currentRegionIndex];

            var move = dir switch
            {
                Direction.Positive => currentRegion.MovePositive,
                Direction.Neutral => currentRegion.MoveNeutral,
                Direction.Negative => currentRegion.MoveNegative,
                _ => throw new NotImplementedException()
            };

            var newValue = Value + move * time;
            Value = Math.Clamp(newValue, currentRegion.OCRange.Start, currentRegion.OCRange.End);

            if (currentRegion.OCRange.Smaller(newValue) && _currentRegionIndex > 0)
            {
                _currentRegionIndex -= 1;
            }
            else if (currentRegion.OCRange.Larger(newValue) && _currentRegionIndex < SnapRegions.Length - 1)
            {
                _currentRegionIndex += 1;
            }
        }


        public KBAxis(
            HashSet<HashSet<Keyboard.Key>> NegativeKB,
            HashSet<HashSet<Keyboard.Key>> PositiveKB,
            double DefaultValue,
            double MinInput,
            double MaxInput,
            int MinOutput,
            int MaxOutput,
            SnapRegion[] SnapRegions)
        {
            if (!SnapRegions[0].OCRange.Start.DoubleEquals(MinInput) || !SnapRegions[0].OCRange.StartInclusive)
            {
                throw new ArgumentException($"First interval needs to be [{MinInput}, ?? was {SnapRegions[0].OCRange}");
            }

            if (!SnapRegions[^1].OCRange.End.DoubleEquals(MaxInput) || !SnapRegions[^1].OCRange.EndInclusive)
            {
                throw new ArgumentException($"Last interval needs to be ??, {MaxInput}] was {SnapRegions[^1].OCRange}");
            }

            for (int i = 1; i < SnapRegions.Length; i++)
            {
                if (!SnapRegions[i - 1].OCRange.End.DoubleEquals(SnapRegions[i].OCRange.Start))
                {
                    throw new ArgumentException($"Intervals have to start with the previous end value, was {SnapRegions[i - 1].OCRange} and {SnapRegions[i].OCRange}");
                }

                if (SnapRegions[i - 1].OCRange.EndInclusive == SnapRegions[i].OCRange.StartInclusive)
                {
                    throw new ArgumentException($"Adjacent edges of intervals have to be one open one closed, was {SnapRegions[i - 1].OCRange} and {SnapRegions[i].OCRange}");
                }


                if (SnapRegions[i].OCRange.Contains(Value))
                {
                    _currentRegionIndex = i;
                }
            }

            if(DefaultValue < MinInput || DefaultValue > MaxInput) throw new ArgumentException($"Default value {DefaultValue} must be between {MinInput} and {MaxInput}");

            this.PositiveKB = PositiveKB;
            this.NegativeKB = NegativeKB;
            this.DefaultValue = DefaultValue;
            this.Value = DefaultValue;
            this.MinInput = MinInput;
            this.MaxInput = MaxInput;
            this.MinOutput = MinOutput;
            this.MaxOutput = MaxOutput;
            this.SnapRegions = SnapRegions;
        }
    }

    public static class DoubleUtility {
        public static bool DoubleEquals(this double a, double b) => Math.Abs(a - b) < 0.000000001;
    }
}
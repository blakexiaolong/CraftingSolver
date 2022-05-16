namespace Libraries
{
    public enum ActionType
    {
        CountUp,
        CountDown,
        Indefinite,
        Immediate
    };

    public class Action : System.IEquatable<Action>
    {
        public int ID { get; init; } // used only for comparison - faster than string comparison on the name

        public string? ShortName { get; init; }
        public string? Name { get; init; }
        public int DurabilityCost { get; init; }
        public int CPCost { get; init; }
        public double SuccessProbability { get; init; }
        public double QualityIncreaseMultiplier { get; init; }
        public double ProgressIncreaseMultiplier { get; set; }
        public ActionType ActionType { get; init; }
        public int ActiveTurns { get; init; }
        public string? Class { get; set; }
        public int Level { get; set; }
        public bool OnGood { get; init; }
        public bool OnExcellent { get; init; }
        public bool OnPoor { get; set; }

        public static bool Equals(Action? x, Action? y)
        {
            if (x == default && y == default) return true;
            else if (x == default || y == default) return false;
            else return x.Equals(y);
        }


        public bool Equals(Action? other) => other != null && ID == other.ID;
        public override string ToString() => $"{Name}";
    }
}

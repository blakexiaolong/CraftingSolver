using System.Diagnostics;

namespace CraftingSolver
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
        public int ID { get; set; } // used only for comparison - faster than string comparison on the name

        public string? ShortName { get; set; }
        public string? Name { get; set; }
        public int DurabilityCost { get; set; }
        public int CPCost { get; set; }
        public double SuccessProbability { get; set; }
        public double QualityIncreaseMultiplier { get; set; }
        public double ProgressIncreaseMultiplier { get; set; }
        public ActionType ActionType { get; set; }
        public int ActiveTurns { get; set; }
        public string? Class { get; set; }
        public int Level { get; set; }
        public bool OnGood { get; set; }
        public bool OnExcellent { get; set; }
        public bool OnPoor { get; set; }

        public static bool Equals(Action x, Action y)
        {
            if (x == default && y == default) return true;
            else if (x == default || y == default) return false;
            else return x.Equals(y);
        }


        public bool Equals(Action? other) => other != null && ID == other.ID;
        public override string ToString() => $"{Name}";
    }
}

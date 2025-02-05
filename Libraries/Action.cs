namespace Libraries
{
    public enum ActionType
    {
        CountUp,
        CountDown,
        Indefinite,
        Immediate
    };

    public class Action
    {
        public byte ID { get; init; } // used only for comparison - faster than string comparison on the name

        public string? ShortName { get; init; }
        public string? Name { get; init; }
        public byte DurabilityCost { get; init; }
        public byte CPCost { get; init; }
        public double SuccessProbability { get; init; }
        public double QualityIncreaseMultiplier { get; init; }
        public double ProgressIncreaseMultiplier { get; set; }
        public ActionType ActionType { get; init; }
        public byte ActiveTurns { get; init; }
        public string? Class { get; set; }
        public byte Level { get; set; }
        public bool OnGood { get; init; }
        public bool OnExcellent { get; init; }
        public bool OnPoor { get; set; }
    }
}

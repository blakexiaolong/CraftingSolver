namespace CraftingSolver
{
    public class SimulatorStep
    {
        public double Craftsmanship { get; set; }
        public double Control { get; set; }
        public int EffectiveCrafterLevel { get; set; }
        public int EffectiveRecipeLevel { get; set; }
        public int LevelDifference { get; set; }
        public double SuccessProbability { get; init; }
        public double QualityIncreaseMultiplier { get; set; }
        public double BProgressGain { get; init; }
        public double BQualityGain { get; init; }
        public double DurabilityCost { get; init; }
        public int CPCost { get; init; }
    }
}

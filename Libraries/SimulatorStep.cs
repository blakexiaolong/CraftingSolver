namespace CraftingSolver
{
    public class SimulatorStep
    {
        public double Craftsmanship { get; set; }
        public double Control { get; set; }
        public int EffectiveCrafterLevel { get; set; }
        public int EffectiveRecipeLevel { get; set; }
        public int LevelDifference { get; set; }
        public double SuccessProbability { get; set; }
        public double QualityIncreaseMultiplier { get; set; }
        public double BProgressGain { get; set; }
        public double BQualityGain { get; set; }
        public double DurabilityCost { get; set; }
        public int CPCost { get; set; }
    }
}

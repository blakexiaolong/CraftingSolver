namespace CraftingSolver
{
    public class Recipe
    {
        public int Level { get; init; }
        public int Difficulty { get; init; }
        public int Durability { get; init; }
        public int StartQuality { get; init; }
        public int MaxQuality { get; init; }
        public int SuggestedCraftsmanship { get; set; }
        public int SuggestedControl { get; set; }
        public int Stars { get; set; }
        public bool IsExpert { get; set; }

        public int ProgressDivider { get; init; }
        public int QualityDivider { get; init; }
        public double ProgressModifier { get; init; }
        public double QualityModifier { get; init; }
    }
}

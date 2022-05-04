namespace CraftingSolver
{
    public class Recipe
    {
        public int Level { get; set; }
        public int Difficulty { get; set; }
        public int Durability { get; set; }
        public int StartQuality { get; set; }
        public int MaxQuality { get; set; }
        public int SuggestedCraftsmanship { get; set; }
        public int SuggestedControl { get; set; }
        public int Stars { get; set; }
        public bool IsExpert { get; set; }

        public int ProgressDivider { get; set; }
        public int QualityDivider { get; set; }
        public double ProgressModifier { get; set; }
        public double QualityModifier { get; set; }
    }
}

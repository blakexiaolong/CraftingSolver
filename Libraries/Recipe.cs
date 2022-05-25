namespace Libraries
{
    public struct Recipe
    {
        public int Level { get; set; }
        public int RLevel { get; init; }
        public int Difficulty { get; init; }
        public int Durability { get; init; }
        public int StartQuality { get; init; }
        public int MaxQuality { get; init; }
        public bool IsExpert { get; set; }

        public int ProgressDivider { get; init; }
        public int QualityDivider { get; init; }
        public double ProgressModifier { get; init; }
        public double QualityModifier { get; init; }
    }
}

namespace Libraries
{
    public struct Recipe
    {
        public byte Level { get; set; }
        public short RLevel { get; init; }
        public int Difficulty { get; init; }
        public byte Durability { get; init; }
        public int StartQuality { get; init; }
        public int MaxQuality { get; init; }
        public bool IsExpert { get; set; }

        public byte ProgressDivider { get; init; }
        public byte QualityDivider { get; init; }
        public double ProgressModifier { get; init; }
        public double QualityModifier { get; init; }
    }
}

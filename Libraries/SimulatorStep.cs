namespace Libraries
{
    public struct SimulatorStep
    {
        public double BProgressGain { get; init; }
        public double BQualityGain { get; init; }
        public double DurabilityCost { get; init; }
        public int CpCost { get; init; }
    }
}

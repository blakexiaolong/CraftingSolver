namespace Libraries
{
    public struct Crafter
    {
        public int Craftsmanship { get; init; }
        public int Control { get; init; }
        public int CP { get; init; }
        public int Level { get; init; }
        public Action?[] Actions { get; init; }
    }
}

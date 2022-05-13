namespace CraftingSolver
{
    public class Crafter
    {
        public string Class { get; set; }
        public int Craftsmanship { get; init; }
        public int Control { get; init; }
        public int CP { get; init; }
        public int Level { get; init; }
        public bool Specialist { get; set; }
        public Action[] Actions { get; init; }
    }
}

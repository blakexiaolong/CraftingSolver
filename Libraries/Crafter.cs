namespace CraftingSolver
{
    public class Crafter
    {
        public string Class { get; set; }
        public int Craftsmanship { get; set; }
        public int Control { get; set; }
        public int CP { get; set; }
        public int Level { get; set; }
        public bool Specialist { get; set; }
        public Action[] Actions { get; set; }
    }
}

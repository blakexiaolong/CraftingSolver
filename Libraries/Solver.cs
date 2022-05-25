namespace Libraries
{
    public static class Solver
    {
        public delegate void LoggingDelegate(string message);
        public interface ISolver
        {
            List<Action>? Run(int maxTasks);
        }
        internal class DfsState
        {
            public long NodesGenerated;
            public long SolutionCount;

            public DfsState()
            {
                NodesGenerated = 0;
                SolutionCount = 0;
            }

            public void Add(DfsState? s)
            {
                if (s == null) return;
                lock (this)
                {
                    NodesGenerated += s.NodesGenerated;
                    SolutionCount += s.SolutionCount;
                }
            }
        }
    }
}

namespace Libraries
{
    public static class Solver
    {
        public delegate void LoggingDelegate(string message);
        public interface ISolver
        {
            List<Action>? Run(int maxTasks);
        }
    }
}

namespace Libraries
{
    public static class Solver
    {
        private static readonly Dictionary<string, int> WastedDict = new();

        public static double ScoreState(Simulator sim, State state, bool ignoreProgress = false)
        {
            if (!state.Success)
            {
                var violations = state.CheckViolations();
                if (state.WastedActions > 0 || !violations.DurabilityOk || !violations.CpOk)
                {
                    foreach (var kvp in state.WastedCounter)
                    {
                        if (!WastedDict.ContainsKey(kvp.Key))
                        {
                            WastedDict.Add(kvp.Key, kvp.Value);
                        }
                        else
                        {
                            WastedDict[kvp.Key] += kvp.Value;
                        }
                    }
                    return -1;
                }
            }
            double progress = ignoreProgress ? 0 : (state.Progress > sim.Recipe.Difficulty ? sim.Recipe.Difficulty : state.Progress) / sim.Recipe.Difficulty;
            double maxQuality = sim.Recipe.MaxQuality * 1.1;
            double quality = (state.Quality > maxQuality ? maxQuality : state.Quality) / sim.Recipe.MaxQuality;
            double cp = state.Cp / sim.Crafter.CP;
            double dur = state.Durability / sim.Recipe.Durability;
            return (progress + quality) * 100 + (cp + dur) * 10;
        }


        public delegate void LoggingDelegate(string message);
        public interface ISolver
        {
            List<Action?> Run(Simulator sim, int maxTasks, LoggingDelegate? loggingDelegate = null);
        }
    }
}

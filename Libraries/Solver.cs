using Libraries;

namespace CraftingSolver
{
    public class Solver
    {
        private static readonly Dictionary<string, int> WastedDict = new();

        public static List<Action> GetFirstRoundActions(Simulator sim)
        {
            List<Action> firstRoundActions = sim.Crafter.Actions.ToList();
            if (sim.Crafter.Level - sim.Recipe.Level < 10) firstRoundActions.Remove(Atlas.Actions.TrainedEye);
            return firstRoundActions;
        }
        public static List<Action> GetOtherActions(List<Action> firstRoundActions)
        {
            Action[] temp = new Action[firstRoundActions.Count];
            firstRoundActions.CopyTo(temp);
            List<Action> otherActions = temp.ToList();
            otherActions.Remove(Atlas.Actions.MuscleMemory);
            otherActions.Remove(Atlas.Actions.TrainedEye);
            otherActions.Remove(Atlas.Actions.Reflect);
            return otherActions;
        }

        public static Tuple<double, bool>? ScoreState(Simulator sim, State state, bool ignoreProgress = false)
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
                    return new Tuple<double, bool>(-1, false);
                }
            }
            bool perfectSolution = state.Quality >= sim.Recipe.MaxQuality && state.Progress >= sim.Recipe.Difficulty;
            double progress = ignoreProgress ? 0 : (state.Progress > sim.Recipe.Difficulty ? sim.Recipe.Difficulty : state.Progress) / sim.Recipe.Difficulty;
            double maxQuality = sim.Recipe.MaxQuality * 1.1;
            double quality = (state.Quality > maxQuality ? maxQuality : state.Quality) / sim.Recipe.MaxQuality;
            double cp = state.Cp / sim.Crafter.CP;
            double dur = state.Durability / sim.Recipe.Durability;
            return new Tuple<double, bool>((progress + quality) * 100 + (cp + dur) * 10, perfectSolution);
        }

        public interface ISolver
        {
            List<Action> Run(Simulator sim, int maxTasks);
        }
    }
}

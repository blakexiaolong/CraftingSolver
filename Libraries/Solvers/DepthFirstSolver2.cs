// using System.Runtime;
// using static Libraries.Solver;
// namespace Libraries.Solvers;
//
// public class DepthFirstSolver2 : ISolver
// {
//     private readonly LightSimulator _sim;
//     private readonly LightState _startState;
//     private readonly LoggingDelegate _logger;
//     private readonly Action[] _crafterActions;
//     private readonly int _crafterActionCount;
//     
//     private int _countdown = 2;
//     private readonly object _lockObject = new();
//
//     private KeyValuePair<double, List<Action>>? bestSolution;
//
//     public DepthFirstSolver2(LightSimulator sim, LoggingDelegate loggingDelegate)
//     {
//         _sim = sim;
//         _logger = loggingDelegate;
//         _startState = _sim.Simulate(new List<Action>())!.Value;
//         _crafterActions = _sim.Crafter.Actions.Where(x => Atlas.Actions.DependableActions.Contains(x)).Select(x => Atlas.Actions.AllActions[x]).ToArray();
//         _crafterActionCount = _crafterActions.Length;
//     }
//
//     public List<Action>? Run(int maxTasks)
//     {
//         _logger($"\n[{DateTime.Now}] Generating Combinations");
//         GCLatencyMode oldMode = GCSettings.LatencyMode;
//         ActionNode head = new ActionNode(null, _startState, null!);
//         try
//         {
//             GCSettings.LatencyMode = GCLatencyMode.Batch;
//             int height = 9;
//             DfsState s = GenerateDfsActionTree(head, height, height)!;
//             _logger($"[{DateTime.Now}] Nodes Generated {s.NodesGenerated} | Solutions Found {s.SolutionCount}");
//         }
//         finally
//         {
//             GCSettings.LatencyMode = oldMode;
//         }
//         return bestSolution?.Value;
//     }
//
//     private DfsState? GenerateDfsActionTree(ActionNode node, int maxLength, int remainingDepth)
//     {
//         if (remainingDepth <= 0) return null;
//         
//         DfsState state = new();
//         if (remainingDepth > 4 && _countdown > 0)
//         {
//             lock (_lockObject) _countdown = Math.Max(_countdown - 1, 0);
//             Thread[] threads = new Thread[_crafterActionCount];
//             for (int i = 0; i < _crafterActionCount; i++)
//             {
//                 Action action = _crafterActions[i];
//                 threads[i] = new Thread(() => state.Add(GenerateDfsActionTreeInner(node, action, maxLength, remainingDepth)));
//                 threads[i].Start();
//             }
//             for (int i = 0; i < _crafterActionCount; i++) threads[i].Join();
//             lock (_lockObject) _countdown += 1;
//         }
//         else
//         {
//             for (int i = 0; i < _crafterActionCount; i++)
//             {
//                 state.Add(GenerateDfsActionTreeInner(node, _crafterActions[i], maxLength, remainingDepth));
//             }
//         }
//         return state;
//     }
//
//     private DfsState? GenerateDfsActionTreeInner(ActionNode node, Action action, int maxLength, int remainingDepth)
//     {
//         LightState? state = _sim.Simulate(action, node.State);
//         if (state == null) return null;
//
//         double score = ScoreState(state.Value);
//         if (score <= 0) return null;
//
//         ActionNode newNode;
//         lock (node) { newNode = node.Add(action, state.Value); }
//
//         DfsState dfsState = new DfsState();
//         dfsState.NodesGenerated++;
//
//         if (state.Value.Success(_sim))
//         {
//             dfsState.SolutionCount++;
//             lock (_lockObject)
//             {
//                 if (!bestSolution.HasValue || score > bestSolution.Value.Key)
//                 {
//                     var path = newNode.GetPath(state.Value.Step - 1);
//                     var sanityState = _sim.Simulate(path);
//                     if (sanityState == null)
//                     {
//                         return dfsState;
//                     }
//
//                     _logger($"\t({score}): {string.Join(",", path.Select(x => x.ShortName))}");
//                     bestSolution = new(score, path);
//                 }
//             }
//         }
//         else
//         {
//             dfsState.Add(GenerateDfsActionTree(newNode, maxLength, remainingDepth - 1));
//         }
//
//         lock (newNode.Parent) { newNode.Parent.Remove(newNode); }
//         return dfsState;
//     }
//
//     private double ScoreState(LightState state)
//     {
//         bool success = state.Success(_sim);
//         if (!success)
//         {
//             var violations = state.CheckViolations(_sim);
//             if (!violations.DurabilityOk || !violations.CpOk) return -1;
//         }
//
//         double progress = (state.Progress > _sim.Recipe.Difficulty ? _sim.Recipe.Difficulty : state.Progress) / _sim.Recipe.Difficulty;
//         double maxQuality = _sim.Recipe.MaxQuality * 1.1;
//         double quality = (state.Quality > maxQuality ? maxQuality : state.Quality) / _sim.Recipe.MaxQuality;
//
//         double cp = state.CP / _sim.Crafter.CP;
//         double dur = state.Durability / _sim.Recipe.Durability;
//         double extraCredit = success ? 1000 : (cp + dur) * 10;
//
//         return (progress + quality) * 100;
//     }
// }
// using System.Collections;
// using System.Collections.Concurrent;
// using System.Diagnostics;
// using System.Numerics;
// using static Libraries.Solver;
// namespace Libraries.Solvers;
//
// public class BacktrackingSolver
// {
//     private const int MaxThreads = 1; // 24
//     
//     private double _bestScore;
//     private List<Action> _bestSolution;
//     private long _calls, _evaluated, _misses, _aggressiveRejections;
//     private long _progressCount;
//     private double _progressPercent;
//
//     private readonly LightSimulator _sim;
//     private readonly LoggingDelegate _logger;
//     private readonly int[] _actions;
//     private readonly long[] _progressAmounts;
//     private readonly long _maxProgressCount;
//     
//     //private const int MaxRegistrySize = 90_000_000;
//     //private readonly object _locker = new();
//     //private long _registryCount, _collision;
//     //private readonly Dictionary<BigInteger, int> _registry = new(MaxRegistrySize * 2);
//     //private readonly ConcurrentDictionary<BigInteger, int> _registry = new(12, MaxRegistrySize * 2);
//     
//     private readonly CountdownEvent _counter = new(1);
//     private readonly Thread?[] _threads = new Thread[MaxThreads];
//     private readonly StreamWriter _file;
//
//     public BacktrackingSolver(LightSimulator sim, LoggingDelegate loggingDelegate)
//     {
//         _sim = sim;
//         _logger = loggingDelegate;
//         _actions = sim.Crafter.Actions.OrderBy(x => x).ToArray();
//         _bestSolution = new();
//         //if (!File.Exists(@"C:\Users\ajime\Downloads\SuccessfulSimulations.txt")) File.Create(@"C:\Users\ajime\Downloads\SuccessfulSimulations.txt");
//         //_file = new(@"C:\Users\ajime\Downloads\SuccessfulSimulations.txt", append: false);
//
//         int progressGranularity = 9;
//         _progressAmounts = new long[progressGranularity];
//         for (int i = 0; i < progressGranularity; i++) _progressAmounts[i] = (long)Math.Pow(_actions.Length, progressGranularity - 1 - i);
//         _maxProgressCount += _progressAmounts[0];
//     }
//
//     public async Task<List<Action>> Run()
//     {
//         List<Thread> extraThreads = new();
//         for (int i = 0; i < _actions.Length; i++)
//         {
//             Thread t = BacktrackThread(i, null, new() { _actions[i] });
//             if (i < _threads.Length) _threads[i] = t;
//             else extraThreads.Add(t);
//             t.Start();
//         }
//
//         Stopwatch sw = new Stopwatch();
//         sw.Start();
//
//         foreach (Thread t in extraThreads) await Task.Run(() => t.Join());
//         _counter.Signal();
//         await Task.Run(() => _counter.Wait());
//         Console.WriteLine($" [{DateTime.Now}, {sw.ElapsedMilliseconds / 1000}s] -- Calls: {_calls:N0}, Evals: {_evaluated:N0} ({_calls - _evaluated:N0} rejected, {_aggressiveRejections:N0} aggressively, {_misses:N0} missed))");
//         return _bestSolution;
//     }
//
//     private Thread BacktrackThread(int threadId, LightState? lastState, List<int> path) => new(() =>
//     {
//         _counter.AddCount();
//         Backtrack(lastState, path);
//         if (threadId < _threads.Length)
//         {
//             _threads[threadId] = null;
//         }
//         _counter.Signal();
//     });
//     private float Backtrack(LightState? lastState, List<int> path)
//     {
//         if (Reject(lastState, path, out LightState? state) || state == null)
//         {
//             if (path.Count < _progressAmounts.Length) AddProgress(_progressAmounts[path.Count]);
//             return 0F;
//         }
//         if (Accept(state.Value)) Output(path, state.Value);
//
//         float success = 0;
//         if (ShouldExpand(state.Value)) success = Expand(path, state.Value);
//
//         if (path.Count == _progressAmounts.Length) AddProgress(1);
//         return success;
//     }
//     
//     #region Audits
//     private bool NonFirstRound(List<int> path)
//     {
//         return path.Count > 1 && Atlas.Actions.FirstRoundActions.Contains(path[^1]);
//     }
//     private bool ReusedBuff(List<int> path)
//     {
//         if (path.Count > 1 && path[^1] == path[^2] && Atlas.Actions.Buffs.Contains(path[^1]))
//         {
//             return true;
//         }
//         return false;
//     }
//     private bool Unfocused(List<int> path)
//     {
//         if (path[^1] == (int)Atlas.Actions.ActionMap.FocusedSynthesis || path[^1] == (int)Atlas.Actions.ActionMap.FocusedTouch)
//         {
//             return path.Count == 1 || path[^2] != (int)Atlas.Actions.ActionMap.Observe;
//         }
//         else if (path.Count > 1 && path[^2] == (int)Atlas.Actions.ActionMap.Observe) return true;
//         return false;
//     }
//     private bool NoQualityBeforeByregots(List<int> path)
//     {
//         if (path[^1] != (int)Atlas.Actions.ActionMap.ByregotsBlessing) return false;
//         for (int i = path.Count - 2; i > 0; i--)
//             if (Atlas.Actions.AllActions[path[i]].QualityIncreaseMultiplier > 0)
//                 return path[i] == (int)Atlas.Actions.ActionMap.ByregotsBlessing;
//         return true;
//     }
//     private bool OutOfDurability(List<int> path, LightState? lastState)
//     {
//         if (lastState == null) return false;
//         return Atlas.Actions.AllActions[path[^1]].DurabilityCost > 0 &&
//                Atlas.Actions.AllActions[path[^1]].DurabilityCost *
//                (lastState.Value.CountDowns.ContainsKey((int)Atlas.Actions.ActionMap.WasteNot) || lastState.Value.CountDowns.ContainsKey((int)Atlas.Actions.ActionMap.WasteNot2) ? 1 : 0.5) > lastState.Value.Durability;
//     }
//     private bool OutOfCp(List<int> path, LightState? lastState)
//     {
//         return lastState?.CP - Atlas.Actions.AllActions[path[^1]].CPCost < 0;
//     }
//     #endregion
//
//     private bool Reject(LightState? lastState, List<int> path, out LightState? state)
//     {
//         if (path.Count > 8) // TODO change this to 26 for production versions?
//         {
//             state = null;
//             return true;
//         }
//         _calls++;
//         state = null;
//
//         if ((lastState.HasValue && lastState.Value.Success(_sim)) || OutOfCp(path, lastState) || NonFirstRound(path) || ReusedBuff(path) || Unfocused(path) || NoQualityBeforeByregots(path)) return true;
//         if (AggressiveReject(path, lastState))
//         {
//             _aggressiveRejections++;
//             return true;
//         }
//         //if (OutOfDurability(path, lastState)) { return true; }
//         
//         _evaluated++;
//         state = lastState.HasValue ? _sim.Simulate(path.Last(), lastState.Value) : _sim.Simulate(path.Last());
//         if (state == null)
//         {
//             _misses++;
//             return true;
//         }
//         
//         // TODO: do some math to see if the state is still complete-able from this point
//
//         return false;// Register2(state.Value); // remember this state, if we already knew about it, short circuit
//     }
//     private bool AggressiveReject(List<int> path, LightState? lastState)
//     {
//         switch (path[^1])
//         {
//             case (int)Atlas.Actions.ActionMap.ByregotsBlessing:
//                 int bb = (int)Atlas.Actions.ActionMap.ByregotsBlessing;
//                 for (int i = 0; i < path.Count - 1; i++)
//                 {
//                     if (path[i] == bb) return true;
//                 }
//                 break;
//             case (int)Atlas.Actions.ActionMap.BasicTouch:
//                 if (path.Count > 1)
//                 {
//                     if (path[^2] == (int)Atlas.Actions.ActionMap.BasicTouch || path[^2] == (int)Atlas.Actions.ActionMap.StandardTouch) return true;
//                     if (path.Count > 2 && path[^2] == (int)Atlas.Actions.ActionMap.AdvancedTouch && path[^3] == (int)Atlas.Actions.ActionMap.StandardTouch) return true;
//                 }
//                 break;
//             case (int)Atlas.Actions.ActionMap.StandardTouch:
//                 if (path.Count > 2 && path[^2] == (int)Atlas.Actions.ActionMap.AdvancedTouch && path[^3] == (int)Atlas.Actions.ActionMap.BasicTouch) return true;
//                 break;
//             case (int)Atlas.Actions.ActionMap.MastersMend:
//                 return !lastState.HasValue || lastState.Value.Durability > _sim.Recipe.Durability - 15;
//             case (int)Atlas.Actions.ActionMap.Veneration:
//                 return path.Count > 1 && (path[^2] == (int)Atlas.Actions.ActionMap.Innovation || path[^2] == (int)Atlas.Actions.ActionMap.GreatStrides);
//             case (int)Atlas.Actions.ActionMap.Innovation:
//                 return path.Count > 1 && path[^2] == (int)Atlas.Actions.ActionMap.Veneration;
//         }
//         return false;
//     }
//
//     // private bool Register(LightState state)
//     // {
//     //     BigInteger s = state.ToBigInt();
//     //
//     //     if (_registry.ContainsKey(s))
//     //     {
//     //         _registry[s]++;
//     //         _collision++;
//     //         return true;
//     //     }
//     //     else
//     //     {
//     //         await lock (_locker)
//     //         {
//     //             try
//     //             {
//     //                 _unlockedWatch.Stop();
//     //                 if (_registry.ContainsKey(s))
//     //                 {
//     //                     _registry[s]++;
//     //                     _collision++;
//     //
//     //                     return true;
//     //                 }
//     //                 else
//     //                 {
//     //                     _registry.Add(s, 1);
//     //                     _registryCount++;
//     //
//     //                     if (_registryCount > MaxRegistrySize)
//     //                     {
//     //                         Stopwatch sw = new();
//     //                         sw.Start();
//     //
//     //                         long oldRegistryCount = _registryCount;
//     //                         List<int> sorted = _registry.Select(y => y.Value).ToList();
//     //                         sorted.Sort();
//     //                         int pivot = sorted[(int)(MaxRegistrySize * 0.5F)];
//     //                         foreach (var kvp in _registry)
//     //                         {
//     //                             if (kvp.Value <= pivot)
//     //                             {
//     //                                 _registry.Remove(kvp.Key, out _);
//     //                                 _registryCount--;
//     //                             }
//     //                         }
//     //
//     //                         Console.WriteLine(
//     //                             $"!!! Cleaned Up ({pivot}) ({oldRegistryCount:N0} => {_registryCount:N0}) !!!");
//     //                     }
//     //
//     //                     return false;
//     //                 }
//     //             }
//     //             finally
//     //             {
//     //                 _unlockedWatch.Start();
//     //             }
//     //         }
//     //     }
//     // }
//     // private bool Register2(LightState state)
//     // {
//     //     BigInteger s = state.ToBigInt();
//     //     return _registry.AddOrUpdate(s, (key) =>
//     //     {
//     //         _registryCount++;
//     //         if (_registryCount > MaxRegistrySize)
//     //         {
//     //             long oldRegistryCount = _registryCount;
//     //             List<int> sorted = _registry.Select(y => y.Value).ToList();
//     //             sorted.Sort();
//     //             int pivot = sorted[(int)(MaxRegistrySize * 0.5F)];
//     //             foreach (var kvp in _registry)
//     //             {
//     //                 if (kvp.Value <= pivot)
//     //                 {
//     //                     _registry.Remove(kvp.Key, out _);
//     //                     _registryCount--;
//     //                 }
//     //             }
//     //
//     //             Console.WriteLine($"!!! Cleaned Up ({pivot}) ({oldRegistryCount:N0} => {_registryCount:N0}) !!!");
//     //         }
//     //
//     //         return 1;
//     //     }, (_, oldValue) =>
//     //     {
//     //         _collision++;
//     //         return oldValue + 1;
//     //     }) > 1;
//     // }
//
//     private bool ShouldExpand(LightState state)
//     {
//         return state.Durability > 0 && !state.Success(_sim);
//     }
//     private float Expand(List<int> path, LightState state)
//     {
//         float sum = 0;
//         int children = 0;
//         
//         path.Add(_actions[0]);
//         foreach (int action in _actions)
//         {
//             path[^1] = action;
//             bool threadStarted = false;
//
//             if (false && _counter.CurrentCount < MaxThreads)
//             {
//                 lock (_threads)
//                 {
//                     if (_counter.CurrentCount < MaxThreads)
//                     {
//                         List<int> localPath = path.ToList();
//                         for (int i = 0; i < _threads.Length; i++)
//                         {
//                             if (_threads[i] == null)
//                             {
//                                 _threads[i] = BacktrackThread(i, state, localPath);
//                                 _threads[i]!.Start();
//                                 threadStarted = true;
//                                 break;
//                             }
//                         }
//                     }
//                 }
//             }
//
//             if (!threadStarted)
//             {
//                 sum += Backtrack(state, path);
//                 children++;
//             }
//         }
//         path.RemoveAt(path.Count - 1);
//
//         float success = children > 0 ? sum / children : 0;
//         if (children > 0 && success < 0.05)
//         {
//             //var _ = string.Join(", ", path.Select(x => Atlas.Actions.AllActions[x].ShortName));
//         }
//         return children > 0 ? success : 0;
//     }
//     
//     private double ScoreState(LightState? state) // max 250 
//     {
//         if (!state.HasValue || !state.Value.Success(_sim)) return -1;
//
//         double maxQuality = _sim.Recipe.MaxQuality * 1.1;
//         double quality = Math.Min(maxQuality, state.Value.Quality) / _sim.Recipe.MaxQuality; // max 110
//
//         double cp = state.Value.CP / _sim.Crafter.CP;
//         double steps = 1 - state.Value.Step / 100D;
//         double extraCredit = cp + steps; // max 2
//
//         return (quality * 100 + extraCredit) / 112; // max 100
//     }
//     private bool Accept(LightState state) => state.Success(_sim) && ScoreState(state) > _bestScore;
//     private void Output(List<int> path, LightState state)
//     {
//         double score = ScoreState(state);
//         
//         lock (_bestSolution)
//         {
//             if (score > _bestScore)
//             {
//                 List<Action> actions = path.Select(x => Atlas.Actions.AllActions[x]).ToList();
//                 _logger($"{string.Join(",", actions.Select(x => x.ShortName))} - {score}");
//                 
//                 _bestScore = score;
//                 _bestSolution = actions;
//             }
//         }
//     }
//     private void AddProgress(long amount)
//     {
//         lock (_progressAmounts)
//         {
//             _progressCount += amount;
//             double temp = (double)_progressCount / _maxProgressCount;
//             if (temp > _progressPercent + 0.01)
//             {
//                 _progressPercent = temp;
//                 Console.WriteLine($"----{Math.Round(_progressPercent * 100)}% = Calls: {_calls:N0}, Evals: {_evaluated:N0} ({(_calls - _evaluated):N0} rejected - {_aggressiveRejections:N0} aggressively), {_misses:N0} missed----");
//             }
//         }
//     }
//     
// }
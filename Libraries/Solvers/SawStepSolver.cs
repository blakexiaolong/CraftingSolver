using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Linq;

namespace Libraries.Solvers;
using static Solver;

public class SawStepSolver
{
    private const int
        MaxThreads = 14,
        MaxDepth = 26,
        StepForwardDepth = 6,
        StepBackDepth = StepForwardDepth - 1,
        StepSize = 20;

    private double _bestScore;
    private List<Action> _bestSolution;
    private long _evaluated, _failures, _forwardSet;

    private readonly LightSimulator _sim;
    private readonly LoggingDelegate _logger;
    private readonly int[] _actions;
    private readonly Dictionary<int, Dictionary<int[], int>> _presolve;

    private CountdownEvent _countdown = new(1);
    private readonly Thread?[] _threads = new Thread[MaxThreads];
    private readonly object _locker = new();
    private readonly Stopwatch _sw = new ();
    private readonly ConcurrentBag<(double, List<int>, int[])> _stepResults = new();

    public SawStepSolver(LightSimulator sim, LoggingDelegate loggingDelegate)
    {
        _sim = sim;
        _logger = loggingDelegate;
        _actions = sim.Crafter.Actions.OrderBy(x => x).ToArray();
        _bestSolution = new List<Action>();

        BigInteger maxProgressCount = BigInteger.Pow(_actions.Length, MaxDepth);
        BigInteger solverCount = BigInteger.Pow(_actions.Length, StepForwardDepth + 1);
        double presolverCount = Math.Pow(_sim.Crafter.Actions.Length, StepForwardDepth);
        _logger($"[{DateTime.Now}] Game space is {maxProgressCount:N0} nodes, solver space is {solverCount:N0} nodes, presolver space is {presolverCount:N0} nodes");

        // TODO: Maybe yank this out into a database somehow?
        Console.Write("Pre-solving");
        _presolve = Presolve();

        double expansionsFound = _presolve.Sum(x => x.Value.Count);
        _logger($"\n[{DateTime.Now}] {expansionsFound:N0} expansions found (eliminated {presolverCount - expansionsFound:N0} [{1 - expansionsFound / presolverCount:P0}] possible expansions)");
    }

    private Dictionary<int, Dictionary<int[], int>> Presolve()
    {
        Dictionary<int, Dictionary<int[], int>> allowedPaths = _sim.Crafter.Actions.ToDictionary(t => t, t => new Dictionary<int[], int>());

        int[] path = new int[StepForwardDepth];
        for (int i = 0; i < path.Length; i++) path[i] = _sim.Crafter.Actions[0];

        do
        {
            if (AuditPresolve(path)) allowedPaths[path[0]].Add(path.ToArray(), 0);
        } while (PresolveIterator(ref path));

        foreach (var key in allowedPaths.Keys.Where(k => !allowedPaths[k].Any()))
        {
            allowedPaths.Remove(key); // remove keys that didn't make it into the presolver
        }

        return allowedPaths;
    }
    private bool PresolveIterator(ref int[] path, int ix = StepForwardDepth - 1)
    {
        if (ix == 0) Console.Write(".");
        
        if (ix == -1)
        {
            return false;
        }
        else if (path[ix] == _sim.Crafter.Actions[^1])
        {
            path[ix] = _sim.Crafter.Actions[0];
            return PresolveIterator(ref path, ix - 1);
        }
        else
        {
            for (int i = 0; i < _sim.Crafter.Actions.Length; i++)
            {
                if (_sim.Crafter.Actions[i] == path[ix])
                {
                    path[ix] = _sim.Crafter.Actions[i + 1];
                    return true;
                }
            }
            return false;
        }
    }
    private bool AuditPresolve(int[] path)
    {
        //var z = path.Select(x => Atlas.Actions.AllActions[x].ShortName).ToList();
        
        for (int i = 0; i < path.Length; i++)
        {
            if (i < path.Length - 1 && Atlas.Actions.Buffs.Contains(path[i]) && path[i] == path[i + 1]) return false;
            if (i > 0 && path[i] == (int)Atlas.Actions.ActionMap.BasicTouch && (path[i - 1] == (int)Atlas.Actions.ActionMap.StandardTouch || path[i - 1] == (int)Atlas.Actions.ActionMap.BasicTouch)) return false;
            if (i > 0 && path[i] == (int)Atlas.Actions.ActionMap.StandardTouch && path[i - 1] == (int)Atlas.Actions.ActionMap.StandardTouch) return false;
            if (Atlas.Actions.FirstRoundActions.Contains(path[i])) return false;
        }

        if (FindAction((int)Atlas.Actions.ActionMap.ByregotsBlessing, path).Count > 1) return false;

        List<int> indexes = FindAction((int)Atlas.Actions.ActionMap.WasteNot, path).Concat(FindAction((int)Atlas.Actions.ActionMap.WasteNot2, path)).ToList();
        if (indexes.Any(x => indexes.Any(y => x - y == 1 || x - y == 2))) return false;
        List<int> indexes2 = FindAction((int)Atlas.Actions.ActionMap.PrudentSynthesis, path).Concat(FindAction((int)Atlas.Actions.ActionMap.PrudentTouch, path)).ToList();
        foreach (int ix in indexes) if (indexes2.Any(x => x > ix && x - ix < Atlas.Actions.AllActions[path[ix]].ActiveTurns)) return false;

        indexes = FindAction((int)Atlas.Actions.ActionMap.Manipulation, path).ToList();
        if (indexes.Any(x => indexes.Any(y => x - y > 0 && x - y < 6))) return false;

        indexes = FindAction((int)Atlas.Actions.ActionMap.Veneration, path).ToList();
        for (int i = 0; i < indexes.Count; i++)
        {
            int duration = Math.Min(Atlas.Actions.AllActions[path[indexes[i]]].ActiveTurns, i < indexes.Count - 1 ? indexes[i + 1] - indexes[i] - 1 : int.MaxValue);
            if (path.Length > indexes[i] + duration && path.Skip(indexes[i] + 1).Take(duration).All(x => !Atlas.Actions.ProgressActions.Contains(x))) return false;
        }
        
        indexes = FindAction((int)Atlas.Actions.ActionMap.Innovation, path).ToList();
        for (int i = 0; i < indexes.Count; i++)
        {
            int duration = Math.Min(Atlas.Actions.AllActions[path[indexes[i]]].ActiveTurns, i < indexes.Count - 1 ? indexes[i + 1] - indexes[i] - 1 : int.MaxValue);
            if (path.Length > indexes[i] + duration && path.Skip(indexes[i] + 1).Take(duration).All(x => !Atlas.Actions.QualityActions.Contains(x))) return false;
        }
        
        int durability = 5, wn = 0, manip = 0;
        foreach (var ac in path)
        {
            Action action = Atlas.Actions.AllActions[ac];
            int cost = action.DurabilityCost;
            if (wn > 0 && cost > 0) cost = cost / 2;
            if (durability == _sim.Recipe.Durability && (ac == (int)Atlas.Actions.ActionMap.MastersMend || ac == (int)Atlas.Actions.ActionMap.ImmaculateMend)) return false;
            if (ac == (int)Atlas.Actions.ActionMap.WasteNot || ac == (int)Atlas.Actions.ActionMap.WasteNot2) wn = Math.Max(wn, action.ActiveTurns);
            if (ac == (int)Atlas.Actions.ActionMap.Manipulation) manip = Math.Max(manip, action.ActiveTurns);
            if (ac == (int)Atlas.Actions.ActionMap.MastersMend) durability += 30;
            if (ac == (int)Atlas.Actions.ActionMap.ImmaculateMend) durability += _sim.Recipe.Durability;
            durability = durability - cost;
            if (manip > 0) durability += 5;
            durability = Math.Min(durability, _sim.Recipe.Durability);
            wn--;
            manip--;
        }
        
        return true;
    }
    private List<int> FindAction(int action, int[] path)
    {
        List<int> indexes = new();
        for (int i = 0; i < path.Length; i++)
        {
            if (path[i] == action) indexes.Add(i);
        }
        return indexes;
    }

    public async Task<List<Action>> Run()
    {
        _sw.Start();

        int step = 0;
        do
        {
            var prevStep =
                (step == 0
                    ? _actions.Select(x => (-1D, new List<int> { x }, Array.Empty<int>())).Where(x => _sim.Simulate(x.Item2) is not null)
                    : _stepResults.OrderByDescending(x => x.Item1).Take(StepSize))
                .ToList();
            _stepResults.Clear();
            _countdown = new(1);
            _evaluated = 0;
            _failures = 0;
            _forwardSet = 0;

            List<Thread> extraThreads = new();
            for (int i = 0; i < prevStep.Count; i++)
            {
                Thread t = SolverThread(i, _sim.Simulate(prevStep[i].Item2), prevStep[i]);
                if (i < _threads.Length) _threads[i] = t;
                else extraThreads.Add(t);
                t.Start();
            }
        
            foreach (Thread t in extraThreads) await Task.Run(() => t.Join());
            _countdown.Signal();
            await Task.Run(() => _countdown.Wait());
            _logger($"[{DateTime.Now}, {_sw.ElapsedMilliseconds / 1000}s] [Step {step++ + 1}] {_evaluated:N0} evaluated - {_failures:N0} failures ({_failures / (double)_evaluated:P0}) || forward set: {_forwardSet:N0}");
        } while (_stepResults.Any() && _stepResults.First().Item2.Count < MaxDepth);

        //var bad = _presolve.SelectMany(x => x.Value.Where(y => y.Value == 0).Select(y => string.Join(", ", y.Key.Select(z => Atlas.Actions.AllActions[z].ShortName)))).ToList();
        return _bestSolution;
    }

    private Thread SolverThread(int threadId, LightState? lastState, (double, List<int>, int[]) prevStep) => new(() =>
    {
        _countdown.AddCount();

        var forward = StepForward(lastState, prevStep);
        _forwardSet += forward.Count;
        foreach (var item in forward) _stepResults.Add(item);
        
        if (threadId < _threads.Length) _threads[threadId] = null;
        _countdown.Signal();
    });

    private List<(double, List<int>, int[])> StepForward(LightState? lastState, (double, List<int>, int[]) prevStep)
    {
        List<(double, List<int>, int[])> ret = new();
        if (prevStep.Item3.Any())
        {
            double localBestScore = double.MinValue;
            List<int> prevExpansion = prevStep.Item3.Skip(1).ToList();
            List<int> prevPath = prevStep.Item2.Concat(prevExpansion).ToList();
            LightState? prevState = _sim.Simulate(prevExpansion, lastState!.Value);
            List<int> localBestPath = new();
            int[] localBestExpansion = Array.Empty<int>();
            foreach (var action in _actions)
            {
                LightState? s = _sim.Simulate(action, prevState.Value);
                if (s is null)
                {
                    _failures++;
                    continue;
                }

                KeyValuePair<double, LightState?> score = Score(s, action);
                List<int> path = prevPath.Concat(new[] { action }).ToList();
                if (score.Key >= _bestScore || score.Value!.Value.Success(_sim))
                {
                    lock (_locker)
                    {
                        if (score.Key <= _bestScore) continue;

                        _bestScore = score.Key;
                        _bestSolution = path.Select(x => Atlas.Actions.AllActions[x]).ToList();
                        var t = _sim.SimulateToFailure(path);
                        _logger($"\t{_bestScore:P} ({t.Item2?.Quality ?? 0:N0} / {_sim.Recipe.MaxQuality:N0} quality) {string.Join(", ", _bestSolution.Select(x => x.ShortName))}");
                    }
                }

                if (score.Key > localBestScore)
                {
                    localBestScore = score.Key;
                    localBestPath = path.ToList();
                    localBestExpansion = prevExpansion.Concat(new[] { action }).ToArray();
                }
            }

            if (localBestScore >= 0) ret.Add((localBestScore, localBestPath.Take(localBestPath.Count - StepBackDepth).ToList(), localBestExpansion));
        }

        foreach (var batch in _presolve)
        {
            double localBestScore = double.MinValue;
            List<int> localBestPath = new();
            int[] localBestExpansion = Array.Empty<int>();
            foreach (var b in batch.Value)
            {
                (int, LightState?) state = lastState.HasValue ? _sim.SimulateToFailure(b.Key, lastState.Value) : _sim.SimulateToFailure(b.Key);
                KeyValuePair<double, LightState?> score = Score(state.Item2, b.Key[0]);
                if (state.Item1 < StepForwardDepth) _failures++;
                if (score.Key < 0) continue; // this may not happen actually
                
                batch.Value[b.Key]++;
                if (score.Key <= localBestScore) continue;
                
                List<int> path = prevStep.Item2.Concat(b.Key).ToList();
                if (score.Key > _bestScore && score.Value!.Value.Success(_sim))
                {
                    lock (_locker)
                    {
                        if (score.Key <= _bestScore) continue;

                        _bestScore = score.Key;
                        _bestSolution = path.Select(x => Atlas.Actions.AllActions[x]).ToList();
                        var s = _sim.SimulateToFailure(path);
                        _logger($"\t{_bestScore:P} ({s.Item2?.Quality ?? 0:N0} / {_sim.Recipe.MaxQuality:N0} quality) {string.Join(", ", _bestSolution.Select(x => x.ShortName))}");
                    }
                }
                if (state.Item1 == StepForwardDepth && score.Key > localBestScore)
                {
                    localBestScore = score.Key;
                    localBestPath = path.ToList();
                    localBestExpansion = b.Key;
                }
            }
            if (localBestScore >= 0) ret.Add((localBestScore, localBestPath.Take(localBestPath.Count - StepBackDepth).ToList(), localBestExpansion));
        }

        return ret;
    }

    private KeyValuePair<double, LightState?> Score(LightState? state, int firstAction)
    {
        _evaluated++;
        if (!state.HasValue) return new(-1, null);
        
        double progress = Math.Min(_sim.Recipe.Difficulty, state.Value.Progress) / _sim.Recipe.Difficulty;

        double maxQuality = _sim.Recipe.MaxQuality * 1.1;
        double quality = Math.Min(maxQuality, state.Value.Quality) / maxQuality;
        if (firstAction == (int)Atlas.Actions.ActionMap.TrainedEye) quality = 1;

        double cp = state.Value.CP / _sim.Crafter.CP;
        double steps = 1 - state.Value.Step / 100D;

        return new((progress * 90 + quality * 150 + steps * 7 + cp * 3) / 250, state); // max 100
    }
}
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Linq;

namespace Libraries.Solvers;
using static Solver;

public class SawStepSolver
{
    private const int
        MaxThreads = 20,
        MaxDepth = 26,
        PresolveDepth = 5,
        StepForwardDepth = 4,
        StepBackDepth = StepForwardDepth - 1,
        StepSize = 100;

    private double _bestScore;
    private List<Action> _bestSolution;
    private long _evaluated, _failures, _forwardSet;

    private readonly LightSimulator _sim;
    private readonly LoggingDelegate _logger;
    private readonly int[] _actions;
    private readonly Dictionary<int, Dictionary<int[], int>>[] _presolve;

    private CountdownEvent _countdown = new(1);
    private readonly Thread?[] _threads = new Thread[MaxThreads];
    private readonly object _locker = new();
    private readonly Stopwatch _sw = new ();

    private readonly ConcurrentBag<KeyValuePair<double, List<int>>> _stepResults = new();

    public SawStepSolver(LightSimulator sim, LoggingDelegate loggingDelegate)
    {
        _sim = sim;
        _logger = loggingDelegate;
        _actions = sim.Crafter.Actions.OrderBy(x => x).ToArray();
        _bestSolution = new List<Action>();

        BigInteger maxProgressCount = BigInteger.Pow(_actions.Length, MaxDepth);
        BigInteger solverCount = BigInteger.Pow(_actions.Length, StepForwardDepth + 1);
        double presolverCount = Math.Pow(_sim.Crafter.Actions.Length, PresolveDepth);
        _logger($"[{DateTime.Now}] Game space is {maxProgressCount:N0} nodes, solver space is {solverCount:N0} nodes, presolver space is {presolverCount:N0} nodes");

        _presolve = new Dictionary<int, Dictionary<int[], int>>[PresolveDepth + 1];
        _presolve[PresolveDepth] = Presolve();
        for (int i = PresolveDepth - 1; i > 0; i--)
        {
            _presolve[i] = _presolve[i + 1].ToDictionary(
                x => x.Key,
                x => x.Value
                    .ToDictionary(y => y.Key.Take(i).ToArray(), y => y.Value)
                    .DistinctBy(y => string.Join(" ", y.Key))
                    .ToDictionary(y => y.Key, y => y.Value));
        }

        double expansionsFound = _presolve[PresolveDepth].Sum(x => x.Value.Count);
        _logger($"{expansionsFound:N0} expansions found (eliminated {presolverCount - expansionsFound:N0} possible expansions)");
    }

    private Dictionary<int, Dictionary<int[], int>> Presolve()
    {
        Dictionary<int, Dictionary<int[], int>> allowedPaths = new();
        foreach (int t in _sim.Crafter.Actions) allowedPaths.Add(t, new Dictionary<int[], int>());

        int[] path = new int[PresolveDepth];
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
    private bool PresolveIterator(ref int[] path, int ix = PresolveDepth - 1)
    {
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
            await DoNextStep(step++);
        } while (_stepResults.Any() && _stepResults.First().Value.Count < MaxDepth);

        //var bad = _presolve.SelectMany(x => x.Value.Where(y => y.Value == 0).Select(y => string.Join(", ", y.Key.Select(z => Atlas.Actions.AllActions[z].ShortName)))).ToList();
        
        return _bestSolution;
    }

    private async Task DoNextStep(int stepCount)
    {
        var prevStep = (stepCount == 0 ? _actions.Select(x => new List<int> { x }) : _stepResults.OrderByDescending(x => x.Key).Take(StepSize).Select(x => x.Value)).ToList();
        _stepResults.Clear();
        _countdown = new(1);
        _evaluated = 0;
        _failures = 0;
        _forwardSet = 0;

        List<Thread> extraThreads = new();
        for (int i = 0; i < prevStep.Count; i++)
        {
            Thread t = SolverThread(i, _sim.Simulate(prevStep[i]), prevStep[i]);
            if (i < _threads.Length) _threads[i] = t;
            else extraThreads.Add(t);
            t.Start();
        }
        
        foreach (Thread t in extraThreads) await Task.Run(() => t.Join());
        _countdown.Signal();
        await Task.Run(() => _countdown.Wait());
        _logger($"[{DateTime.Now}, {_sw.ElapsedMilliseconds / 1000}s] [Step {stepCount + 1}] {_evaluated:N0} evaluated - {_failures:N0} failures ({_failures / (double)_evaluated:P}) || forward set: {_forwardSet:N0}");
    }

    private Thread SolverThread(int threadId, LightState? lastState, List<int> path) => new(() =>
    {
        _countdown.AddCount();

        var forward = StepForward(lastState, path);
        _forwardSet += forward.Count;
        foreach (var item in forward) _stepResults.Add(item);
        
        if (threadId < _threads.Length) _threads[threadId] = null;
        _countdown.Signal();
    });

    private List<KeyValuePair<double, List<int>>> StepForward(LightState? lastState, List<int> path)
    {
        List<KeyValuePair<double, List<int>>> ret = new();
        
        foreach (var batch in _presolve[PresolveDepth])
        {
            double localBestScore = -1;
            List<int> localBestPath = new();
            foreach (var b in batch.Value)
            {
                List<int> expansion = path.Concat(b.Key).ToList();
                KeyValuePair<double, LightState?> score = Score(lastState, expansion);
                if (score.Key >= 0)
                {
                    batch.Value[b.Key]++;
                    if (score.Key > localBestScore)
                    {
                        localBestScore = score.Key;
                        localBestPath = expansion;
                        
                        if (score.Key > _bestScore && score.Value!.Value.Success(_sim))
                        {
                            lock (_locker)
                            {
                                if (score.Key > _bestScore)
                                {
                                    _bestScore = score.Key;
                                    _bestSolution = expansion.Select(x => Atlas.Actions.AllActions[x]).ToList();
                                    var s = _sim.Simulate(expansion);
                                    _logger($"\t{_bestScore:P} ({s?.Quality ?? 0:N0} / {_sim.Recipe.MaxQuality:N0} quality) {string.Join(", ", _bestSolution.Select(x => x.ShortName))}");
                                }
                            }
                        }
                    }
                }
                else
                {
                    //var q = expansion.Select(y => string.Join(", ", Atlas.Actions.AllActions[y].ShortName)).ToList();
                }
            }

            if (localBestScore >= 0) ret.Add(new(localBestScore, localBestPath.Take(localBestPath.Count - StepBackDepth).ToList()));
        }

        return ret;
    }
    private KeyValuePair<double, LightState?> Score(LightState? lastState, List<int> expansion)
    {
        _evaluated++;
        int step = lastState?.Step ?? 0;
        LightState? state = null;
        for (int i = step; i < expansion.Count; i++)
        {
            if (state.HasValue)
                state = _sim.Simulate(expansion[i], state.Value);
            else if (lastState.HasValue)
                state = _sim.Simulate(expansion[i], lastState.Value);
            else
                state = _sim.Simulate(expansion[i]);

            if (!state.HasValue || state.Value.Success(_sim)) break;
        }

        if (state.HasValue)
        {
            double progress = Math.Min(_sim.Recipe.Difficulty, state.Value.Progress) / _sim.Recipe.Difficulty;
            
            double maxQuality = _sim.Recipe.MaxQuality * 1.1;
            double quality = Math.Min(maxQuality, state.Value.Quality) / maxQuality;
            if (expansion[0] == (int)Atlas.Actions.ActionMap.TrainedEye) quality = 1; 

            double cp = state.Value.CP / _sim.Crafter.CP;
            double steps = 1 - state.Value.Step / 100D;

            return new((progress * 90 + quality * 150 + steps * 7 + cp * 3) / 250, state); // max 100
        }
        else
        {
            _failures++;
            return new(-1, state);
        }
    }
}
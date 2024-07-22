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
        StepForwardDepth = 5,
        StepBackDepth = StepForwardDepth - 1,
        StepSize = 20;

    private double _bestScore;
    private List<Action> _bestSolution;
    private long _evaluated, _failures, _forwardSet;

    private readonly LightSimulator _sim;
    private readonly LoggingDelegate _logger;
    private readonly short[] _actions;
    private readonly short[][] _presolve;

    private CountdownEvent _countdown = new(1);
    private readonly Thread?[] _threads = new Thread[MaxThreads];
    private readonly object _locker = new();
    private readonly Stopwatch _sw = new ();
    private readonly ConcurrentBag<(double, List<short>, short[])> _stepResults = new();

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

        double expansionsFound = _presolve.Length;
        _logger($"\n[{DateTime.Now}] {expansionsFound:N0} expansions found (eliminated {presolverCount - expansionsFound:N0} [{1 - expansionsFound / presolverCount:P0}] possible expansions)");
    }

    private short[][] Presolve()
    {
        List<short[]> allowedPaths = new();

        short[] path = new short[StepForwardDepth];
        for (int i = 0; i < path.Length; i++) path[i] = (short)_sim.Crafter.Actions[0];

        do
        {
            if (AuditPresolve(path)) allowedPaths.Add(path.ToArray());
        } while (PresolveIterator(ref path));
        //allowedPaths.Sort();

        return allowedPaths.ToArray();
    }
    private bool PresolveIterator(ref short[] path, int ix = StepForwardDepth - 1)
    {
        if (ix == 0) Console.Write(".");
        
        if (ix == -1)
        {
            return false;
        }
        else if (path[ix] == _sim.Crafter.Actions[^1])
        {
            path[ix] = (short)_sim.Crafter.Actions[0];
            return PresolveIterator(ref path, ix - 1);
        }
        else
        {
            for (int i = 0; i < _sim.Crafter.Actions.Length; i++)
            {
                if (_sim.Crafter.Actions[i] == path[ix])
                {
                    path[ix] = (short)_sim.Crafter.Actions[i + 1];
                    return true;
                }
            }
            return false;
        }
    }
    private bool AuditPresolve(short[] path)
    {
        //var z = path.Select(x => Atlas.Actions.AllActions[x].ShortName).ToList();
        
        for (int i = 0; i < path.Length; i++)
        {
            if (i < path.Length - 1 && Atlas.Actions.Buffs.Contains(path[i]) && path[i] == path[i + 1]) return false;
            if (i > 0 && path[i] == (short)Atlas.Actions.ActionMap.BasicTouch && (path[i - 1] == (short)Atlas.Actions.ActionMap.StandardTouch || path[i - 1] == (short)Atlas.Actions.ActionMap.BasicTouch)) return false;
            if (i > 0 && path[i] == (short)Atlas.Actions.ActionMap.StandardTouch && path[i - 1] == (short)Atlas.Actions.ActionMap.StandardTouch) return false;
            if (Atlas.Actions.FirstRoundActions.Contains(path[i])) return false;
        }

        if (FindAction((short)Atlas.Actions.ActionMap.ByregotsBlessing, path).Count > 1) return false;

        List<int> indexes = FindAction((short)Atlas.Actions.ActionMap.WasteNot, path).Concat(FindAction((short)Atlas.Actions.ActionMap.WasteNot2, path)).ToList();
        if (indexes.Any(x => indexes.Any(y => x - y == 1 || x - y == 2))) return false;
        List<int> indexes2 = FindAction((short)Atlas.Actions.ActionMap.PrudentSynthesis, path).Concat(FindAction((short)Atlas.Actions.ActionMap.PrudentTouch, path)).ToList();
        foreach (int ix in indexes) if (indexes2.Any(x => x > ix && x - ix < Atlas.Actions.AllActions[path[ix]].ActiveTurns)) return false;

        indexes = FindAction((short)Atlas.Actions.ActionMap.Manipulation, path).ToList();
        if (indexes.Any(x => indexes.Any(y => x - y > 0 && x - y < 6))) return false;

        indexes = FindAction((short)Atlas.Actions.ActionMap.Veneration, path).ToList();
        for (int i = 0; i < indexes.Count; i++)
        {
            int duration = Math.Min(Atlas.Actions.AllActions[path[indexes[i]]].ActiveTurns, i < indexes.Count - 1 ? indexes[i + 1] - indexes[i] - 1 : int.MaxValue);
            if (path.Length > indexes[i] + duration && path.Skip(indexes[i] + 1).Take(duration).All(x => !Atlas.Actions.ProgressActions.Contains(x))) return false;
        }
        
        indexes = FindAction((short)Atlas.Actions.ActionMap.Innovation, path).ToList();
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
            if (wn > 0 && cost > 0) cost /= 2;
            if (durability == _sim.Recipe.Durability && ac is (short)Atlas.Actions.ActionMap.MastersMend or (short)Atlas.Actions.ActionMap.ImmaculateMend) return false;
            if (ac is (short)Atlas.Actions.ActionMap.WasteNot or (short)Atlas.Actions.ActionMap.WasteNot2) wn = Math.Max(wn, action.ActiveTurns);
            if (ac == (short)Atlas.Actions.ActionMap.Manipulation) manip = Math.Max(manip, action.ActiveTurns);
            if (ac == (short)Atlas.Actions.ActionMap.MastersMend) durability += 30;
            if (ac == (short)Atlas.Actions.ActionMap.ImmaculateMend) durability += _sim.Recipe.Durability;
            durability -= cost;
            if (manip > 0) durability += 5;
            durability = Math.Min(durability, _sim.Recipe.Durability);
            wn--;
            manip--;
        }
        
        return true;
    }
    private List<int> FindAction(short action, short[] path)
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
                    ? _actions.Select(x => (-1D, new List<short> { x }, Array.Empty<short>())).Where(x => _sim.Simulate(x.Item2) is not null)
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

    private Thread SolverThread(int threadId, LightState? lastState, (double, List<short>, short[]) prevStep) => new(() =>
    {
        if (_countdown.IsSet) return;
        _countdown.AddCount();

        var forward = StepForward(lastState, prevStep);
        _forwardSet += forward.Count;
        foreach (var item in forward) _stepResults.Add(item);
        
        if (threadId < _threads.Length) _threads[threadId] = null;
        _countdown.Signal();
    });

    private List<(double, List<short>, short[])> StepForward(LightState? lastState, (double, List<short>, short[]) prevStep)
    {
        double localBestScore = double.MinValue;;
        List<short> localBestPath = new();
        short[] localBestExpansion = Array.Empty<short>();
        
        List<(double, List<short>, short[])> ret = new();
        if (prevStep.Item3.Any())
        {
            List<short> prevExpansion = prevStep.Item3.Skip(1).ToList();
            List<short> prevPath = prevStep.Item2.Concat(prevExpansion).ToList();
            LightState? prevState = _sim.Simulate(prevExpansion, lastState!.Value);
            foreach (var action in _actions)
            {
                LightState? s = _sim.Simulate(action, prevState.Value);
                if (s is null)
                {
                    _failures++;
                    continue;
                }

                KeyValuePair<double, LightState?> score = Score(s, action);
                List<short> path = prevPath.Concat(new[] { action }).ToList();
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
        }

        short prevKey = -1, key;
        foreach (var batch in _presolve)
        {
            key = batch[0];
            if (prevKey != key)
            {
                if (localBestScore >= 0) ret.Add((localBestScore, localBestPath.Take(localBestPath.Count - StepBackDepth).ToList(),  localBestExpansion));

                localBestScore = double.MinValue;
                localBestPath = new();
                localBestExpansion = Array.Empty<short>();
                prevKey = key;
            }

            (int, LightState?) state = lastState.HasValue
                ? _sim.SimulateToFailure(batch, lastState.Value)
                : _sim.SimulateToFailure(batch);
            KeyValuePair<double, LightState?> score = Score(state.Item2, key);
            if (state.Item1 < StepForwardDepth) _failures++;

            if (score.Key <= localBestScore) continue;

            if (score.Key > _bestScore && score.Value!.Value.Success(_sim))
            {
                lock (_locker)
                {
                    if (score.Key <= _bestScore) continue;
                    short[] path = prevStep.Item2.Concat(batch).ToArray();

                    _bestScore = score.Key;
                    _bestSolution = path.Select(x => Atlas.Actions.AllActions[x]).ToList();
                    var s = _sim.SimulateToFailure(path);
                    _logger($"\t{_bestScore:P} ({s.Item2?.Quality ?? 0:N0} / {_sim.Recipe.MaxQuality:N0} quality) {string.Join(", ", _bestSolution.Select(x => x.ShortName))}");
                }
            }

            if (state.Item1 == StepForwardDepth && score.Key > localBestScore)
            {
                localBestScore = score.Key;
                localBestPath = prevStep.Item2.Concat(batch).ToList();
                localBestExpansion = batch.ToArray();
            }
        }
        if (localBestScore >= 0) ret.Add((localBestScore, localBestPath.Take(localBestPath.Count - StepBackDepth).ToList(),  localBestExpansion));

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
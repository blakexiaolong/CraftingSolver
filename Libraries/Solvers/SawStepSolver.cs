using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;

namespace Libraries.Solvers;
using static Solver;

public class SawStepSolver
{
    private const int
        MaxThreads = 20,
        MaxDepth = 15,
        StepForwardDepth = 6,
        StepSize = 5;

    private double _bestScore;
    private List<Action> _bestSolution;
    private long _evaluated, _failures, _skipped , _forwardSet;
    private long _totalEvaluated, _totalFailures, _totalSkipped, _nodesEvaluated;

    private readonly LightSimulator _sim;
    private readonly LoggingDelegate _logger;
    private readonly byte[] _actions;
    private readonly byte[][] _presolve;

    private CountdownEvent _countdown = new(1);
    private readonly Thread?[] _threads = new Thread[MaxThreads];
    private readonly object _locker = new();
    private readonly Stopwatch _sw = new ();
    private readonly ConcurrentBag<(double, List<byte>, byte[])> _stepResults = new();

    public SawStepSolver(LightSimulator sim, LoggingDelegate loggingDelegate)
    {
        _sim = sim;
        _logger = loggingDelegate;
        _actions = sim.Crafter.Actions.OrderBy(x => x).ToArray();
        _bestSolution = new List<Action>();

        BigInteger gameSpace = BigInteger.Pow(_actions.Length, MaxDepth);
        BigInteger solverSpace = BigInteger.Pow(_actions.Length, StepForwardDepth);
        _logger($"[{DateTime.Now}] Game space is {gameSpace:N0} nodes, solver space is {solverSpace:N0} nodes");

        // TODO: Maybe yank this out into a database somehow?
        Console.Write("Pre-solving");
        _presolve = Presolve();

        double expansionsFound = _presolve.Length;
        _logger($"\n[{DateTime.Now}] {expansionsFound:N0} expansions found (eliminated {(double)solverSpace - expansionsFound:N0} [{1 - expansionsFound / (double)solverSpace:P0}] possible expansions)");
    }

    private byte[][] Presolve()
    {
        List<byte[]> allowedPaths = new();

        byte[] path = new byte[StepForwardDepth];
        for (int i = 0; i < path.Length; i++) path[i] = _sim.Crafter.Actions[0];

        do
        {
            if (AuditPresolve(path)) allowedPaths.Add(path.ToArray());
        } while (PresolveIterator(ref path));

        return allowedPaths.ToArray();
    }

    private bool PresolveIterator(ref byte[] path, int ix = StepForwardDepth - 1)
    {
        while (true)
        {
            if (ix == 0) Console.Write(".");

            if (ix == -1) return false;
            if (path[ix] == _sim.Crafter.Actions[^1])
            {
                path[ix] = _sim.Crafter.Actions[0];
                ix -= 1;
                continue;
            }

            for (int i = 0; i < _sim.Crafter.Actions.Length; i++)
            {
                if (_sim.Crafter.Actions[i] != path[ix]) continue;

                path[ix] = _sim.Crafter.Actions[i + 1];
                return true;
            }

            return false;
        }
    }

    private bool AuditPresolve(byte[] path)
    {
        for (int i = 0; i < path.Length; i++)
        {
            if (i < path.Length - 1 && Atlas.Actions.Buffs.Contains(path[i]) && path[i] == path[i + 1]) return false;
            if (i > 0 && path[i] == (byte)Atlas.Actions.ActionMap.BasicTouch && (path[i - 1] == (byte)Atlas.Actions.ActionMap.StandardTouch || path[i - 1] == (byte)Atlas.Actions.ActionMap.BasicTouch)) return false;
            if (i > 0 && path[i] == (byte)Atlas.Actions.ActionMap.StandardTouch && path[i - 1] == (byte)Atlas.Actions.ActionMap.StandardTouch) return false;
            if (Atlas.Actions.FirstRoundActions.Contains(path[i])) return false;
        }

        if (FindAction((byte)Atlas.Actions.ActionMap.ByregotsBlessing, path).Count > 1) return false;

        List<int> indexes = FindAction((byte)Atlas.Actions.ActionMap.WasteNot, path).Concat(FindAction((byte)Atlas.Actions.ActionMap.WasteNot2, path)).ToList();
        if (indexes.Any(x => indexes.Any(y => x - y == 1 || x - y == 2))) return false;
        List<int> indexes2 = FindAction((byte)Atlas.Actions.ActionMap.PrudentSynthesis, path).Concat(FindAction((byte)Atlas.Actions.ActionMap.PrudentTouch, path)).ToList();
        foreach (int ix in indexes) if (indexes2.Any(x => x > ix && x - ix < Atlas.Actions.AllActions[path[ix]].ActiveTurns)) return false;

        indexes = FindAction((byte)Atlas.Actions.ActionMap.Manipulation, path);
        if (indexes.Any(x => indexes.Any(y => x - y > 0 && x - y < 6))) return false;

        indexes = FindAction((byte)Atlas.Actions.ActionMap.Veneration, path);
        for (int i = 0; i < indexes.Count; i++)
        {
            int duration = Math.Min(Atlas.Actions.AllActions[path[indexes[i]]].ActiveTurns, i < indexes.Count - 1 ? indexes[i + 1] - indexes[i] - 1 : int.MaxValue);
            if (path.Length > indexes[i] + duration && path.Skip(indexes[i] + 1).Take(duration).All(x => !Atlas.Actions.ProgressActions.Contains(x))) return false;
        }
        
        indexes = FindAction((byte)Atlas.Actions.ActionMap.Innovation, path);
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
            if (durability == _sim.Recipe.Durability && ac is (byte)Atlas.Actions.ActionMap.MastersMend or (byte)Atlas.Actions.ActionMap.ImmaculateMend) return false;
            if (ac is (byte)Atlas.Actions.ActionMap.WasteNot or (byte)Atlas.Actions.ActionMap.WasteNot2) wn = Math.Max(wn, action.ActiveTurns);
            if (ac == (byte)Atlas.Actions.ActionMap.Manipulation) manip = Math.Max(manip, action.ActiveTurns);
            if (ac == (byte)Atlas.Actions.ActionMap.MastersMend) durability += 30;
            if (ac == (byte)Atlas.Actions.ActionMap.ImmaculateMend) durability += _sim.Recipe.Durability;
            durability -= cost;
            if (manip > 0) durability += 5;
            durability = Math.Min(durability, _sim.Recipe.Durability);
            wn--;
            manip--;
        }
        
        return true;
    }
    private List<int> FindAction(byte action, byte[] path)
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
        List<(double, List<byte>, byte[])> prevStep = _actions
            .Select(x => (-1D, new List<byte> { x }, Array.Empty<byte>()))
            .Where(x => !_sim.Simulate(x.Item2).IsError)
            .ToList();

        _totalEvaluated = 0;
        _totalFailures = 0;
        _totalSkipped = 0;
        _nodesEvaluated = 0;
        do
        {
            _stepResults.Clear();
            _countdown = new(1);
            _evaluated = 0;
            _failures = 0;
            _skipped = 0;
            _forwardSet = 0;

            List<Thread> extraThreads = new();
            for (int i = 0; i < prevStep.Count; i++)
            {
                Thread t = SolverThread(i, prevStep[i]);
                if (i < _threads.Length) _threads[i] = t;
                else extraThreads.Add(t);
                t.Start();
            }

            foreach (Thread t in extraThreads) await Task.Run(() => t.Join());

            _countdown.Signal();
            await Task.Run(() => _countdown.Wait());
            _logger($"[{DateTime.Now}, {_sw.ElapsedMilliseconds / 1000}s] [Step {step++ + 1}] {_skipped:N0} skipped ({(double)_skipped / (_evaluated + _skipped):P0}) - {_evaluated:N0} evaluated ({(double)_evaluated / (_evaluated + _skipped):P0}) - {_failures:N0} failures ({(double)_failures / (_evaluated + _skipped):P0}) >> {_forwardSet:N0}");
            _nodesEvaluated += prevStep.Count;

            prevStep = _stepResults
                .OrderByDescending(x => x.Item1)
                .Take(StepSize)
                .ToList();
            _totalEvaluated += _evaluated;
            _totalFailures += _failures;
            _totalSkipped += _skipped;
        } while (_stepResults.Any() && _stepResults.First().Item2.Count < MaxDepth - StepForwardDepth);

        //var bad = _presolve.SelectMany(x => x.Value.Where(y => y.Value == 0).Select(y => string.Join(", ", y.Key.Select(z => Atlas.Actions.AllActions[z].byteName)))).ToList();
        _logger($"[{DateTime.Now}, {_sw.ElapsedMilliseconds / 1000}s] " +
                $"{_totalSkipped:N0} skipped ({(double)_totalSkipped / (_totalEvaluated + _totalSkipped):P0}) " +
                $"{_totalEvaluated:N0} evaluated ({(double)_totalEvaluated / (_totalEvaluated + _totalSkipped):P0}) - " +
                $"{_totalFailures:N0} failures ({(double)_totalFailures / (_totalEvaluated + _totalSkipped):P0}) - " +
                $"{_nodesEvaluated * Math.Pow(_actions.Length, StepForwardDepth):N0} nodes " +
                $"(~{_nodesEvaluated * Math.Pow(_actions.Length, StepForwardDepth) / Math.Pow(_actions.Length, MaxDepth):P12} of game space) evaluated");
        return _bestSolution;
    }

    private Thread SolverThread(int threadId, (double, List<byte>, byte[]) prevStep) => new(() =>
    {
        if (_countdown.IsSet) return;
        _countdown.AddCount();

        ResetLocals(out double localBestScore, out var localBestPath, out var localBestExpansion);
        List<(double, List<byte>, byte[])> forward = new();
        LightState prevState = _sim.Simulate(prevStep.Item2);
        
        #region Handle Previous Expansion
        if (prevStep.Item3.Any())
        {
            List<byte> prevExpansion = prevStep.Item3.Skip(1).ToList();
            LightState expansionState = _sim.Simulate(prevExpansion, prevState);
            foreach (var action in _actions)
            {
                LightState s = _sim.Simulate(action, expansionState);
                _evaluated++;
                if (s.IsError)
                {
                    _failures++;
                    continue;
                }

                double score = Score(s, action);
                byte[] batch = prevExpansion.Concat(new[] { action }).ToArray();
                ConfirmHighScore(score, s.Success(_sim), prevStep, batch);
                PreserveState(score, ref localBestScore, ref localBestPath, ref localBestExpansion, prevStep, batch);
            }
        }
        #endregion

        #region Handle New Expansions
        byte prevKey = byte.MaxValue;
        int skipIx = -1; byte skipKey = 0;
        foreach (var preSolution in _presolve)
        {
            switch (skipIx)
            {
                case >= 0 when preSolution[skipIx] == skipKey:
                    _skipped += 1;
                    continue; // fast-forward
                case >= 0:
                    skipIx = -1;
                    break; // record scratch
            }
            
            byte key = preSolution[0];
            if (prevKey != key)
            {
                if (localBestScore >= 0) forward.Add((localBestScore, localBestPath.Take(prevStep.Item2.Count+1).ToList(),  localBestExpansion.ToArray()));
                ResetLocals(out localBestScore, out localBestPath, out localBestExpansion);
                prevKey = key;
            }

            LightState state = _sim.SimulateToFailure(preSolution, prevState);
            _evaluated++;
            int stepsTaken = state.Step - prevState.Step;
            if (stepsTaken < StepForwardDepth)
            {
                _failures++;
                skipIx = stepsTaken;
                skipKey = preSolution[stepsTaken];
                continue;
            }
            double score = Score(state, key);

            if (score <= localBestScore) continue;
            ConfirmHighScore(score, state.Success(_sim), prevStep, preSolution);
            PreserveState(score, ref localBestScore, ref localBestPath, ref localBestExpansion, prevStep, preSolution);
        }

        if (localBestScore >= 0) forward.Add((localBestScore, localBestPath.Take(prevStep.Item2.Count + 1).ToList(), localBestExpansion.ToArray()));
        #endregion

        _forwardSet += forward.Count;
        foreach (var item in forward.OrderByDescending(x => x.Item1).Take(StepSize)) _stepResults.Add(item);
        
        if (threadId < _threads.Length) _threads[threadId] = null;
        _countdown.Signal();
    });

    private double Score(LightState state, int firstAction)
    {
        double progress = Math.Min(_sim.Recipe.Difficulty, state.Progress) / _sim.Recipe.Difficulty;

        double maxQuality = _sim.Recipe.MaxQuality * 1.1;
        double quality = Math.Min(maxQuality, state.Quality) / maxQuality;
        if (firstAction == (int)Atlas.Actions.ActionMap.TrainedEye) quality = 1;

        double cp = state.CP / _sim.Crafter.CP;
        double steps = 1 - state.Step / 100D;

        return (progress * 90 + quality * 150 + steps * 9 + cp * 1) / 250; // max 100
    }

    private void ResetLocals(out double localBestScore, out IEnumerable<byte> localBestPath, out IEnumerable<byte> localBestExpansion)
    {
        localBestScore = double.MinValue;
        localBestPath = Array.Empty<byte>();
        localBestExpansion = Array.Empty<byte>();
    }
    private void PreserveState(double score, ref double localBestScore, ref IEnumerable<byte> localBestPath, ref IEnumerable<byte> localBestExpansion, (double, List<byte>, byte[]) prevStep, byte[] batch)
    {
        if (score <= localBestScore) return;
        
        localBestScore = score;
        localBestPath = prevStep.Item2.Concat(batch);
        localBestExpansion = batch;
        
    }
    private void ConfirmHighScore(double score, bool success, (double, List<byte>, byte[]) prevStep, IEnumerable<byte> batch)
    {
        if (score <= _bestScore || !success) return;
        
        lock (_locker)
        {
            if (score <= _bestScore) return;
            byte[] path = prevStep.Item2.Concat(batch).ToArray();

            _bestScore = score;
            _bestSolution = path.Select(x => Atlas.Actions.AllActions[x]).ToList();
            LightState s = _sim.SimulateToFailure(path);
            _logger($"\t{_bestScore:P} ({s.Quality:N0} / {_sim.Recipe.MaxQuality:N0} quality) {string.Join(", ", _bestSolution.Select(x => x.ShortName))}");
        }
    }
}
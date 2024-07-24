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
        StepSize = 20;

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
        GC.Collect();

        double expansionsFound = _presolve.Length;
        _logger($"\n[{DateTime.Now}] {expansionsFound:N0} expansions found (eliminated {(double)solverSpace - expansionsFound:N0} [{1 - expansionsFound / (double)solverSpace:P0}] possible expansions)");
    }

    private byte[][] Presolve()
    {
        List<byte[]> allowedPaths = new();

        int skipIx = -1, skipKey = 0;
        long skipped = 0;
        byte[] path = new byte[StepForwardDepth];
        byte[] actions = _sim.Crafter.Actions.Where(x => !Atlas.Actions.FirstRoundActions.Contains(x)).ToArray();
        for (int i = 0; i < path.Length; i++) path[i] = actions[0];

        //TODO: multithread
        do
        {
            if (skipIx >= 0 && path[skipIx] == skipKey) continue; // nyoooom
            else if (skipIx >= 0) skipIx = -1; // resume presolving
            
            (bool, int) audit = AuditPresolve(path);
            if (audit.Item1)
            {
                allowedPaths.Add(path.ToArray());
            }
            else
            {
                skipIx = audit.Item2;
                skipKey = path[skipIx];
                skipped++;
            }
        } while (PresolveIterator(ref path, actions));

        Console.WriteLine($"\nPresolved - {skipped:N0} elements were proactively skipped");
        return allowedPaths.ToArray();
    }
    private bool PresolveIterator(ref byte[] path, byte[] allowedActions, int ix = StepForwardDepth - 1)
    {
        while (true)
        {
            if (ix == 0) Console.Write(".");

            if (ix == -1) return false;
            if (path[ix] == allowedActions[^1])
            {
                path[ix] = allowedActions[0];
                ix -= 1;
                continue;
            }

            for (int i = 0; i < allowedActions.Length; i++)
            {
                if (allowedActions[i] != path[ix]) continue;

                path[ix] = allowedActions[i + 1];
                return true;
            }

            return false;
        }
    }
    private (bool,int) AuditPresolve(byte[] path)
    {
        int durability = 5, wn = 0, manip = 0;
        int lastWasteNot = -1, lastManip = -1, innovation = -1, veneration = -1;
        bool byregotsUsed = false;
        for (int i = 0; i < path.Length; i++)
        {
            var action = Atlas.Actions.AllActions[path[i]];
            if (i > 0)
            {
                if (Atlas.Actions.Buffs.Contains(path[i]) && path[i] == path[i - 1]) return (false, i); // repeated buff
                if (path[i] == (byte)Atlas.Actions.ActionMap.BasicTouch && path[i - 1] is (byte)Atlas.Actions.ActionMap.BasicTouch or (byte)Atlas.Actions.ActionMap.StandardTouch) return (false, i); // bad ordering
                if (path[i] == (byte)Atlas.Actions.ActionMap.ByregotsBlessing)
                {
                    if (byregotsUsed) return (false, i); // not a good idea
                    byregotsUsed = true;
                }
            }
            if (Atlas.Actions.FirstRoundActions.Contains(path[i])) return (false, i); // first round actions aren't allowed
            int cost = action.DurabilityCost;
            if (wn > 0 && cost > 0) cost /= 2;
            if (durability == _sim.Recipe.Durability && path[i] is (byte)Atlas.Actions.ActionMap.MastersMend or (byte)Atlas.Actions.ActionMap.ImmaculateMend) return (false, i);
            
            if (path[i] is (byte)Atlas.Actions.ActionMap.WasteNot or (byte)Atlas.Actions.ActionMap.WasteNot2)
            {
                if (lastWasteNot >= 0 && i - lastWasteNot <= 2) return (false, i); // super wasteful
                wn = Math.Max(wn, action.ActiveTurns);
                lastWasteNot = i;
            }
            else if (path[i] == (byte)Atlas.Actions.ActionMap.Manipulation)
            {
                if (lastManip >= 0 && i - lastManip <= 5) return (false, i); // not worth the GP
                manip = Math.Max(manip, action.ActiveTurns);
                lastManip = i;
            }
            else if (path[i] == (byte)Atlas.Actions.ActionMap.MastersMend) durability += 30;
            else if (path[i] == (byte)Atlas.Actions.ActionMap.ImmaculateMend) durability += _sim.Recipe.Durability;
            else if (path[i] == (byte)Atlas.Actions.ActionMap.PrudentTouch || path[i]==(byte)Atlas.Actions.ActionMap.PrudentSynthesis)
            {
                if (wn > 0) return (false, i); // can't use this action
            }
            else if (path[i] == (byte)Atlas.Actions.ActionMap.Veneration)
            {
                veneration = action.ActiveTurns + 1;
            }
            else if (path[i] == (byte)Atlas.Actions.ActionMap.Innovation)
            {
                innovation = action.ActiveTurns + 1;
            }

            // intentionally seperated from above
            if (innovation > 0 && action.QualityIncreaseMultiplier > 0)
            {
                innovation = 0; // this is only tracking if the buff has been used
            }
            else if (innovation == 0) return (false, i); // innovation fell off without being used

            // intentionally seperated from above
            if (veneration > 0 && action.ProgressIncreaseMultiplier > 0)
            {
                veneration = 0; // this is only tracking if the buff has been used
            }
            else if (veneration == 0) return (false, i); // veneration fell off without being used
            
            durability -= cost;
            if (manip > 0) durability += 5;
            durability = Math.Min(durability, _sim.Recipe.Durability);
            wn--;
            manip--;
            innovation--;
            veneration--;
        }

        return (true, 0);
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
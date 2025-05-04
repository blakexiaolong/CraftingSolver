using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;

namespace Libraries.Solvers;
using static Solver;

public class SawStepSolver
{
    private const int
        MaxThreads = 20,
        MaxDepth = 30,
        StepForwardDepth = 3,
        StepSize = 100;

    private double _bestScore;
    private List<Action> _bestSolution;
    private long _presolveFound, _evaluated, _failures, _skipped , _forwardSet;
    private long _totalEvaluated, _totalFailures, _totalSkipped, _nodesEvaluated;

    private readonly LightSimulator _sim;
    private readonly LoggingDelegate _logger;
    private readonly byte[] _actions;
    private readonly byte[][] _presolve;

    private Dictionary<int, StateNode>[] _tree;
    [Flags]
    public enum StateNode
    {
        TrainedPerfection = 1<<0,
        Groundwork = 1<<1,
        PreparatoryTouch = 1<<2,
        Veneration = 1<<3,
        Innovation = 1<<4,
        GreatStrides = 1<<5,
        BasicSynth = 1<<6,
        CarefulSynthesis = 1<<7,
        BasicTouch = 1<<8,
        StandardTouch = 1<<9,
        AdvancedTouch = 1<<10,
        RefinedTouch = 1<<11,
        ByregotsBlessing = 1<<12,
        ImmaculateMend = 1<<13,
        MastersMend = 1<<14,
        Manipulation = 1<<15,
        WasteNot = 1<<16,
        WasteNot2 = 1<<17,
        PrudentTouch = 1<<18,
        DelicateSynthesis = 1<<19,
        TrainedFinesse = 1<<20,
        PrudentSynthesis = 1<<21,
        Observe = 1<<22,
        
        TrainedEye = 1<<23,
        Reflect = 1<<24,
        MuscleMemory = 1<<25,
    }
    private Dictionary<byte, int> actionToState = new()
    {
        { (int)Atlas.Actions.ActionMap.TrainedPerfection, (int)StateNode.TrainedPerfection },
        { (int)Atlas.Actions.ActionMap.Groundwork, (int)StateNode.Groundwork },
        { (int)Atlas.Actions.ActionMap.PreparatoryTouch, (int)StateNode.PreparatoryTouch },
        { (int)Atlas.Actions.ActionMap.Veneration, (int)StateNode.Veneration },
        { (int)Atlas.Actions.ActionMap.Innovation, (int)StateNode.Innovation },
        { (int)Atlas.Actions.ActionMap.GreatStrides, (int)StateNode.GreatStrides },
        { (int)Atlas.Actions.ActionMap.BasicSynth, (int)StateNode.BasicSynth },
        { (int)Atlas.Actions.ActionMap.CarefulSynthesis, (int)StateNode.CarefulSynthesis },
        { (int)Atlas.Actions.ActionMap.BasicTouch, (int)StateNode.BasicTouch },
        { (int)Atlas.Actions.ActionMap.StandardTouch, (int)StateNode.StandardTouch },
        { (int)Atlas.Actions.ActionMap.AdvancedTouch, (int)StateNode.AdvancedTouch },
        { (int)Atlas.Actions.ActionMap.RefinedTouch, (int)StateNode.RefinedTouch },
        { (int)Atlas.Actions.ActionMap.ByregotsBlessing, (int)StateNode.ByregotsBlessing },
        { (int)Atlas.Actions.ActionMap.ImmaculateMend, (int)StateNode.ImmaculateMend },
        { (int)Atlas.Actions.ActionMap.MastersMend, (int)StateNode.MastersMend },
        { (int)Atlas.Actions.ActionMap.Manipulation, (int)StateNode.Manipulation },
        { (int)Atlas.Actions.ActionMap.WasteNot, (int)StateNode.WasteNot },
        { (int)Atlas.Actions.ActionMap.WasteNot2, (int)StateNode.WasteNot2 },
        { (int)Atlas.Actions.ActionMap.PrudentTouch, (int)StateNode.PrudentTouch },
        { (int)Atlas.Actions.ActionMap.DelicateSynthesis, (int)StateNode.DelicateSynthesis },
        { (int)Atlas.Actions.ActionMap.TrainedFinesse, (int)StateNode.TrainedFinesse },
        { (int)Atlas.Actions.ActionMap.PrudentSynthesis, (int)StateNode.PrudentSynthesis },
        { (int)Atlas.Actions.ActionMap.Observe, (int)StateNode.Observe },
        { (int)Atlas.Actions.ActionMap.TrainedEye, (int)StateNode.TrainedEye },
        { (int)Atlas.Actions.ActionMap.Reflect, (int)StateNode.Reflect },
        { (int)Atlas.Actions.ActionMap.MuscleMemory, (int)StateNode.MuscleMemory },
    };
    private Dictionary<int, byte> stateToAction;

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
        _logger($"[{DateTime.Now}] Game space is {gameSpace:N0} nodes, solver space [{StepForwardDepth}] is {solverSpace:N0} nodes (~1 / {gameSpace / solverSpace:N0})");
        
        
        Console.Write("Pre-solving");
        stateToAction = actionToState.ToDictionary(x => x.Value, x => x.Key);
        _presolve = Presolve(); // TODO: Make it so the tree is just generated
        GenerateTree2();
        GC.Collect();

        _logger($"\n[{DateTime.Now}] {_presolveFound:N0} expansions found (eliminated {(double)solverSpace - _presolveFound:N0} [{1 - _presolveFound / (double)solverSpace:P0}] possible expansions)");
    }

    #region Presolving
    private byte[][] Presolve()
    {
        object presolveLock = new();
        List<byte[]> allowedPaths = new();

        long skipped = 0;
        byte[] actions = _sim.Crafter.Actions.Where(x => !Atlas.Actions.FirstRoundActions.Contains(x)).ToArray();

        List<Thread> presolverThreads = new();
        foreach (var t in actions)
        {
            byte[] path = new byte[StepForwardDepth];
            path[0] = t;
            for (int j = 1; j < path.Length; j++) path[j] = actions[0];

            presolverThreads.Add(new Thread(() =>
            {
                List<byte[]> ret = new();
                int skipIx = -1, skipKey = 0;
                long f = 0;

                do
                {
                    if (skipIx >= 0 && path[skipIx] == skipKey) continue; // nyoooom
                    else if (skipIx >= 0) skipIx = -1; // resume presolving

                    var audit = AuditPresolve(path);
                    if (audit.Item1)
                    {
                        f += 1;
                        ret.Add(path.ToArray());
                    }
                    else
                    {
                        skipIx = audit.Item2;
                        skipKey = path[skipIx];
                        skipped += (long)BigInteger.Pow(actions.Length, StepForwardDepth - 1 - skipIx) - 1;
                    }
                } while (PresolveIterator(ref path, actions));

                Console.Write(".");
                lock (presolveLock)
                {
                    _presolveFound += f;
                    allowedPaths.AddRange(ret);
                }
            }));
        }
        foreach (var t in presolverThreads) t.Start();
        foreach (var t in presolverThreads) t.Join();

        Console.WriteLine($"\nPresolved - {skipped:N0} elements were proactively skipped");
        return allowedPaths.ToArray();
    }
    private bool PresolveIterator(ref byte[] path, byte[] allowedActions, int ix = StepForwardDepth - 1)
    {
        while (true)
        {
            if (ix == 0) return false; // we're done presolving (this first action)
            
            // set this to the 0th action so we can iterate the next left item
            if (path[ix] == allowedActions[^1])
            {
                path[ix] = allowedActions[0];
                ix -= 1;
                continue;
            }

            // iterate path[ix] to the next allowed action
            for (int i = 0; i < allowedActions.Length; i++)
            {
                if (allowedActions[i] != path[ix]) continue;
                path[ix] = allowedActions[i + 1];
                return true;
            }

            return false; // path[ix] isn't one of the allowed actions??
        }
    }
    private (bool,int) AuditPresolve(byte[] path)
    {
        int durability = 5, wn = 0, manip = 0;
        int lastWasteNot = -1, lastManip = -1, innovation = -1, veneration = -1;
        bool byregotsUsed = false;
        for (short i = 0; i < path.Length; i++)
        {
            Action action = Atlas.Actions.AllActions[path[i]];
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
            int cost = action.DurabilityCost / 2; // may have a ticking waste not
            
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
    #endregion
    
    #region Tree Presolver 2
    private void GenerateTree2()
    {
        Stopwatch st = new Stopwatch();
        st.Start();

        _tree = new Dictionary<int, StateNode>[StepForwardDepth];
        for (int i = 0; i < _tree.Length; i++) _tree[i] = new Dictionary<int, StateNode>();

        // build the thing
        foreach (var path in _presolve)
        {
            int parentIx = 0;
            for (int i = 0; i < path.Length; i++)
            {
                if (i > 0)
                {
                    parentIx = parentIx * 26 + path[i - 1];
                }
                if (!_tree[i].ContainsKey(parentIx)) _tree[i][parentIx] = 0;
                _tree[i][parentIx] |= (StateNode)actionToState[path[i]];
            }
        }
        Console.WriteLine($"Done building {st.ElapsedMilliseconds/1000}s");
        st.Restart();

        // byte[][] presolveAudit = new byte[_presolve.Length][];
        // int ix = 0;
        // // then re-create the list of arrays to make sure this makes sense
        // foreach (var kvp in _tree[^1])
        // {
        //     int parentIx = kvp.Key;
        //     byte[] path = new byte[StepForwardDepth];
        //     for (int i = StepForwardDepth - 2; i >= 0; i--)
        //     {
        //         path[i] = (byte)(parentIx % 26);
        //         parentIx = (int)Math.Truncate(Math.Floor(parentIx / 26M));
        //     }
        //
        //     for (int i = 0; i < 26; i++)
        //     {
        //         if ((int)(kvp.Value & (StateNode)(1 << i)) != 0)
        //         {
        //             presolveAudit[ix] = path.ToArray();
        //             presolveAudit[ix++][^1] = stateToAction[1 << i];
        //         }
        //     }
        // }
        // Console.WriteLine($"Done rebuilding {st.ElapsedMilliseconds/1000}s");
        // Console.WriteLine(_tree[^1].Keys.Max().ToString("N0"));
    }
    #endregion
    
    #region Solving
    public async Task<List<Action>> Run()
    {
        _sw.Start();

        int step = 0;
        List<(double, List<byte>, byte[])> prevStep = _actions.Select(x => (-1D, new List<byte> { x }, Array.Empty<byte>())).Where(x => !_sim.Simulate(x.Item2).IsError).ToList();

        _totalEvaluated = 0;
        _totalFailures = 0;
        _totalSkipped = 0;
        _nodesEvaluated = 0;
        do
        {
            _stepResults.Clear();
            _countdown = new CountdownEvent(1);
            _evaluated = 0;
            _failures = 0;
            _skipped = 0;
            _forwardSet = 0;

            List<Thread> extraThreads = new();
            for (int i = 0; i < prevStep.Count; i++)
            {
                Thread t = SolverThread(i, prevStep[i]);
                if (i < _threads.Length)
                {
                    _threads[i] = t;
                    t.Start();
                }
                else extraThreads.Add(t);
            }
            await Task.Delay(1_000);

            while (extraThreads.Any())
            {
                for (int i = 0; i < _threads.Length; i++)
                {
                    if (extraThreads.Any() && (_threads[i] == null || !_threads[i]!.IsAlive))
                    {
                        extraThreads[0].Start();
                        _threads[i] = extraThreads[0];
                        extraThreads.RemoveAt(0);
                    }
                }
                await Task.Delay(1_000);
            }

            _countdown.Signal();
            await Task.Run(() => _countdown.Wait());
            _logger($"[{DateTime.Now}, {MsToHumanReadable(_sw.ElapsedMilliseconds)}] [Step {step++ + 1}] " +
                    $"{_skipped:N0} skipped ({(double)_skipped / (_evaluated + _skipped):P0}) - " +
                    $"{_evaluated:N0} evaluated ({(double)_evaluated / (_evaluated + _skipped):P0}) | " +
                    $"{_failures:N0} failures ({(double)_failures / _evaluated:P0})" +
                    $">> {_forwardSet:N0}");
            _nodesEvaluated += prevStep.Count;

            prevStep = _stepResults.OrderByDescending(x => x.Item1).Take(StepSize).ToList();
            _totalEvaluated += _evaluated;
            _totalFailures += _failures;
            _totalSkipped += _skipped;
        } while (_stepResults.Any() && _stepResults.First().Item2.Count < MaxDepth - StepForwardDepth);

        _logger($"[{DateTime.Now}, {MsToHumanReadable(_sw.ElapsedMilliseconds, true)}] " +
                $"{_totalSkipped:N0} skipped ({(double)_totalSkipped / (_totalEvaluated + _totalSkipped):P0}) - " +
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
    #endregion

    #region Helpers
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

    private string MsToHumanReadable(long ms, bool full = false)
    {
        int days = 0, hours = 0, minutes = 0, seconds = 0;
        
        while (ms - 86400000 > 0)
        {
            ms -= 86400000;
            days += 1;
        }
        while (ms - 3600000 > 0)
        {
            ms -= 3600000;
            hours += 1;
        }
        while (ms - 60000 > 0)
        {
            ms -= 60000;
            minutes += 1;
        }
        while (ms - 1000 > 0)
        {
            ms -= 1000;
            seconds += 1;
        }

        if (!full) return $"{(days > 0 ? $"{days}d" : "")}{(hours > 0 ? $"{hours}h" : "")}{(minutes > 0 ? $"{minutes}m" : "")}{(seconds > 0 ? $"{seconds}s" : "")}";
        else if (days > 0) return $"{days}d{(hours > 0 ? $"{hours}h" : "")}";
        else if (hours > 0) return $"{hours}h{(minutes > 0 ? $"{minutes}m" : "")}";
        else if (minutes > 0) return $"{minutes}m{(seconds > 0 ? $"{seconds}s" : "")}";
        else return $"{seconds}s";
    }
    #endregion
}
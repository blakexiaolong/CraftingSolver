using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;

namespace Libraries.Solvers;
using static Solver;

public class SawStepSolver
{
    private const int
        MaxThreads = 25,
        MaxDepth = 30,
        StepForwardDepth = 7,
        StepBackDepth = 0,
        StepSize = 10_000;

    private double _bestScore;
    private List<Action> _bestSolution;
    private long _presolveFound, _evaluated, _failures, _skipped , _forwardSet;
    private long _totalEvaluated, _totalFailures, _totalSkipped, _nodesEvaluated;

    private readonly NanoSimulator _sim;
    private readonly LoggingDelegate _logger;
    private readonly byte[] _actions;

    private const int TreeWidth = 23;
    private readonly StateNode[] _tree;
    private struct StateNode
    {
        public uint Id;
        public StateActions Actions;
        
        [Flags]
        public enum StateActions
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
        }
    }
    private readonly Dictionary<byte, int> _actionToState = new()
    {
        { (int)Atlas.Actions.ActionMap.TrainedPerfection, (int)StateNode.StateActions.TrainedPerfection },
        { (int)Atlas.Actions.ActionMap.Groundwork, (int)StateNode.StateActions.Groundwork },
        { (int)Atlas.Actions.ActionMap.PreparatoryTouch, (int)StateNode.StateActions.PreparatoryTouch },
        { (int)Atlas.Actions.ActionMap.Veneration, (int)StateNode.StateActions.Veneration },
        { (int)Atlas.Actions.ActionMap.Innovation, (int)StateNode.StateActions.Innovation },
        { (int)Atlas.Actions.ActionMap.GreatStrides, (int)StateNode.StateActions.GreatStrides },
        { (int)Atlas.Actions.ActionMap.BasicSynth, (int)StateNode.StateActions.BasicSynth },
        { (int)Atlas.Actions.ActionMap.CarefulSynthesis, (int)StateNode.StateActions.CarefulSynthesis },
        { (int)Atlas.Actions.ActionMap.BasicTouch, (int)StateNode.StateActions.BasicTouch },
        { (int)Atlas.Actions.ActionMap.StandardTouch, (int)StateNode.StateActions.StandardTouch },
        { (int)Atlas.Actions.ActionMap.AdvancedTouch, (int)StateNode.StateActions.AdvancedTouch },
        { (int)Atlas.Actions.ActionMap.RefinedTouch, (int)StateNode.StateActions.RefinedTouch },
        { (int)Atlas.Actions.ActionMap.ByregotsBlessing, (int)StateNode.StateActions.ByregotsBlessing },
        { (int)Atlas.Actions.ActionMap.ImmaculateMend, (int)StateNode.StateActions.ImmaculateMend },
        { (int)Atlas.Actions.ActionMap.MastersMend, (int)StateNode.StateActions.MastersMend },
        { (int)Atlas.Actions.ActionMap.Manipulation, (int)StateNode.StateActions.Manipulation },
        { (int)Atlas.Actions.ActionMap.WasteNot, (int)StateNode.StateActions.WasteNot },
        { (int)Atlas.Actions.ActionMap.WasteNot2, (int)StateNode.StateActions.WasteNot2 },
        { (int)Atlas.Actions.ActionMap.PrudentTouch, (int)StateNode.StateActions.PrudentTouch },
        { (int)Atlas.Actions.ActionMap.DelicateSynthesis, (int)StateNode.StateActions.DelicateSynthesis },
        { (int)Atlas.Actions.ActionMap.TrainedFinesse, (int)StateNode.StateActions.TrainedFinesse },
        { (int)Atlas.Actions.ActionMap.PrudentSynthesis, (int)StateNode.StateActions.PrudentSynthesis },
        { (int)Atlas.Actions.ActionMap.Observe, (int)StateNode.StateActions.Observe },
    };
    private readonly Dictionary<int, byte> _stateToAction;

    private CountdownEvent _countdown = new(1);
    private readonly Thread?[] _threads = new Thread[MaxThreads];
    private readonly object _locker = new();
    private readonly Stopwatch _sw = new ();
    private readonly ConcurrentBag<(double, byte[], LightState)> _stepResults = new();

    public SawStepSolver(NanoSimulator sim, LoggingDelegate loggingDelegate)
    {
        _sim = sim;
        _logger = loggingDelegate;
        _actions = sim.Crafter.Actions.OrderBy(x => x).ToArray();
        _bestSolution = new List<Action>();

        BigInteger gameSpace = BigInteger.Pow(_actions.Length, MaxDepth);
        BigInteger solverSpace = BigInteger.Pow(_actions.Length, StepForwardDepth);
        _logger($"[{DateTime.Now}] Game space is {gameSpace:N0} nodes, solver space [{StepForwardDepth}] is {solverSpace:N0} nodes (~1 / {gameSpace / solverSpace:N0})");
        
        Console.Write("Pre-solving");
        _stateToAction = _actionToState.ToDictionary(x => x.Value, x => x.Key);
        _tree = Presolve();

        _logger($"\n[{DateTime.Now}] {_presolveFound:N0} expansions found (eliminated {(double)solverSpace - _presolveFound:N0} [{1 - _presolveFound / (double)solverSpace:P0}] possible expansions)");
    }

    #region Presolving
    private StateNode[] Presolve()
    {
        object presolveLock = new();

        long skipped = 0;
        byte[] actions = _sim.Crafter.Actions.Where(x => !Atlas.Actions.FirstRoundActions.Contains(x)).ToArray();
        List<StateNode>[] allowedNodes = new List<StateNode>[actions.Length];

        List<Thread> presolverThreads = new();
        for (int i = 0; i < actions.Length; i++)
        {
            int ix = i;
            allowedNodes[ix] = new();
            
            byte[] path = new byte[StepForwardDepth];
            path[0] = actions[ix];
            for (int j = 1; j < path.Length; j++)
                path[j] = actions[0];

            presolverThreads.Add(new Thread(() =>
            {
                int skipIx = -1, skipKey = 0;
                long foundPaths = 0;

                uint stateIx = 0;
                for (int j = 0; j < StepForwardDepth - 1; j++)
                    stateIx = stateIx * TreeWidth + path[j];
                StateNode node = new StateNode() { Id = stateIx, Actions = 0 };
                
                do
                {
                    if (skipIx >= 0 && path[skipIx] == skipKey) continue; // nyoooom
                    else if (skipIx >= 0) skipIx = -1; // resume presolving

                    var audit = AuditPresolve(path);
                    if (audit.Item1)
                    {
                        foundPaths += 1;
                        uint newStateIx = 0;
                        for (int j = 0; j < StepForwardDepth - 1; j++)
                            newStateIx = newStateIx * TreeWidth + path[j];
                        if (newStateIx != stateIx)
                        {
                            if (node.Actions != 0) allowedNodes[ix].Add(node);
                            node = new StateNode() { Id = stateIx, Actions = 0 };
                            stateIx = newStateIx;
                        }
                        else
                        {
                            node.Actions |= (StateNode.StateActions)_actionToState[path[StepForwardDepth - 1]];
                        }
                    }
                    else
                    {
                        skipIx = audit.Item2;
                        skipKey = path[skipIx];
                        skipped += (long)BigInteger.Pow(actions.Length, StepForwardDepth - 1 - skipIx) - 1;
                    }
                } while (PresolveIterator(ref path, actions));

                Console.Write(".");
                lock (presolveLock) { _presolveFound += foundPaths; }
            }));
        }
        foreach (var t in presolverThreads) t.Start();
        foreach (var t in presolverThreads) t.Join();

        Console.WriteLine($"\nPresolved - {skipped:N0} elements were proactively skipped");
        return allowedNodes.SelectMany(x => x).OrderBy(x => x.Id).ToArray();
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
        int maxDurability = _sim.Recipe.Durability, minDurability = 5;
        int wn = 8, manip = 8; // assume ticking waste not 2 and manipulation
        int lastWasteNot = -1, lastManip = -1, innovation = -1, veneration = -1;
        bool byregotsUsed = false, trainedPerfectionUsed = false;
        for (short i = 0; i < path.Length; i++)
        {
            if (maxDurability <= 0) return (false, i - 1);
            Action action = Atlas.Actions.AllActions[path[i]];
            if (i > 0)
            {
                if (Atlas.Actions.Buffs.Contains(path[i]) && path[i] == path[i - 1]) return (false, i); // repeated buff
                if (path[i] == (byte)Atlas.Actions.ActionMap.BasicTouch && path[i - 1] is (byte)Atlas.Actions.ActionMap.BasicTouch) return (false, i); // bad ordering
                if (path[i] == (byte)Atlas.Actions.ActionMap.ByregotsBlessing)
                {
                    if (byregotsUsed) return (false, i); // not a good idea
                    byregotsUsed = true;
                }
                if (path[i] == (byte)Atlas.Actions.ActionMap.TrainedPerfection)
                {
                    if (trainedPerfectionUsed) return (false, i); // can't happen
                    trainedPerfectionUsed = true;
                }
            }
            if (Atlas.Actions.FirstRoundActions.Contains(path[i])) return (false, i); // first round actions aren't allowed
            int cost = wn > 0 ? action.DurabilityCost / 2 : action.DurabilityCost;
            
            if (path[i] is (byte)Atlas.Actions.ActionMap.WasteNot or (byte)Atlas.Actions.ActionMap.WasteNot2)
            {
                if (lastWasteNot >= 0 && i - lastWasteNot <= 2) return (false, i); // super wasteful
                wn = lastWasteNot > 0 ? Math.Max(wn, action.ActiveTurns) : action.ActiveTurns;
                lastWasteNot = i;
            }
            else if (path[i] == (byte)Atlas.Actions.ActionMap.Manipulation)
            {
                if (lastManip >= 0 && i - lastManip <= 5) return (false, i); // not worth the GP
                manip = lastManip > 0 ? Math.Max(manip, action.ActiveTurns) : action.ActiveTurns;
                lastManip = i;
            }
            else if (path[i] == (byte)Atlas.Actions.ActionMap.MastersMend)
            {
                if (minDurability >= _sim.Recipe.Durability - 10) return (false, i); // wasted
                maxDurability += 30;
                minDurability += 30;
            }
            else if (path[i] == (byte)Atlas.Actions.ActionMap.ImmaculateMend)
            {
                if (minDurability >= _sim.Recipe.Durability - 30) return (false, i); // should have just used Master's Mend
                maxDurability = _sim.Recipe.Durability;
                minDurability = _sim.Recipe.Durability;
            }
            else if (path[i] == (byte)Atlas.Actions.ActionMap.PrudentTouch || path[i]==(byte)Atlas.Actions.ActionMap.PrudentSynthesis)
            {
                if (lastWasteNot > -1 && wn > 0) return (false, i); // can't use this action
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
            
            maxDurability -= cost;
            minDurability -= lastWasteNot >= 0 ? cost : action.DurabilityCost; // don't assume waste not is active here
            if (manip > 0)
            {
                maxDurability += 5;
                if (lastManip >= 0) minDurability += 5; // don't assume manipulation is active here
            }
            maxDurability = Math.Min(maxDurability, _sim.Recipe.Durability);
            minDurability = Math.Min(minDurability, _sim.Recipe.Durability);
            wn--;
            manip--;
            innovation--;
            veneration--;
        }

        return (true, 0);
    }
    #endregion
    
    #region Solving
    public async Task<List<Action>> Run()
    {
        GC.Collect();
        _sw.Start();

        int step = 0;
        List<(double, byte[])> prevStep = _actions.Select(x => (-1D, new[] { x })).Where(x => !_sim.Simulate(x.Item2).IsError).ToList();

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
            _nodesEvaluated += prevStep.Count;
            
            //TODO: reenable this? somehow it was dropping the final score
            prevStep = _stepResults.GroupBy(x => x.Item1)
                .OrderByDescending(x => x.Key)
                .Take(StepSize)
                .SelectMany(group => group.DistinctBy(x => x.Item3))
                .Take(StepSize)
                .Select(x => (x.Item1, x.Item2))
                .ToList();
            //prevStep = _stepResults.OrderByDescending(x => x.Item1).Take(StepSize).Select(x => (x.Item1, x.Item2)).ToList();
            
            _logger($"[{DateTime.Now}, {MsToHumanReadable(_sw.ElapsedMilliseconds)}] [Step {step++ + 1}] " +
                    $"{_skipped:N0} skipped ({(double)_skipped / (_evaluated + _skipped):P0}) - " +
                    $"{_evaluated:N0} evaluated ({(double)_evaluated / (_evaluated + _skipped):P0}) | " +
                    $"{_failures:N0} failures ({(double)_failures / _evaluated:P0})" +
                    $" >> {_forwardSet:N0} >> {prevStep.Count:N0}");
            
            _totalEvaluated += _evaluated;
            _totalFailures += _failures;
            _totalSkipped += _skipped;
        } while (_stepResults.Any() && _stepResults.First().Item2.Length < MaxDepth - StepForwardDepth);

        _logger($"[{DateTime.Now}, {MsToHumanReadable(_sw.ElapsedMilliseconds, true)}] " +
                $"{_totalSkipped:N0} skipped ({(double)_totalSkipped / (_totalEvaluated + _totalSkipped):P0}) - " +
                $"{_totalEvaluated:N0} evaluated ({(double)_totalEvaluated / (_totalEvaluated + _totalSkipped):P0}) | " +
                $"{_totalFailures:N0} failures ({(double)_totalFailures / _totalEvaluated:P0}) - " +
                $"{_nodesEvaluated * Math.Pow(_actions.Length, StepForwardDepth):N0} nodes " +
                $"(~{_nodesEvaluated * Math.Pow(_actions.Length, StepForwardDepth) / Math.Pow(_actions.Length, MaxDepth):P12} of game space) evaluated");
        return _bestSolution;
    }
    private Thread SolverThread(int threadId, (double, byte[]) prevStep) => new(() =>
    {
        if (_countdown.IsSet) return;
        _countdown.AddCount();

        ResetLocals(out double localBestScore, out var localBestPath, out var localBestState);
        List<(double, byte[], LightState)> forward = new();
        LightState prevState = _sim.SimulateToFailure(prevStep.Item2);

        #region Handle New Expansions
        byte prevKey = byte.MaxValue;
        long skipIx = -1;
        ArrayPool<byte> pool = ArrayPool<byte>.Create();
        foreach (var kvp in _tree)
        {
            switch (skipIx)
            {
                case >= 0 when skipIx >= kvp.Id:
                    _skipped += 1;
                    continue; // fast-forward
                case >= 0:
                    skipIx = -1;
                    break; // record scratch
            }
            
            uint parentIx = kvp.Id;
            byte[] path = pool.Rent(StepForwardDepth);
            for (int i = StepForwardDepth - 2; i >= 0; i--)
            {
                path[i] = (byte)(parentIx % TreeWidth);
                parentIx = (uint)Math.Truncate(Math.Floor(parentIx / (decimal)TreeWidth));
            }

            byte key = path[0];
            if (prevKey != key)
            {
                if (localBestScore >= 0)
                    forward.Add((localBestScore, localBestPath.Take(prevStep.Item2.Length + (StepForwardDepth - StepBackDepth)).ToArray(), localBestState));
                ResetLocals(out localBestScore, out localBestPath, out localBestState);
                prevKey = key;
            }

            LightState parentState = _sim.SimulateToFailure(path, StepForwardDepth - 1, prevState);
            _evaluated++;
            double score = Score(parentState, key);
            bool success = parentState.Success(_sim);
            if (score > localBestScore && success)
                ConfirmHighScore(score, prevStep, path);
            int stepsTaken = parentState.Step - prevState.Step;
            if (stepsTaken <= StepForwardDepth - 2)
            {
                long mod = (int)Math.Pow(TreeWidth, StepForwardDepth - stepsTaken - 2);
                skipIx = kvp.Id - (kvp.Id % mod) + (mod - 1);
                        
                if (!success) _failures++;
                pool.Return(path, clearArray: true);
                continue;
            }
            else if (success)
            {
                pool.Return(path, clearArray: true);
                continue;
            }

            for (int i = 0; i < TreeWidth; i++)
            {
                if ((int)(kvp.Actions & (StateNode.StateActions)(1 << i)) == 1 << i)
                {
                    LightState state = _sim.Simulate(_stateToAction[1 << i], parentState);
                    _evaluated++;
                    if (state.IsError)
                    {
                        _failures++;
                        continue;
                    }

                    score = Score(state, key);
                    if (score > localBestScore)
                    {
                        path[StepForwardDepth - 1] = _stateToAction[1 << i];
                        if (state.Success(_sim)) ConfirmHighScore(score, prevStep, path);
                        PreserveState(score, state, ref localBestScore, ref localBestPath, ref localBestState, prevStep, path);
                    }
                }
            }
            pool.Return(path, true);
        }

        if (localBestScore >= 0) forward.Add((localBestScore, localBestPath.Take(prevStep.Item2.Length + (StepForwardDepth - StepBackDepth)).ToArray(), localBestState));
        #endregion

        _forwardSet += forward.Count;
        var temp = forward.OrderByDescending(x => x.Item1).ToArray();
        foreach (var item in forward.OrderByDescending(x => x.Item1)) _stepResults.Add(item);
        
        if (threadId < _threads.Length) _threads[threadId] = null;
        _countdown.Signal();
    });

    private double Score(LightState state, int firstAction)
    {
        double progress = Math.Min(_sim.Recipe.Difficulty, state.Progress) / _sim.Recipe.Difficulty;

        double maxQuality = _sim.Recipe.MaxQuality * 1.1;
        double quality = Math.Min(maxQuality, state.Quality) / maxQuality;
        if (firstAction == (int)Atlas.Actions.ActionMap.TrainedEye) quality = 1;
        
        // ReSharper disable once PossibleLossOfFraction
        double cp = state.CP / _sim.Crafter.CP;
        double steps = 1 - state.Step / 100D;

        return (progress*90 + quality*150 + steps*9 + cp*1) / 250; // max 100
    }
    #endregion

    #region Helpers
    private void ResetLocals(out double localBestScore, out IEnumerable<byte> localBestPath, out LightState localBestState)
    {
        localBestScore = double.MinValue;
        localBestPath = Array.Empty<byte>();
        localBestState = new LightState();
    }
    private void PreserveState(double score, LightState state, ref double localBestScore, ref IEnumerable<byte> localBestPath, ref LightState localBestState, (double, byte[]) prevStep, byte[] batch)
    {
        if (score <= localBestScore) return;
        
        localBestScore = score;
        localBestPath = prevStep.Item2.Concat(batch.Take(StepForwardDepth)).ToArray();
        localBestState = state;
    }
    private void ConfirmHighScore(double score, (double, byte[]) prevStep, IEnumerable<byte> batch)
    {
        lock (_locker)
        {
            if (score <= _bestScore) return;
            byte[] path = prevStep.Item2.Concat(batch.Take(StepForwardDepth)).ToArray();

            _bestScore = score;
            LightState s = _sim.SimulateToFailure(path);
            _bestSolution = path.Take(s.Step).Select(x => Atlas.Actions.AllActions[x]).ToList();
            _logger($"\t{_bestScore:P} ({s.Quality:N0} Quality | {s.CP:N0} CP | {s.Durability:N0} Durability) [\"{string.Join("\", \"", _bestSolution.Select(x => x.ShortName))}\"]");
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
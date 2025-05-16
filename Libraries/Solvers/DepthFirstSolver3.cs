using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;

namespace Libraries.Solvers;
using static Solver;

public class DepthFirstSolver3
{
    private const int
        MaxThreads = 30,
        MaxDepth = 30;
    
    private double _bestScore;
    private List<Action> _bestSolution;
    private BigInteger _presolveFound, _evaluated, _failures, _auditFailures, _skipped;
    
    private readonly LightSimulator _sim;
    private readonly LoggingDelegate _logger;
    private readonly byte[] _actions;
    
    private CountdownEvent _countdown = new(1);
    private readonly Thread?[] _threads = new Thread[MaxThreads];
    private readonly object _locker = new();
    private readonly Stopwatch _sw = new ();
    private readonly ConcurrentBag<(double, List<byte>, byte[])> _stepResults = new();

    public DepthFirstSolver3(LightSimulator sim, Solver.LoggingDelegate loggingDelegate)
    {
        _sim = sim;
        _logger = loggingDelegate;
        _actions = sim.Crafter.Actions.OrderBy(x => x).ToArray();
        _bestSolution = new List<Action>();
    }

    public async Task<List<Action>> Run()
    {
        BigInteger gameSpace = BigInteger.Pow(_actions.Length, MaxDepth);
        _logger($"[{DateTime.Now}] Game space is [{MaxDepth}] {gameSpace:N0} nodes");
        
        _sw.Start();

        _countdown = new CountdownEvent(1);
        _evaluated = 0;
        _failures = 0;
        _auditFailures = 0;
        _skipped = 0;

        List<Thread> extraThreads = new();
        for (int i = 0; i < _actions.Length; i++)
        {
            Thread t = SolverThread(i, _actions[i]);
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
        await Task.Run(() =>
        {
            while (!_countdown.Wait(300_000))
            {
                _logger($"[{DateTime.Now}, {MsToHumanReadable(_sw.ElapsedMilliseconds, true)}] ({(double)(100_000*(_auditFailures+_evaluated+_skipped)/gameSpace)/100}%) " +
                        $"{_presolveFound:N0} found paths - " +
                        $"{_evaluated:N0} evaluated - " +
                        $"{_failures:N0} failures - " +
                        $"{_skipped:N0} skipped");
            }
        });

        _logger($"[{DateTime.Now}, {MsToHumanReadable(_sw.ElapsedMilliseconds, true)}] " +
                $"{_presolveFound:N0} found paths - " +
                $"{_evaluated:N0} evaluated - " +
                $"{_failures:N0} failures - " +
                $"{_skipped:N0} skipped");
        return _bestSolution;
    }

    private Thread SolverThread(int threadId, byte startAction) => new(() =>
    {
        if (_countdown.IsSet) return;
        _countdown.AddCount();

        byte[] actions = _sim.Crafter.Actions.Where(x => !Atlas.Actions.FirstRoundActions.Contains(x)).ToArray();

        double localBestScore = double.MinValue;
        byte[] localBestPath = new byte[MaxDepth];

        byte[] path = new byte[MaxDepth];
        path[0] = startAction;
        for (int j = 1; j < path.Length; j++) path[j] = actions[0];
        do
        {
            // var audit = AuditSolve(path);
            // if (true || audit.Item1)
            // {
                LightState state = _sim.SimulateToFailure(path);
                _evaluated++;
                if (state.Success(_sim))
                {
                    _presolveFound++;
                    double score = Score(state, startAction);
                    if (score > localBestScore)
                    {
                        ConfirmHighScore(score, state.Success(_sim), path);
                        PreserveState(score, ref localBestScore, ref localBestPath, path);
                    }
                }
                else _failures++;

                if (state.Step < path.Length - 1)
                {
                    SolveIterator(state.Step, ref path, actions);
                }
            // }
            // else
            // {
            //     _auditFailures++;
            //     SolveIterator(audit.Item2, ref path, actions);
            // }
        } while (SolveIterator(ref path, actions));

        if (threadId < _threads.Length) _threads[threadId] = null;
        _countdown.Signal();
    });
    private bool SolveIterator(int skipIx, ref byte[] path, byte[] allowedActions)
    {
        for (int i = skipIx+1; i < path.Length; i++)
        {
            path[i] = allowedActions[^1];
        }
        _skipped += BigInteger.Pow(allowedActions.Length, path.Length - 1 - skipIx) - 1;
        return true;
    }
    private bool SolveIterator(ref byte[] path, byte[] allowedActions, int ix = MaxDepth - 1)
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
    private (bool,int) AuditSolve(byte[] path)
    {
        int durability = 5, wn = 0, manip = 0;
        int lastWasteNot = -1, lastManip = -1, innovation = -1, veneration = -1;
        bool byregotsUsed = false, trainedPerfectionUsed = true;
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
                if (path[i] == (byte)Atlas.Actions.ActionMap.TrainedPerfection)
                {
                    if (trainedPerfectionUsed) return (false, i); // can't happen
                    trainedPerfectionUsed = true;
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
    
    private double Score(LightState state, int firstAction)
    {
        double progress = Math.Min(_sim.Recipe.Difficulty, state.Progress) / _sim.Recipe.Difficulty;

        double maxQuality = _sim.Recipe.MaxQuality * 1.1;
        double quality = Math.Min(maxQuality, state.Quality) / maxQuality;
        if (firstAction == (int)Atlas.Actions.ActionMap.TrainedEye) quality = 1;
        double steps = 1 - state.Step / (double)MaxDepth;

        return (progress * 90 + quality * 150 + steps * 10) / 250; // max 100
    }
    private void PreserveState(double score, ref double localBestScore, ref byte[] localBestPath, byte[] path)
    {
        if (score <= localBestScore) return;
        
        localBestScore = score;
        localBestPath = path.ToArray();
    }
    private void ConfirmHighScore(double score, bool success, byte[] path)
    {
        if (score <= _bestScore || !success) return;
        
        lock (_locker)
        {
            if (score <= _bestScore) return;

            _bestScore = score;
            LightState s = _sim.SimulateToFailure(path);
            _bestSolution = path.Take(s.Step).Select(x => Atlas.Actions.AllActions[x]).ToList();
            _logger($"\t{_bestScore:P} ({s.Quality:N0} / {_sim.Recipe.MaxQuality:N0} quality) [{path.Length}] {string.Join(", ", _bestSolution.Select(x => x.ShortName))}");
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
}
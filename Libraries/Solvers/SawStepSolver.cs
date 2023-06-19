using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Linq;

namespace Libraries.Solvers;
using static Solver;

public class SawStepSolver
{
    private const int
        MaxThreads = 1,
        MaxDepth = 26,
        StepForwardDepth = 4,
        StepBackDepth = StepForwardDepth - 1,
        StepSize = 100;

    private double _bestScore;
    private List<Action> _bestSolution;
    private long _evaluated, _failures, _forwardSet, _backSet;

    private readonly LightSimulator _sim;
    private readonly LoggingDelegate _logger;
    private readonly int[] _actions;
    private readonly int[][] _presolve = new[] { new int[1] };

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
        _bestSolution = new();

        BigInteger maxProgressCount = BigInteger.Pow(_actions.Length, MaxDepth);
        BigInteger solverCount = BigInteger.Pow(_actions.Length, StepForwardDepth + 1);
        _logger($"[{DateTime.Now}] Game space is {maxProgressCount:N0} nodes, solver space is {solverCount:N0} nodes");

        _presolve = Presolve();
        _logger($"{_presolve.Length:N0} expansions found (eliminated {Math.Pow(_sim.Crafter.Actions.Length, StepForwardDepth) - _presolve.Length:N0} possible expansions)");
    }

    private int[][] Presolve()
    {
        List<int[]> allowedPaths = new();
        int[] path = new int[StepForwardDepth];
        for (int i = 0; i < path.Length; i++) path[i] = _sim.Crafter.Actions[0];

        if (_sim.Simulate(path, useDurability: false).HasValue)
        {
            allowedPaths.Add(path.ToArray());
        }

        bool cont;
        do
        {
            cont = PresolveIterator(ref path);
            if (_sim.Simulate(path, useDurability: false).HasValue)
            {
                allowedPaths.Add(path.ToArray());
            }
        } while (cont);

        allowedPaths.RemoveAll(x => x.Any(y => Atlas.Actions.FirstRoundActions.Contains(y)));
        return allowedPaths.ToArray();
    }
    private bool PresolveIterator(ref int[] path, int ix = StepForwardDepth - 1)
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

    public async Task<List<Action>> Run()
    {
        _sw.Start();

        int step = 0;
        do
        {
            await DoNextStep(step++);
        } while (_stepResults.Any() && _stepResults.First().Value.Count < MaxDepth);

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
        _backSet = 0;

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
        _logger($"[{DateTime.Now}, {_sw.ElapsedMilliseconds / 1000}s] [Step {stepCount+1}, Length {prevStep.First().Count + 1}] {_evaluated:N0} evaluated - {_failures:N0} failures = {_forwardSet:N0} ({_backSet:N0})");
    }

    private Thread SolverThread(int threadId, LightState? lastState, List<int> path) => new(() =>
    {
        _countdown.AddCount();

        var forward = StepForward(lastState, path);
        _forwardSet += forward.Count;
        //var x = forward.SelectMany(x => x.Value.Select(y => Atlas.Actions.AllActions[y].ShortName).ToList()).ToList();
        var backward = StepBackward(forward);
        _backSet += backward.Count;
        foreach (var item in backward) _stepResults.Add(item);
        //_logger($"{Atlas.Actions.AllActions[path.FirstOrDefault()].Name}: {forward.Count:N0} -> {backward.Count:N0}");
        
        if (threadId < _threads.Length) _threads[threadId] = null;
        _countdown.Signal();
    });

    private List<KeyValuePair<double, List<int>>> StepForward(LightState? lastState, List<int> path)
    {
        List<KeyValuePair<double, List<int>>> ret = new();
        
        List<List<int>> expansions = Expand(path.ToList(), StepForwardDepth).ToList();
        for (int i = 0; i < expansions.Count; i++)
        {
            KeyValuePair<double, LightState?> score = Score(lastState, expansions[i]);
            if (score.Key >= 0)
            {
                if (score.Key > _bestScore && score.Value!.Value.Success(_sim))
                {
                    lock (_locker)
                    {
                        if (score.Key > _bestScore)
                        {
                            _bestScore = score.Key;
                            _bestSolution = expansions[i].Select(x => Atlas.Actions.AllActions[x]).ToList();
                            var s = _sim.Simulate(expansions[i]);
                            _logger($"\t{_bestScore:P} ({s?.Quality ?? 0:N0} / {_sim.Recipe.MaxQuality:N0} quality) {string.Join(", ", _bestSolution.Select(x => x.ShortName))}");
                        }
                        else
                        {
                            ret.Add(new(score.Key, expansions[i]));   
                        }
                    }
                }
                else
                {
                    ret.Add(new(score.Key, expansions[i]));   
                }
            }
        }

        return ret;
    }
    private IEnumerable<List<int>> Expand(List<int> path, int amount)
    {
        if (amount == 0) return new List<List<int>> { path };
        return _actions.SelectMany(action =>
        {
            List<int> expansion = path.ToList();
            expansion.Add(action);
            return Expand(expansion, amount - 1);
        });
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
            double progress = Math.Min(_sim.Recipe.Difficulty, state.Value.Progress) / _sim.Recipe.Difficulty; // max 100
            
            double maxQuality = _sim.Recipe.MaxQuality * 1.1;
            double quality = Math.Min(maxQuality, state.Value.Quality) / _sim.Recipe.MaxQuality; // max 110

            double cp = state.Value.CP / _sim.Crafter.CP;
            double steps = 1 - state.Value.Step / 100D;
            double extraCredit = cp + steps; // max 2

            return new((progress * 100 + quality * 100 + extraCredit) / 212, state); // max 100
        }
        else
        {
            _failures++;
            return new(-1, state);
        }
    }
    
    private List<KeyValuePair<double, List<int>>> StepBackward(List<KeyValuePair<double, List<int>>> step)
    {
        return step
            .Select(x => new KeyValuePair<double, List<int>>(x.Key, x.Value.Take(x.Value.Count - StepBackDepth).ToList()))
            .GroupBy(x => string.Join("", x.Value.Select(y => y.ToString("D2"))), (_, pairs) =>
            {
                var p = pairs.MaxBy(y => y.Key);
                return new KeyValuePair<double, List<int>>(p.Key, p.Value);
            }).ToList();
    }
}
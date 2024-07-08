using System.Diagnostics;
using System.Numerics;

namespace Libraries.Solvers;
using static Solver;

public class BruteForceSolver2
{
    private const int
        MaxThreads = 20,
        MaxDepth = 28;

    private double _bestScore = double.MinValue;
    private List<Action> _bestSolution;
    private readonly LightSimulator _sim;
    private readonly LoggingDelegate _logger;
    private readonly int[] _actions;
    
    private CountdownEvent _countdown = new(1);
    private readonly Thread?[] _threads = new Thread[MaxThreads];
    private readonly object _locker = new();
    private readonly Stopwatch _sw = new ();

    private BigInteger space = BigInteger.Zero, evaluated = BigInteger.Zero, skipped = BigInteger.Zero;
    
    public BruteForceSolver2(LightSimulator sim, LoggingDelegate loggingDelegate)
    {
        _sim = sim;
        _logger = loggingDelegate;
        _actions = sim.Crafter.Actions.OrderBy(x => x).ToArray();
        _bestSolution = new List<Action>();
        space = BigInteger.Pow(_actions.Length, 28);
    }
    
    public async Task<List<Action>> Run()
    {
        _sw.Start();

        int[] arr = new int[MaxDepth];
        for (int i = 0; i < MaxDepth; i++)
            arr[i] = 0;

        (int, LightState?) result;
        do
        {
            result = _sim.SimulateToFailure(arr.Select(x => _actions[x]));
            evaluated++;
            if (result.Item2.HasValue && result.Item2.Value.Success(_sim))
            {
                double score = Score(result.Item2.Value);
                if (score > _bestScore)
                {
                    _bestScore = score;
                    _bestSolution = arr.Take(result.Item1).Select(x => Atlas.Actions.AllActions[x]).ToList();
                    _logger($"[{(evaluated + skipped) / (space / 100000)}%] \t{_bestScore:P} ({result.Item2!.Value.Quality} / {_sim.Recipe.MaxQuality:N0} quality) {string.Join(", ", _bestSolution.Select(x => x.ShortName))}");
                }
            }
        } while (IteratePath(arr, result.Item1, _actions.Length));
        
        return _bestSolution;
    }

    private bool IteratePath(int[] arr, int ix, int max)
    {
        if (ix > arr.Length - 1)
        {
            ix = arr.Length - 1;
        }
        
        if (ix < 0)
        {
            return false;
        }
        else if (arr[ix] + 1 >= max)
        {
            IteratePath(arr, ix - 1, max);
        }
        else
        {
            arr[ix] += 1;
            for (int i = ix + 1; i < arr.Length; i++)
            {
                skipped += (9 - arr[i]) * BigInteger.Pow(10, arr.Length - i - 1);
                arr[i] = 0;
            }
        }
        return true;
    }

    private double Score(LightState state)
    {
        double progress = Math.Min(_sim.Recipe.Difficulty, state.Progress) / _sim.Recipe.Difficulty;

        double maxQuality = _sim.Recipe.MaxQuality * 1.1;
        double quality = Math.Min(maxQuality, state.Quality) / maxQuality;

        double cp = state.CP / _sim.Crafter.CP;
        double steps = 1 - state.Step / 100D;

        return (progress * 90 + quality * 150 + steps * 7 + cp * 3) / 250; // max 100
    }
}
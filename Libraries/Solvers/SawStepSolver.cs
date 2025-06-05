using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Newtonsoft.Json;

namespace Libraries.Solvers;
using static Solver;

public class SawStepSolver
{
    private const int
        MaxThreads = 50,
        MaxDepth = 30,
        StepForwardDepth = 5,
        StepSize = 1_000,
        StepFlattenThreshold = StepSize * 10;

    private double _bestScore, _worstAllowedScore = double.MinValue;
    private List<Action> _bestSolution;
    private ConcurrentQueue<(int, byte[])> _prevStep;
    private long _presolveFound, _evaluated, _failures, _skipped, _foundSolutions;
    private long _totalEvaluated, _totalFailures, _totalSkipped, _totalSimulated, _totalWastedSimulations;
    private long _simulations, _wastedSimulations;

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

    private readonly Thread?[] _threads = new Thread[MaxThreads];
    private readonly object _locker = new();
    private readonly Stopwatch _sw = new ();
    private readonly ConcurrentBag<ForwardItem> _stepResults = new();

    public SawStepSolver(NanoSimulator sim, LoggingDelegate loggingDelegate)
    {
        _sim = sim;
        _logger = loggingDelegate;
        _actions = sim.Crafter.Actions.OrderBy(x => x).ToArray();
        _bestSolution = new List<Action>();

        BigInteger gameSpace = BigInteger.Pow(_actions.Length, MaxDepth);
        BigInteger solverSpace = BigInteger.Pow(_actions.Count(x => !Atlas.Actions.FirstRoundActions.Contains(x)), StepForwardDepth);
        _logger($"[{DateTime.Now}] Game space is {gameSpace:N0} nodes, solver space [{StepForwardDepth}] is {solverSpace:N0} nodes (~1 / {gameSpace / solverSpace:N0})");
        
        Console.Write("Pre-solving");
        _stateToAction = _actionToState.ToDictionary(x => x.Value, x => x.Key);
        _tree = Presolve();

        _logger($"\n[{DateTime.Now}] {_presolveFound:N0} expansions found (eliminated {(double)solverSpace - _presolveFound:N0} [{1 - _presolveFound / (double)solverSpace:P0}] possible expansions)");
    }
    
    #region Teamcraft
    private class SearchResponseItem
    {
        public string En { get; set; }
        public int ItemId { get; set; }
    }
    private class RecipeResponseItem
    {
        public int Id { get; set; }
        public int Job { get; set; }
        public int Lvl { get; set; }
        public int Stars { get; set; }
        public bool Hq { get; set; }
        public int Durability { get; set; }
        public int Quality { get; set; }
        public int Progress { get; set; }
        public int ProgressDivider { get; set; }
        public int QualityDivider { get; set; }
        public int ProgressModifier { get; set; }
        public int QualityModifier { get; set; }
        public int ControlReq { get; set; }
        public int CraftsmanshipReq { get; set; }
        public int RLvl { get; set; }
        public int RequiredQuality { get; set; }
        public bool Expert { get; set; }
    }
    public static async Task<Recipe> Lookup(string term, Crafter crafter)
    {
        using HttpClient httpClient = new HttpClient();
        HttpResponseMessage response = await httpClient.GetAsync($"https://api.ffxivteamcraft.com/search?query={term}&type=Recipe&sort=desc&lang=en");
        using StreamReader streamReader = new StreamReader(await response.Content.ReadAsStreamAsync());
        SearchResponseItem[]? searchResponse = JsonConvert.DeserializeObject<SearchResponseItem[]>(await streamReader.ReadToEndAsync());
        if (searchResponse == default || searchResponse.Length == 0) throw new Exception("Search returned no results");

        response = await httpClient.GetAsync($"https://api.ffxivteamcraft.com/data/recipes-per-item/854db737f29126dbe84cecd2a4a17a6b0823ecfa/{searchResponse[0].ItemId}");
        using StreamReader streamReader2 = new StreamReader(await response.Content.ReadAsStreamAsync());
        Dictionary<string, RecipeResponseItem[]>? recipeResponse = JsonConvert.DeserializeObject<Dictionary<string, RecipeResponseItem[]>>(await streamReader2.ReadToEndAsync());
        if (recipeResponse == default || recipeResponse.Count == 0) throw new Exception("Search returned no recipes");

        RecipeResponseItem recipe = recipeResponse[searchResponse[0].ItemId.ToString()][0];
        if (crafter.Control < recipe.ControlReq) throw new Exception("You do not have the Control to craft this item");
        if (crafter.Craftsmanship < recipe.CraftsmanshipReq) throw new Exception("You do not have the Craftsmanship to craft this item");
        
        return new Recipe()
        {
            Level = (byte)recipe.Lvl,
            RLevel = (short)recipe.RLvl,
            Difficulty = recipe.Progress,
            StartQuality = 0,
            MaxQuality = recipe.Hq ? recipe.Quality : 0,
            Durability = (byte)recipe.Durability,
            ProgressDivider = (byte)recipe.ProgressDivider,
            QualityDivider = (byte)recipe.QualityDivider,
            ProgressModifier = recipe.ProgressModifier / 100D,
            QualityModifier = recipe.QualityModifier / 100D,
            IsExpert = recipe.Expert
        };
    }
    #endregion

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
    public List<Action> Run()
    {
        GC.Collect();
        _sw.Start();

        int step = 0, preLength;
        byte[][] stepPaths = _actions.Select(x => new[] { x }).Where(x => !_sim.Simulate(x).IsError).ToArray();
        _prevStep = new ConcurrentQueue<(int, byte[])>(stepPaths.Select((x, ix) => (ix, x)));

        do
        {
            _stepResults.Clear();
            _evaluated = 0;
            _failures = 0;
            _skipped = 0;
            _foundSolutions = 0;
            _simulations = 0;
            _wastedSimulations = 0;

            for(int ix = 0; ix < MaxThreads; ix++)
            {
                Thread t = SolverThread();
                t.Start();
                _threads[ix++] = t;
            }
            foreach (Thread? t in _threads) t?.Join();
            
            Console.WriteLine();
            preLength = stepPaths.FirstOrDefault()?.Length ?? 0;
            stepPaths = _stepResults
                .GroupBy(x => x.Score)
                .OrderByDescending(x => x.Key)
                .SelectMany(group => group.DistinctBy(x => x.State).Select(x =>
                {
                    byte[] path = new byte[preLength + StepForwardDepth];
                    
                    uint parentIx = x.PresolverKey;
                    stepPaths[x.StepIx].CopyTo(path, 0); // get start of path
                    for (int i = preLength + StepForwardDepth - 2; i >= preLength; i--)
                    {
                        path[i] = (byte)(parentIx % TreeWidth); // middle
                        parentIx = (uint)Math.Truncate(Math.Floor(parentIx / (decimal)TreeWidth));
                    }
                    path[^1] = x.Action; // last action
                    
                    return path;
                }))
                .Take(StepSize)
                .OrderBy(x => x, new StepComparer())
                .ToArray();
            _prevStep =  new ConcurrentQueue<(int, byte[])>(stepPaths.Select((x, i) => (i, x)));
            _worstAllowedScore = _stepResults.OrderByDescending(x => x.Score).Take(StepSize).LastOrDefault().Score;

            _logger($"[{DateTime.Now}, {MsToHumanReadable(_sw.ElapsedMilliseconds)}] [Step {step++ + 1}] " +
                    $"{_skipped:N0} skipped ({(double)_skipped / (_evaluated + _skipped):P0}) - " +
                    $"{_evaluated:N0} evaluated ({(double)_evaluated / (_evaluated + _skipped):P0}) | " +
                    $"{_failures:N0} failures ({(double)_failures / _evaluated:P0}) | " +
                    $"{_simulations:N0} simulations - {_wastedSimulations:N0} wasted ({_wastedSimulations / (double)_simulations:P0}) " +
                    $">> {_foundSolutions:N0} >> {_prevStep.Count:N0}");

            _totalEvaluated += _evaluated;
            _totalFailures += _failures;
            _totalSkipped += _skipped;
            _totalSimulated += _simulations;
            _totalWastedSimulations += _wastedSimulations;
        } while (_prevStep.Count > 0 && preLength < MaxDepth);

        _logger($"[{DateTime.Now}, {MsToHumanReadable(_sw.ElapsedMilliseconds, true)}] " +
                $"{_totalSkipped:N0} skipped ({(double)_totalSkipped / (_totalEvaluated + _totalSkipped):P0}) - " +
                $"{_totalEvaluated:N0} evaluated ({(double)_totalEvaluated / (_totalEvaluated + _totalSkipped):P0}) | " +
                $"{_totalFailures:N0} failures ({(double)_totalFailures / _totalEvaluated:P0}) | " +
                $"{_totalSimulated:N0} simulations - {_totalWastedSimulations:N0} wasted ({_totalWastedSimulations / (double)_totalSimulated:P0})");
        return _bestSolution;
    }

    private class StepComparer : IComparer<byte[]>
    {
        public int Compare(byte[] x, byte[] y)
        {
            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i]) return x[i].CompareTo(y[i]);
            }
            return 0;
        }
    }
    struct ForwardItem
    {
        public float Score;
        public LightState State;
        public int StepIx;
        public uint PresolverKey;
        public byte Action;
    }
    private Thread SolverThread() => new(() =>
    {
        long simulations = 0, wastedSimulations = 0, solutions = 0;

        byte[] lastStep = Array.Empty<byte>();
        Dictionary<float, List<ForwardItem>> forward = new Dictionary<float, List<ForwardItem>>(StepSize);
        long forwardItems = 0;
        while (_prevStep.TryDequeue(out (int, byte[]) step))
        {
            LightState prevState = _sim.SimulateToFailure(step.Item2);
            simulations += step.Item2.Length;
            for (int t = 0; t < step.Item2.Length && t < lastStep.Length; t++)
            {
                if (step.Item2[t] == lastStep[t]) wastedSimulations++;
                else break;
            }
            lastStep = step.Item2.ToArray();

            #region Handle New Expansions
            long skipIx = -1;
            byte[] lastPath = Array.Empty<byte>(); 
            ArrayPool<byte> pool = ArrayPool<byte>.Create();
            foreach (var kvp in _tree) // TODO: object allocations here are a problem
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

                LightState parentState = _sim.SimulateToFailure(path, StepForwardDepth - 1, prevState);
                simulations += StepForwardDepth - 1;
                for (int t = 0; t < StepForwardDepth - 1 && t < lastPath.Length - 1; t++)
                {
                    if (path[t] == lastPath[t]) wastedSimulations++;
                    else break;
                }
                lastPath = path.ToArray();
                _evaluated++;
                float score = Score(parentState, key);
                bool success = parentState.Success(_sim);
                if (success) ConfirmHighScore(score, step.Item2, path);
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

                int actions = (int)kvp.Actions;
                for (int actionIx = 0; actionIx < TreeWidth; actionIx++)
                {
                    if ((actions & 1) == 0)
                    {
                        actions >>= 1;
                        continue;
                    }

                    actions >>= 1;
                    byte action = _stateToAction[1 << actionIx];
                    LightState state = _sim.Simulate(action, parentState);
                    simulations++;
                    _evaluated++;
                    if (state.IsError)
                    {
                        _failures++;
                        continue;
                    }

                    score = Score(state, key);
                    if (score == 0) continue;

                    path[StepForwardDepth - 1] = action;
                    success = state.Success(_sim);
                    if (success)
                    {
                        ConfirmHighScore(score, step.Item2, path);
                        continue;
                    }

                    solutions++;
                    if (score < _worstAllowedScore || (forward.ContainsKey(score) && forward[score].Any(x=>x.State.Equals(state)))) continue;

                    if (!forward.ContainsKey(score)) forward[score] = new List<ForwardItem>();
                    forward[score].Add(new ForwardItem()
                    {
                        Score = score,
                        State = state,
                        StepIx = step.Item1,
                        PresolverKey = kvp.Id,
                        Action = action
                    });
                    if (++forwardItems >= StepFlattenThreshold)
                    {
                        int c = 0;
                        double localWorstScore = forward.Keys.Min();
                        foreach (float k in forward.Keys.OrderByDescending(x => x))
                        {
                            c += forward[k].Count;
                            if (c > StepSize)
                            {
                                localWorstScore = k;
                                break;
                            }
                        }
                        
                        lock (_locker) _worstAllowedScore = Math.Max(_worstAllowedScore, localWorstScore);
                        foreach (float k in forward.Keys)
                            if (k < _worstAllowedScore)
                                forward.Remove(k);
                        forwardItems = forward.Sum(x => x.Value.Count);
                    }
                }

                pool.Return(path, true);
            }
            #endregion
        }

        foreach (var item in forward
                     .Where(x => x.Key >= _worstAllowedScore)
                     .OrderByDescending(x => x.Key)
                     .SelectMany(x=>x.Value)
                     .Take(StepSize))
            _stepResults.Add(item);
        
        lock (_locker)
        {
            _simulations += simulations;
            _wastedSimulations += wastedSimulations;
            _foundSolutions += solutions;
        }
    });

    private float Score(LightState state, int firstAction)
    {
        double progress = Math.Min(_sim.Recipe.Difficulty, state.Progress) / _sim.Recipe.Difficulty;

        double maxQuality = _sim.Recipe.MaxQuality * 1.1;
        double quality = Math.Min(maxQuality, state.Quality) / maxQuality;
        if (firstAction == (int)Atlas.Actions.ActionMap.TrainedEye) quality = 1;
        
        // ReSharper disable once PossibleLossOfFraction
        float cp = state.CP / _sim.Crafter.CP;
        float steps = 1 - state.Step / 100F;

        return (float)((progress*90 + quality*150 + steps*9 + cp*1) / 250F); // max 100
    }
    #endregion

    #region Helpers
    private void ConfirmHighScore(double score, byte[] prevStep, IEnumerable<byte> batch)
    {
        if (score <= _bestScore) return;
        lock (_locker)
        {
            if (score <= _bestScore) return;
            byte[] path = prevStep.Concat(batch.Take(StepForwardDepth)).ToArray();

            _bestScore = score;
            LightState s = _sim.SimulateToFailure(path);
            _bestSolution = path.Take(s.Step).Select(x => Atlas.Actions.AllActions[x]).ToList();
            _logger($"\t{_bestScore:P} ({s.Quality,6:N0} Quality | {s.CP,4:N0} CP | {s.Durability,2:N0} Durability) [\"{string.Join("\", \"", _bestSolution.Select(x => x.ShortName))}\"]");
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
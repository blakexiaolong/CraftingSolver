using System.Collections.Concurrent;
using System.Runtime;
using static Libraries.Solver;
namespace Libraries.Solvers;

public class JABOASolver2
{
    private const double MaxDurabilityCost = 88 / 30D;
    private const double MinDurabilityCost = 98 / 160D;
    private const int MaxProgressListCount = 15000;
    private const int MaxQualityListCount = 8000000;
    private const int ProgressSetMaxLength = 7;
    private const int QualitySetMaxLength = 10;//14;

    private readonly LightSimulator _sim;
    private readonly LightState _startState;
    private readonly LoggingDelegate _logger;

    private readonly Action[] _progressActions, _qualityActions;
    private readonly KeyValuePair<Action, double>[] _durabilityActions;

    private int _countdown = 4;
    private readonly object _lockObject = new();

    private readonly ConcurrentBag<KeyValuePair<double, List<Action>>> _scoredProgressLists = new();
    private readonly ConcurrentBag<KeyValuePair<double, List<Action>>> _scoredQualityLists = new();
    private readonly ConcurrentQueue<KeyValuePair<double, List<Action>>> _qualityQueue = new();
    private List<List<Action>> _progressLists = new();
    private KeyValuePair<double, List<Action>>? _bestSolution;

    public JABOASolver2(LightSimulator sim, LoggingDelegate loggingDelegate)
    {
        _sim = sim;
        _logger = loggingDelegate;
        _startState = _sim.Simulate(new List<Action>())!.Value;

        _progressActions =
            Atlas.Actions.ProgressActions.Where(x => _sim.Crafter.Actions.Contains(x))
            .Concat(Atlas.Actions.ProgressBuffs.Where(y => _sim.Crafter.Actions.Contains(y))).ToArray();

        var q =
            Atlas.Actions.QualityActions.Where(x => _sim.Crafter.Actions.Contains(x))
            .Concat(Atlas.Actions.QualityBuffs.Where(y => _sim.Crafter.Actions.Contains(y))).ToList();
        if (sim.PureLevelDifference < 10 || sim.Recipe.IsExpert) q.Remove(Atlas.Actions.TrainedEye);
        q.Remove(Atlas.Actions.DelicateSynthesis); // todo: put this back in
        _qualityActions = q.ToArray();

        _durabilityActions = Atlas.Actions.DurabilityActions.Where(x => _sim.Crafter.Actions.Contains(x.Key)).ToArray();
    }

    public List<Action>? Run(int maxTasks)
    {
        int cp = _sim.Crafter.CP + (int)(_sim.Recipe.Durability * MaxDurabilityCost);

        _logger($"\n[{DateTime.Now}] Generating Progress Combinations");
        GenerateProgressTree(cp);
        _progressLists = _scoredProgressLists.OrderBy(x => x.Key).Take(MaxProgressListCount).Select(x => x.Value).ToList();
        int cpCost = (int)ListToCpCost(_progressLists.First());
        cp = _sim.Crafter.CP + (int)(_sim.Recipe.Durability * MaxDurabilityCost) - cpCost;
        _logger($"\t{string.Join(",", _progressLists.First().Select(x => x.ShortName))}");
        _logger($"[{DateTime.Now}] CP Cost: {cpCost}; CP Remaining: {cp}");

        _logger($"\n[{DateTime.Now}] Generating Quality Combinations");
        GenerateQualityTree(cp);

        return _bestSolution?.Value;
    }

    #region Progress Tree
    private void GenerateProgressTree(int cpLimit)
    {
        DfsState state = new();
        foreach (var action in _progressActions)
        {
            state.Add(GenerateProgressTreeInner(new(null, _startState, null!), action, 0, ProgressSetMaxLength, cpLimit));
        }
        _logger($"[{DateTime.Now}] Nodes Generated: {state.NodesGenerated} | Solutions Found: {state.SolutionCount}");
    }
    private DfsState GenerateProgressTreeInner(ActionNode node, Action action, double prevScore, int remainingDepth, int cpLimit)
    {
        DfsState dfsState = new DfsState();
        if (remainingDepth <= 0) return dfsState;

        LightState? state = _sim.Simulate(action, node.State, false);
        if (state == null) return dfsState;

        double score = ScoreState(state, ignoreProgress: false);
        if (score < 0) return dfsState;
        if (_sim.Crafter.CP - state.Value.CP > cpLimit) return dfsState;
        if (action.ActiveTurns <= 0 && score <= prevScore) return dfsState;

        ActionNode newNode;
        lock (node) { newNode = node.Add(action, state.Value); }

        if (state.Value.Success(_sim))
        {
            dfsState.SolutionCount++;
            List<Action> path = newNode.GetPath(state.Value.Step);
            _scoredProgressLists.Add(new((int)ListToCpCost(path), path));
        }

        dfsState.NodesGenerated++;
        foreach (Action nextAction in _progressActions) dfsState.Add(GenerateProgressTreeInner(newNode, nextAction, score, remainingDepth - 1, cpLimit));
        lock (node) { node.Remove(newNode); }

        if (remainingDepth == ProgressSetMaxLength) _logger($"-> {action.Name} ({dfsState.NodesGenerated} generated)");
        return dfsState;
    }
    #endregion

    #region Quality Tree
    private void GenerateQualityTree(int cpLimit)
    {
        GCLatencyMode oldMode = GCSettings.LatencyMode;
        try
        {
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            DfsState state = new();
            state.Add(GenerateQualityTreeThreading(new(null, _startState, null!), 0, QualitySetMaxLength, cpLimit));
            _logger($"[{DateTime.Now}] Nodes Generated: {state.NodesGenerated} | Solutions Found: {state.SolutionCount}");
        }
        finally
        {
            GCSettings.LatencyMode = oldMode;
        }
    }
    private DfsState GenerateQualityTreeThreading(ActionNode node, double prevScore, int remainingDepth, int cpLimit)
    {
        DfsState state = new();
        if (remainingDepth <= 0) return state;
        else if (remainingDepth > 4 && _countdown > 0)
        {
            lock (_lockObject) _countdown = Math.Max(_countdown - 1, 0);
            Thread[] threads = new Thread[_qualityActions.Length];
            for (int i = 0; i < threads.Length; i++)
            {
                Action action = _qualityActions[i];
                threads[i] = new Thread(() => state.Add(GenerateQualityTreeInner(node, action, prevScore, remainingDepth, cpLimit)));
                threads[i].Start();
            }
            foreach (var thread in threads) thread.Join();
            lock (_lockObject) _countdown += 1;
        }
        else
        {
            foreach (Action action in _qualityActions)
            {
                state.Add(GenerateQualityTreeInner(node, action, prevScore, remainingDepth, cpLimit));
            }
        }
        return state;
    }
    private DfsState GenerateQualityTreeInner(ActionNode node, Action action, double prevScore, int remainingDepth, int cpLimit)
    {
        DfsState dfsState = new DfsState();
        LightState? state = _sim.Simulate(action, node.State, false);
        if (state == null) return dfsState;

        double score = ScoreState(state, ignoreProgress: true);
        if (score < 0) return dfsState;
        if (_sim.Crafter.CP - state.Value.CP > cpLimit) return dfsState;
        if (action.ActiveTurns <= 0 && score <= prevScore) return dfsState;

        ActionNode newNode;
        lock (node) { newNode = node.Add(action, state.Value); }

        bool doNextLevel = true;
        if (_bestSolution == null || _bestSolution.Value.Key - 140 > score)
        {
            dfsState.SolutionCount++;
            List<Action> path = newNode.GetPath(state.Value.Step);
            InlineMerger(path);
        }

        dfsState.NodesGenerated++;
        dfsState.Add(GenerateQualityTreeThreading(newNode, score, remainingDepth - 1, cpLimit));
        lock (node) { node.Remove(newNode); }

        if (remainingDepth == QualitySetMaxLength) _logger($"-> {action.Name} ({dfsState.NodesGenerated} generated)");
        return dfsState;
    }
    #endregion

    #region Merger
    ulong notEnoughCpFirst = 0;
    ulong bothFirstRound = 0;
    ulong notEnoughProgressCount = 0;
    ulong zipCount = 0;
    ulong notEnoughCpSecond = 0;
    ulong craftNotFinished = 0;
    ulong subZeroScore = 0;
    ulong belowBestScoreFirst = 0;
    ulong durabilitiesSolved = 0;
    ulong belowBestScoreSecond = 0;
    private bool InlineMerger(List<Action> qualityList)
    {
        //return true;
        for (int i = 0; i < _progressLists.Count; i++)
        {
            List<Action> progressList = _progressLists[i];
            double cpMax = _sim.Crafter.CP;
            if (cpMax - ListToCpCost(qualityList, false) - ListToCpCost(progressList, false) - MinDurabilityCPCost(qualityList, cpMax) < 0)
            {
                notEnoughCpFirst++;
                continue;
            };

            uint progressMerger = 1;
            int progressMergeLength = qualityList.Count + progressList.Count;
            uint maxProgressCombinations = (uint)Math.Pow(2, progressMergeLength);

            if (Atlas.Actions.FirstRoundActions.Contains(progressList[0])) progressMerger = (uint)Math.Pow(2, progressMergeLength - 1) + 1;
            if (Atlas.Actions.FirstRoundActions.Contains(qualityList[0])) maxProgressCombinations = (uint)Math.Pow(2, progressMergeLength - 1);
            if (progressMerger > maxProgressCombinations)
            {
                bothFirstRound++;
                continue;
            }

            do
            {
                if (System.Numerics.BitOperations.PopCount(progressMerger) != progressList.Count)
                {
                    notEnoughProgressCount++;
                    continue;
                }
                zipCount++;

                List<Action> preDurabilityActions = ZipLists(qualityList, progressList, progressMerger, progressMergeLength);
                if (cpMax - ListToCpCost(preDurabilityActions, false) - MinDurabilityCPCost(preDurabilityActions, cpMax) < 0)
                {
                    notEnoughCpSecond++;
                    break;
                }

                LightState? progressState = _sim.Simulate(preDurabilityActions, false);
                if (!progressState.HasValue || progressState.Value.Progress < _sim.Recipe.Difficulty)
                {
                    craftNotFinished++;
                    continue;
                }

                double progressScore = ScoreState(progressState);
                if (progressScore < 0)
                {
                    subZeroScore++;
                    continue;
                }
                if (_bestSolution != null && progressScore - _bestSolution.Value.Key < 3)
                {
                    belowBestScoreFirst++;
                    continue;
                }

                // insert durability
                if (MinMaxSolveDurability(preDurabilityActions, progressState.Value.CP, out var postDurabilityActions))
                {
                    durabilitiesSolved++;
                    double newScore = ScoreState(_sim.Simulate(postDurabilityActions!));
                    lock (_lockObject)
                    {
                        if (_bestSolution != null && newScore - _bestSolution.Value.Key < 3)
                        {
                            belowBestScoreSecond++;
                            continue;
                        };

                        _logger($"[{DateTime.Now}] New Best Solution Found ({newScore}):");
                        _logger($"\t{string.Join(",", postDurabilityActions!.Select(x => x.ShortName))}");
                        _bestSolution = new(newScore, postDurabilityActions!);
                    }
                }
            } while ((progressMerger += 2) < maxProgressCombinations);
        }
        return true;
    }
    private bool MinMaxSolveDurability(List<Action> actions, double cpMax, out List<Action>? durabilityActions)
    {
        durabilityActions = null;
        double baseDurabilityCost = GetDurabilityCost(actions, out int stopIx);
        if (stopIx == actions.Count)
        {
            durabilityActions = actions;
            return true;
        }

        List<KeyValuePair<double, List<Action>>> durabilityLists = new();
        foreach (var durabilityAction in _durabilityActions)
        {
            if (cpMax < durabilityAction.Key.CPCost) continue;
            for (int i = 0; i < stopIx; i++)
            {
                actions.Insert(i, durabilityAction.Key);
                try
                {
                    LightState? s = _sim.Simulate(actions, useDurability: false);
                    if (s == null) continue;

                    double newDurabilityCost = GetDurabilityCost(actions, out int _);
                    if (newDurabilityCost >= baseDurabilityCost) continue;

                    //double durabilitySavings = (baseDurabilityCost - newDurabilityCost) / durabilityAction.Key.CPCost;
                    durabilityLists.Add(new(cpMax - durabilityAction.Key.CPCost, actions.ToList()));
                }
                finally
                {
                    actions.RemoveAt(i);
                }
            }
        }

        double bestScore = 0;
        foreach (var durabilityList in durabilityLists)
        {
            if (!MinMaxSolveDurability(durabilityList.Value, durabilityList.Key, out var solved)) continue;

            LightState? s = _sim.Simulate(solved!, useDurability: true);
            if (s == null || !s.Value.Success(_sim)) continue;

            double newScore = ScoreState(s);
            if (!(newScore > bestScore)) continue;

            bestScore = newScore;
            durabilityActions = solved;
        }

        return bestScore > 0;
    }
    private double MinDurabilityCPCost(List<Action> actions, double cpMax)
    {
        double remainingCp = cpMax;
        List<Action> chosenActions = actions.ToList();

        do
        {
            Action? chosenAction = null;
            List<Action>? bestActions = null;
            double baseDurabilityCost = GetDurabilityCost(chosenActions, out int _);
            if (baseDurabilityCost == 0) return 0;

            double bestEfficiency = 0;
            foreach (var durabilityAction in _durabilityActions)
            {
                if (remainingCp < durabilityAction.Key.CPCost) continue;
                for (int i = 0; i < chosenActions.Count; i++)
                {
                    chosenActions.Insert(i, durabilityAction.Key);
                    try
                    {
                        double newDurabilityCost = GetDurabilityCost(chosenActions, out int _);
                        if (newDurabilityCost >= baseDurabilityCost) continue;

                        double newEfficiency = (baseDurabilityCost - newDurabilityCost) / durabilityAction.Key.CPCost;
                        if (newEfficiency <= bestEfficiency) continue;

                        LightState? s = _sim.Simulate(chosenActions, useDurability: false);
                        if (s == null) continue;

                        bestEfficiency = newEfficiency;
                        chosenAction = durabilityAction.Key;
                        bestActions = chosenActions.ToList();
                    }
                    finally
                    {
                        chosenActions.RemoveAt(i);
                    }
                }
            }

            if (chosenAction == null) return cpMax + 1;
            remainingCp -= chosenAction.CPCost;

            if (bestActions == null) continue;
            chosenActions = bestActions.ToList();

            LightState? x = _sim.Simulate(bestActions, useDurability: true);
            if (x == null) continue;

            return cpMax - remainingCp;
        } while (true);
    }
    private static List<Action> ZipLists(List<Action> left, List<Action> right, uint merger, int length)
    {
        int l = left.Count, r = right.Count;
        List<Action> actions = new(length);
        for (int i = 0; i < length; i++)
        {
            actions.Insert(0, ((merger >> i) & 1) == 0 ? left[--l] : right[--r]);
        }
        return actions;
    }
    #endregion

    #region Utilities
    private double ScoreState(LightState? state, bool ignoreProgress = false)
    {
        if (!state.HasValue) return -1;
        bool success = state.Value.Success(_sim);
        if (!success)
        {
            var violations = state.Value.CheckViolations(_sim);
            if (!violations.DurabilityOk || !violations.CpOk) return -1;
        }

        double progress = ignoreProgress ? 0 : (state.Value.Progress > _sim.Recipe.Difficulty ? _sim.Recipe.Difficulty : state.Value.Progress) / _sim.Recipe.Difficulty;
        double maxQuality = _sim.Recipe.MaxQuality * 1.1;
        double quality = (state.Value.Quality > maxQuality ? maxQuality : state.Value.Quality) / _sim.Recipe.MaxQuality;

        double cp = state.Value.CP / _sim.Crafter.CP;
        double steps = (100 - state.Value.Step) / 100D;
        double extraCredit = (cp + steps) * 10;

        return (progress + quality) * 100 + extraCredit + (success ? 20 : 0);
    }
    private static double ListToCpCost(List<Action> list, bool considerDurability = true)
    {
        double cpTotal = list.Sum(x => x.CPCost);
        double durabilityTotal = considerDurability ? list.Sum(x => x.DurabilityCost) * MinDurabilityCost : 0;
        int observeCost = (list.Count(x => x.Equals(Atlas.Actions.FocusedSynthesis)) + list.Count(x => x.Equals(Atlas.Actions.FocusedTouch))) * Atlas.Actions.Observe.CPCost;

        int comboSavings = 0;
        int index = 0;
        while ((index = list.FindIndex(index, x => x.Equals(Atlas.Actions.StandardTouch))) >= 0)
        {
            if (index > 0 && list[index - 1].Equals(Atlas.Actions.BasicTouch))
            {
                comboSavings += Atlas.Actions.StandardTouch.CPCost - Atlas.Actions.BasicTouch.CPCost;
                if (list.Count > index + 1 && list[index + 1].Equals(Atlas.Actions.AdvancedTouch))
                {
                    comboSavings += Atlas.Actions.AdvancedTouch.CPCost - Atlas.Actions.BasicTouch.CPCost;
                }
            }
            index++;
        }

        return cpTotal + observeCost + durabilityTotal - comboSavings;
    }
    private double GetDurabilityCost(List<Action> actions, out int stopIx)
    {
        double dur = _sim.Recipe.Durability;
        double cost = 0;
        int wnRounds = 0;
        int manipRounds = 0;
        stopIx = actions.Count;

        for (int i = 0; i < actions.Count - 1; i++)
        {
            var action = actions[i];
            if (action.Equals(Atlas.Actions.WasteNot))
            {
                wnRounds = 4;
            }
            else if (action.Equals(Atlas.Actions.WasteNot2))
            {
                wnRounds = 8;
            }
            else if (action.Equals(Atlas.Actions.Manipulation))
            {
                manipRounds = 8;
            }
            else if (action.Equals(Atlas.Actions.MastersMend))
            {
                cost -= 30;
                dur += 30;
            }
            else
            {
                cost += wnRounds > 0 ? action.DurabilityCost / 2 : action.DurabilityCost;
                dur -= wnRounds > 0 ? action.DurabilityCost / 2 : action.DurabilityCost;
            }

            if (dur <= 0 && stopIx > i)
                stopIx = i;

            wnRounds--;
            if (!action.Equals(Atlas.Actions.Manipulation)) manipRounds--;
            if (manipRounds > 0)
            {
                cost -= 5;
                dur += 5;
            }
        }

        return cost;
    }
    #endregion
}

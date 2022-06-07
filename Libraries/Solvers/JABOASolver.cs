using System.Collections.Concurrent;
using System.Runtime;
using static Libraries.Solver;

namespace Libraries.Solvers
{
    // Just a Bunch of Actions
    public class JaboaSolver : ISolver
    {
        private const double MaxDurabilityCost = 88 / 30D;
        private const double MinDurabilityCost = 96 / 40D;
        private const int QualityListMaxLength = 50000000; // 50 million

        private const int ProgressSetMaxLength = 7;
        private const int QualitySetMaxLength = 12;

        private readonly LightSimulator _sim;
        private readonly LightState _startState;
        private readonly LoggingDelegate _logger;

        private readonly Action[] _progressActions, _qualityActions, _durabilityActions;

        private int _countdown = 4;
        private readonly object _lockObject = new();
        private readonly ManualResetEventSlim _mergeEvent = new();

        private readonly ConcurrentBag<KeyValuePair<double, List<Action>>> _scoredProgressLists = new(), _scoredQualityLists = new();
        private List<List<Action>> _progressLists = new();
        private KeyValuePair<double, List<Action>>? _bestSolution;
        private readonly List<KeyValuePair<double, List<Action>>> _durabilityLists = new();
        private readonly ConcurrentStack<KeyValuePair<double, List<Action>>> _qualityLists = new();

        public JaboaSolver(LightSimulator sim, LoggingDelegate loggingDelegate)
        {
            _sim = sim;
            _logger = loggingDelegate;
            _startState = _sim.Simulate(new List<Action>())!.Value;

            // ReSharper disable once HeapView.ObjectAllocation.Evident
            _progressActions = new Action[Atlas.Actions.ProgressActions.Length + Atlas.Actions.ProgressBuffs.Length];
            Atlas.Actions.ProgressActions.CopyTo(_progressActions, 0);
            Atlas.Actions.ProgressBuffs.CopyTo(_progressActions, Atlas.Actions.ProgressActions.Length);
            
            var q = new List<Action>();
            q.AddRange(Atlas.Actions.QualityActions);
            q.AddRange(Atlas.Actions.QualityBuffs);
            if (sim.PureLevelDifference < 10 || sim.Recipe.IsExpert) q.Remove(Atlas.Actions.TrainedEye);
            q.Remove(Atlas.Actions.DelicateSynthesis);
            _qualityActions = q.ToArray();

            _durabilityActions = Atlas.Actions.DurabilityActions.ToArray();
        }

        public List<Action>? Run(int maxTasks)
        {
            int cp = _sim.Crafter.CP + (int)(_sim.Recipe.Durability * MaxDurabilityCost);

            _logger($"\n[{DateTime.Now}] Generating Progress Combinations");
            GenerateDfsActionTree(_progressActions, ProgressFailure, ProgressSuccess, ProgressSuccessCallback, ProgressScore, ProgressSetMaxLength, cp, ignoreProgress: false);
            _progressLists = _scoredProgressLists.OrderBy(x => x.Key).Select(x => x.Value).ToList();
            int cpCost = (int)ListToCpCost(_progressLists.First());
            cp = _sim.Crafter.CP + (int)(_sim.Recipe.Durability * MaxDurabilityCost) - cpCost;
            _logger($"\t{string.Join(",", _progressLists.First().Select(x => x.ShortName))}");
            _logger($"[{DateTime.Now}] CP Cost: {cpCost}; CP Remaining: {cp}");

            _logger($"\n[{DateTime.Now}] Generating Durability Combinations");
            GenerateDurabilityLists(cp);
            _logger($"[{DateTime.Now}] {_durabilityLists.Count} Lists Generated");

            _logger($"\n[{DateTime.Now}] Generating Quality Combinations");
            GenerateDfsActionTree(_qualityActions, QualityFailure, QualitySuccess, QualitySuccessCallback, QualityScore, QualitySetMaxLength, cp, ignoreProgress: true);

            Merger();
            return _bestSolution?.Value;
        }
        
        #region Combination Generator
        #region Delegates
        private delegate bool SuccessCondition(LightState state, double score);
        private bool ProgressSuccess(LightState state, double score)
        {
            return state.Success(_sim);
        }
        private bool QualitySuccess(LightState state, double score)
        {
            return _bestSolution == null || score - _bestSolution.Value.Key > 1;
        }

        private delegate void SuccessCallback(double score, List<Action> actions);
        private void ProgressSuccessCallback(double score, List<Action> actions)
        {
            _scoredProgressLists.Add(new(score, actions));
        }
        private void QualitySuccessCallback(double score, List<Action> actions)
        {
            //return;
            if (_bestSolution != null && score < _bestSolution.Value.Key) return;
            if (_mergeEvent.IsSet)
            {
                lock (_scoredQualityLists)
                {
                    _scoredQualityLists.Add(new(score, actions));
                }

                Merger();
            }
            else
            {
                _scoredQualityLists.Add(new(score, actions));
                if (_scoredQualityLists.Count >= QualityListMaxLength) Merger();
            }
        }

        private delegate bool FailureCondition(LightState state);
        private bool ProgressFailure(LightState state)
        {
            return false;
        }
        private bool QualityFailure(LightState state)
        {
            return false;
        }

        private delegate double NodeScore(List<Action> path, double score);
        private double ProgressScore(List<Action> path, double score)
        {
            return ListToCpCost(path);
        }
        private double QualityScore(List<Action> path, double score)
        {
            return score;
        }
        #endregion

        private void GenerateDfsActionTree(Action[] actions, FailureCondition failureCondition, SuccessCondition successCondition, SuccessCallback successCallback, NodeScore nodeScore, int maxLength, double cpLimit, bool ignoreProgress)
        {
            GCLatencyMode oldMode = GCSettings.LatencyMode;
            // ReSharper disable once HeapView.ObjectAllocation.Evident
            ActionNode head = new ActionNode(null, _startState, null!);
            try
            {
                GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
                var s = SubDfsActionTree(head, actions, failureCondition, successCondition, successCallback, nodeScore, 0, maxLength, maxLength, cpLimit, ignoreProgress);
                _logger($"[{DateTime.Now}] Nodes Generated: {s.NodesGenerated} | Solutions Found: {s.SolutionCount}");
            }
            finally
            {
                GCSettings.LatencyMode = oldMode;
            }
        }
        private DfsState SubDfsActionTree(ActionNode node, Action[] actions, FailureCondition failureCondition, SuccessCondition successCondition, SuccessCallback successCallback, NodeScore nodeScore, double prevScore, int maxLength, int remainingDepth, double cpLimit, bool ignoreProgress)
        {
            DfsState state = new();
            switch (remainingDepth)
            {
                case <= 0: return state;
                case > 4 when _countdown > 0:
                {
                    lock (_lockObject) _countdown = Math.Max(_countdown - 1, 0);
                    Thread[] threads = new Thread[actions.Length];
                    for (int i = 0; i < threads.Length; i++)
                    {
                        Action action = actions[i];
                        threads[i] = new Thread(() => state.Add(SubDfsActionTreeInner(node, action, actions, failureCondition, successCondition, successCallback, nodeScore, prevScore, maxLength, remainingDepth, cpLimit, ignoreProgress)));
                        threads[i].Start();
                    }
                    foreach (var thread in threads) thread.Join();
                    lock (_lockObject) _countdown += 1;
                    break;
                }
                default:
                {
                    foreach (var action in actions)
                    {
                        state.Add(SubDfsActionTreeInner(node, action, actions, failureCondition, successCondition, successCallback, nodeScore, prevScore, maxLength, remainingDepth, cpLimit, ignoreProgress));
                    }
                    break;
                }
            }
            return state;
        }
        private DfsState SubDfsActionTreeInner(ActionNode node, Action action, Action[] actions, FailureCondition failureCondition, SuccessCondition successCondition, SuccessCallback successCallback, NodeScore nodeScore, double prevScore, int maxLength, int remainingDepth, double cpLimit, bool ignoreProgress)
        {
            var dfsState = new DfsState();
            LightState? state = _sim.Simulate(action, node.State, false);

            if (state == null || failureCondition(state.Value)) return dfsState;

            double score = ScoreState(state, ignoreProgress: ignoreProgress);
            if (score < 0) return dfsState;
            if (_sim.Crafter.CP - state.Value.CP > cpLimit) return dfsState;
            if (action.ActiveTurns <= 0 && score <= prevScore) return dfsState;

            ActionNode newNode;
            lock (node)
            {
                newNode = node.Add(action, state.Value);
            }
            
            dfsState.NodesGenerated++;
            dfsState.Add(SubDfsActionTree(newNode, actions, failureCondition, successCondition, successCallback, nodeScore, score, maxLength, remainingDepth - 1, cpLimit, ignoreProgress));

            if (successCondition(state.Value, score))
            {
                dfsState.SolutionCount++;
                List<Action> path = node.GetPath(state.Value.Step - 1);
                path.Add(action);
                var s = _sim.Simulate(path, false);
                successCallback((int)nodeScore(path, score), path);
            }

            lock (newNode.Parent)
            {
                newNode.Parent.Remove(newNode);
            }

            if (remainingDepth == maxLength) _logger($"-> {action.Name} ({dfsState.NodesGenerated} generated)");
            return dfsState;
        }

        private void GenerateDurabilityLists(double cpMax)
        {
            Dictionary<Action, int> actionChoices = _durabilityActions.ToDictionary(action => action, _ => 999);
            IEnumerable<List<Action>> results = _durabilityActions.Select(action => new List<Action> { action });

            do
            {
                results = results.SelectMany(result => Iterate(result, actionChoices, true)).Where(result => result.Sum(action => action.CPCost) <= cpMax).ToList();
                _durabilityLists.AddRange(results.Select(x => new KeyValuePair<double, List<Action>>(x.Sum(y => y.CPCost), x)));
            } while (results.Any());
        }
        #endregion

        #region Mergers
        #region Delegates
        //private delegate KeyValuePair<double, List<Action>>? SuccessfulCombinationCallback(List<Action> actions, double score, double cpMax);
        //private KeyValuePair<double, List<Action>>? MergeProgressSuccessCallback(List<Action> actions, double score, double cpMax)
        //{
        //    var solution = InsertDurability(actions, cpMax);
        //    if (solution == null) return null;

        //    LightState? s = _sim.Simulate(solution.Value.Value);
        //    if (s == null) return null;

        //    KeyValuePair<double, List<Action>> foundSolution = new(ScoreState(s), solution.Value.Value);
        //    _logger($"[{DateTime.Now}] Best Score: {foundSolution.Key}\n\t{string.Join(",", foundSolution.Value.Select(x => x.ShortName))}");

        //    return foundSolution;
        //}
        //private KeyValuePair<double, List<Action>>? MergeDurabilitySuccessCallback(List<Action> actions, double score, double cpMax)
        //{
        //    _bestSolution = new KeyValuePair<double, List<Action>>(score, actions);
        //    return _bestSolution;
        //}
        #endregion

        private void Merger()
        {
            lock (_scoredQualityLists)
            {
                if (_scoredQualityLists.Count >= QualityListMaxLength)
                {
                    _mergeEvent.Set();
                    _logger($"[{DateTime.Now}] QUALITY MERGING TIME YOOO {_scoredQualityLists.Count}");
                    _qualityLists.PushRange(_scoredQualityLists.Take(QualityListMaxLength)
                        .OrderBy(x => x.Key)
                        .Select(x => new KeyValuePair<double, List<Action>>(x.Key, x.Value)).ToArray());
                    _scoredQualityLists.Clear();
                }
            }

            while (_qualityLists.TryPop(out var qualityList))
            {
                if (_bestSolution.HasValue && _bestSolution.Value.Key - 102 > qualityList.Key) continue;

                List<Action> leftList = qualityList.Value;
                if (leftList.Any(x => x.Equals(Atlas.Actions.DelicateSynthesis))) continue; // todo: really need to figure out how to merge delicate synthesis

                for (int i = 0; i < _progressLists.Count; i++)
                {
                    List<Action> rightList = _progressLists[i];
                    double cpMax = _sim.Crafter.CP - ListToCpCost(leftList, false) - ListToCpCost(rightList, false);
                    if (cpMax < 0) continue;

                    double durabilityCost = leftList.Sum(x => x.DurabilityCost) + rightList.Sum(x => x.DurabilityCost) + 5 - rightList[^1].DurabilityCost;
                    double durabilityNeed = durabilityCost - _sim.Recipe.Durability;
                    if (!FetchDurabilityLists(leftList.Concat(rightList), cpMax, durabilityNeed).Any()) continue;

                    int[] progressMerger = new int[leftList.Count + rightList.Count];
                    for (int j = 0; j < progressMerger.Length; j++) progressMerger[j] = 0;
                    int progressLeft = progressMerger.Length;

                    do
                    {
                        if (progressLeft != leftList.Count) continue;
                        if (progressMerger[^1] == 0) continue;
                        if (Atlas.Actions.FirstRoundActions.Contains(leftList[0]) && progressMerger[0] == 1) continue;
                        if (Atlas.Actions.FirstRoundActions.Contains(rightList[0]) && progressMerger[0] == 0) continue;

                        List<Action> preDurabilityActions = ZipLists(leftList, rightList, progressMerger);
                        LightState? progressState = _sim.Simulate(preDurabilityActions, false);
                        if (!progressState.HasValue || progressState.Value.Progress < _sim.Recipe.Difficulty) continue;

                        double progressScore = ScoreState(progressState);
                        if (_bestSolution != null && progressScore - _bestSolution.Value.Key < 1) continue;

                        // insert durability
                        cpMax = progressState.Value.CP;
                        durabilityCost = preDurabilityActions.Sum(x => x.DurabilityCost) + 5 - preDurabilityActions[^1].DurabilityCost;
                        durabilityNeed = durabilityCost - _sim.Recipe.Durability;
                        List<List<Action>> durabilityLists = FetchDurabilityLists(preDurabilityActions, cpMax, durabilityNeed).ToList();


                        for (int j = 0; j < durabilityLists.Count; j++)
                        {
                            List<Action> durabilityList = durabilityLists[j];
                            int[] durabilityMerger = new int[preDurabilityActions.Count + durabilityList.Count];
                            for (int k = 0; k < durabilityList.Count; k++) durabilityMerger[k] = 0;
                            int durabilityLeft = durabilityMerger.Length;

                            do
                            {
                                if (durabilityLeft != preDurabilityActions.Count) continue;
                                if (durabilityMerger[^1] == 1) continue;
                                if (Atlas.Actions.FirstRoundActions.Contains(preDurabilityActions[0]) && durabilityMerger[0] == 1) continue;

                                List<Action> postDurabilityActions = ZipLists(preDurabilityActions, durabilityList, durabilityMerger);
                                LightState? durabilityState = _sim.Simulate(postDurabilityActions);
                                if (!durabilityState.HasValue || durabilityState.Value.Progress < _sim.Recipe.Difficulty) continue;

                                double durabilityScore = ScoreState(durabilityState);
                                if (_bestSolution != null && durabilityScore - _bestSolution.Value.Key < 1) continue;

                                _logger($"[{DateTime.Now}] New Best Solution Found ({durabilityScore}):");
                                _logger($"\t{string.Join(",", postDurabilityActions.Select(x => x.ShortName))}");
                                _bestSolution = new(durabilityScore, postDurabilityActions);
                            } while (Iterate(durabilityMerger, durabilityMerger.Length - 1, ref durabilityLeft));
                        }
                    } while (Iterate(progressMerger, progressMerger.Length - 1, ref progressLeft));
                }
            }
            _mergeEvent.Reset();
        }

        private IEnumerable<List<Action>> FetchDurabilityLists(IEnumerable<Action> actions, double cpMax, double durabilityNeed)
        {
            return _durabilityLists
                .Where(x => x.Key <= cpMax && MaxDurabilityGain(x.Value, actions) >= durabilityNeed)
                .Select(x => x.Value);
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
            double dur = state.Value.Durability / _sim.Recipe.Durability;
            double extraCredit = success ? 1000 : (cp + dur) * 10;

            return (progress + quality) * 100;
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
        private static double MaxDurabilityGain(List<Action> durabilityActions, IEnumerable<Action> actions)
        {
            double maxGain = 0;
            int wnRounds = 0;
            int manipRounds = durabilityActions.Count + actions.Count() - 1;
            
            foreach (var action in durabilityActions)
            {
                switch (action.ShortName)
                {
                    case "mastersMend":
                        maxGain += 30;
                        break;
                    case "wasteNot":
                        wnRounds += 4;
                        break;
                    case "wasteNot2":
                        wnRounds += 8;
                        break;
                    case "manipulation":
                        for (int i = 0; i < 8; i++)
                        {
                            if (manipRounds <= 0) continue;

                            manipRounds--;
                            maxGain += 5;
                        }
                        break;
                }
            }

            wnRounds = Math.Min(wnRounds, actions.Count());
            maxGain += actions.OrderByDescending(x => x.DurabilityCost).Take(wnRounds).Sum(x => x.DurabilityCost / 2);

            return maxGain;
        }
        private static List<List<Action>> Iterate(List<Action> prevSet, Dictionary<Action, int> actionChoices, bool qualityOnly)
        {
            List<List<Action>> newSets = new List<List<Action>>();
            Dictionary<Action, int> counts = new Dictionary<Action, int>();
            foreach (var group in prevSet.GroupBy(x => x.ID))
            {
                counts.Add(group.First(), group.Count());
            }

            var remainingActions = actionChoices.Where(x => !counts.ContainsKey(x.Key) || x.Value > counts[x.Key]).Select(x => x.Key).ToList();
            if (!qualityOnly && !remainingActions.Any(x => x.ProgressIncreaseMultiplier > 0)) return newSets;

            foreach (Action action in remainingActions)
            {
                if (action == prevSet[^1] && Atlas.Actions.Buffs.Contains(action)) continue;

                List<Action> newSet = prevSet.ToList();
                newSet.Add(action);
                newSets.Add(newSet);
            }
            return newSets;
        }
        private static bool Iterate(int[] arr, int ix, ref int leftCount)
        {
            if (ix == -1) return false;

            arr[ix] = (arr[ix] + 1) % 2;
            leftCount += arr[ix] == 0 ? 1 : -1;

            return arr[ix] == 1 || Iterate(arr, ix - 1, ref leftCount);
        }
        private static List<Action> ZipLists(List<Action> left, List<Action> right, int[] merger)
        {
            int l = 0, r = 0;
            List<Action> actions = new List<Action>(merger.Length);
            for (int i = 0; i < merger.Length; i++)
            {
                actions.Insert(i, merger[i] == 0 ? left[l++] : right[r++]);
            }
            return actions;
        }
        #endregion
    }
}

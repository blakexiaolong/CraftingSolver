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
        private const int QualityListMaxLength = 100000000; // 100 million

        private const int ProgressSetMaxLength = 7;
        private const int QualitySetMaxLength = 12;

        private readonly LightSimulator _sim;
        private readonly LightState _startState;
        private readonly LoggingDelegate _logger;

        private readonly Action[] _progressActions, _qualityActions, _durabilityActions;

        private int _countdown = 4;
        private readonly object _lockObject = new();

        private readonly ConcurrentBag<KeyValuePair<double, List<Action>>> _scoredProgressLists = new(), _scoredQualityLists = new();
        private List<List<Action>> _progressLists = new();
        private KeyValuePair<double, List<Action>>? _bestSolution;

        public JaboaSolver(LightSimulator sim, LoggingDelegate loggingDelegate)
        {
            _sim = sim;
            _logger = loggingDelegate;
            _startState = _sim.Simulate(new List<Action>())!.Value;

            // ReSharper disable once HeapView.ObjectAllocation.Evident
            _progressActions = new Action[Atlas.Actions.ProgressActions.Length + Atlas.Actions.ProgressBuffs.Length];
            Atlas.Actions.ProgressActions.CopyTo(_progressActions, 0);
            Atlas.Actions.ProgressBuffs.CopyTo(_progressActions, Atlas.Actions.ProgressActions.Length);

            // ReSharper disable once HeapView.ObjectAllocation.Evident
            _qualityActions = new Action[Atlas.Actions.QualityActions.Length + Atlas.Actions.QualityBuffs.Length - 1];
            Atlas.Actions.QualityActions.CopyTo(_qualityActions, 0);
            Atlas.Actions.QualityBuffs.CopyTo(_qualityActions, Atlas.Actions.QualityActions.Length - 1);

            _durabilityActions = Atlas.Actions.DurabilityActions.ToArray();
        }

        public List<Action>? Run(int maxTasks)
        {
            int cp = _sim.Crafter.CP + (int)(_sim.Recipe.Durability * MaxDurabilityCost);

            _logger($"\n[{DateTime.Now}] Generating Progress Combinations");
            GenerateDfsActionTree(_progressActions, ProgressFailure, ProgressSuccess, ProgressSuccessCallback, ProgressScore, ProgressSetMaxLength, cp, ignoreProgress: false);
            _progressLists = _scoredProgressLists.OrderBy(x => x.Key).Select(x=>x.Value).ToList();
            int cpCost = (int)ListToCpCost(_progressLists.First());
            cp = _sim.Crafter.CP + (int)(_sim.Recipe.Durability * MaxDurabilityCost) - cpCost;
            _logger($"\t{string.Join(",", _progressLists.First().Select(x => x.ShortName))}");
            _logger($"[{DateTime.Now}] CP Cost: {cpCost}; CP Remaining: {cp}");

            _logger($"\n[{DateTime.Now}] Generating Quality Combinations");
            GenerateDfsActionTree(_qualityActions, QualityFailure, QualitySuccess, QualitySuccessCallback, QualityScore, QualitySetMaxLength, cp, ignoreProgress: true);

            QualityMerge();
            return _bestSolution?.Value;
        }

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
            return; // todo: remove this, its just to count the number of nodes
            if (_bestSolution != null && score < _bestSolution.Value.Key) return;
            _scoredQualityLists.Add(new(score, actions));
            if (_scoredQualityLists.Count < QualityListMaxLength) return;

            QualityMerge();
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
            return -1 * score;
        }

        private delegate KeyValuePair<double, List<Action>>? SuccessfulCombinationCallback(List<Action> actions, double score, double cpMax);

        private KeyValuePair<double, List<Action>>? MergeProgressSuccessCallback(List<Action> actions, double score, double cpMax)
        {
            var solution = InsertDurability(actions, cpMax);
            if (solution == null) return null;

            LightState? s = _sim.Simulate(solution.Value.Value);
            if (s == null) return null;

            KeyValuePair<double, List<Action>> foundSolution = new(ScoreState(s), solution.Value.Value);
            _logger($"[{DateTime.Now}] Best Score: {foundSolution.Key}\n\t{string.Join(",", foundSolution.Value.Select(x => x.ShortName))}");

            return foundSolution;
        }
        private KeyValuePair<double, List<Action>>? MergeDurabilitySuccessCallback(List<Action> actions, double score, double cpMax)
        {
            _bestSolution = new KeyValuePair<double, List<Action>>(score, actions);
            return _bestSolution;
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
                var s = SubDfsActionTree(head, actions, failureCondition, successCondition, successCallback, nodeScore, maxLength, maxLength, cpLimit, ignoreProgress);
                _logger($"[{DateTime.Now}] Nodes Generated: {s.NodesGenerated} | Solutions Found: {s.SolutionCount}");
            }
            finally
            {
                GCSettings.LatencyMode = oldMode;
            }
        }
        private DfsState SubDfsActionTree(ActionNode node, Action[] actions, FailureCondition failureCondition, SuccessCondition successCondition, SuccessCallback successCallback, NodeScore nodeScore, int maxLength, int remainingDepth, double cpLimit, bool ignoreProgress)
        {
            DfsState state = new();
            if (remainingDepth <= 0) return state;
            if (remainingDepth > 4 && _countdown > 0)
            {
                lock (_lockObject) _countdown = Math.Max(_countdown - 1, 0);
                Thread[] threads = new Thread[actions.Length];
                for (int i = 0; i < threads.Length; i++)
                {
                    Action action = actions[i];
                    threads[i] = new Thread(() => state.Add(SubDfsActionTreeInner(node, action, actions, failureCondition, successCondition, successCallback, nodeScore, maxLength, remainingDepth, cpLimit, ignoreProgress)));
                    threads[i].Start();
                }
                foreach (var thread in threads) thread.Join();
                lock (_lockObject) _countdown += 1;
            }
            else
            {
                foreach (var action in actions)
                {
                    state.Add(SubDfsActionTreeInner(node, action, actions, failureCondition, successCondition, successCallback, nodeScore, maxLength, remainingDepth, cpLimit, ignoreProgress));
                }
            }
            return state;
        }

        private DfsState SubDfsActionTreeInner(ActionNode node, Action action, Action[] actions, FailureCondition failureCondition, SuccessCondition successCondition, SuccessCallback successCallback, NodeScore nodeScore, int maxLength, int remainingDepth, double cpLimit, bool ignoreProgress)
        {
            var dfsState = new DfsState();
            LightState? state = _sim.Simulate(action, node.State, false);

            if (state == null || failureCondition(state.Value)) return dfsState;

            double score = ScoreState(state, ignoreProgress: ignoreProgress);
            if (score <= 0) return dfsState;
            if (_sim.Crafter.CP - state.Value.CP > cpLimit) return dfsState;

            ActionNode newNode;
            dfsState.NodesGenerated++;
            lock (node)
            {
                newNode = node.Add(action, state.Value);
            }
            dfsState.Add(SubDfsActionTree(newNode, actions, failureCondition, successCondition, successCallback, nodeScore, maxLength, remainingDepth - 1, cpLimit, ignoreProgress));

            if (successCondition(state.Value, score))
            {
                dfsState.SolutionCount++;
                List<Action> path = node.GetPath(state.Value.Step - 1);
                path.Add(action);
                successCallback((int)nodeScore(path, score), path);
            }

            lock (newNode.Parent)
            {
                newNode.Parent.Remove(newNode);
            }

            if (remainingDepth == maxLength) _logger($"-> {action.Name} ({dfsState.NodesGenerated} generated)");
            return dfsState;
        }

        private void QualityMerge()
        {
            _logger($"[{DateTime.Now}] QUALITY MERGING TIME YOOO");
            List<KeyValuePair<double, List<Action>>> qualityLists = _scoredQualityLists.OrderBy(x => x.Key)
                .Select(x => new KeyValuePair<double, List<Action>>(x.Key, x.Value))
                .ToList();
            _scoredQualityLists.Clear();
            for (int i = 0; i < qualityLists.Count; i++)
            {
                double key = CombineActionLists(qualityLists[i].Value, _progressLists, progressLeft: false, useDurability: false, MergeProgressSuccessCallback)?.Key ?? -1;
                if (key <= -1) continue;

                int index = qualityLists.FindIndex(x => x.Key < key);
                if (index < 0) continue;

                for (int j = index; j < qualityLists.Count; j++)
                {
                    qualityLists.RemoveAt(index);
                }
            }
        }
        private KeyValuePair<double, List<Action>>? CombineActionLists(List<Action> leftList, List<List<Action>> right, bool progressLeft, bool useDurability, SuccessfulCombinationCallback callback)
        {
            KeyValuePair<double, List<Action>>? bestSolution = null;
            if (leftList.Any(x => x.Equals(Atlas.Actions.DelicateSynthesis))) return bestSolution; // todo: really need to figure out how to merge delicate synthesis
            
            for (int i = 0; i < right.Count; i++)
            {
                List<Action> rightList = right[i];
                double cpMax = _sim.Crafter.CP - ListToCpCost(leftList, false) - ListToCpCost(rightList, false);
                if (cpMax < 0) return bestSolution;

                int[] merger = new int[leftList.Count + rightList.Count];
                for (int j = 0; j < merger.Length; j++) merger[j] = 0;

                do
                {
                    if (merger[^1] == (progressLeft ? 1 : 0)) continue; // solutions should end with a progress action
                    if (Atlas.Actions.FirstRoundActions.Contains(leftList[0]) && merger[0] == 1) continue;
                    if (Atlas.Actions.FirstRoundActions.Contains(rightList[0]) && merger[0] == 0) continue;

                    List<Action>? actions = ZipLists(leftList, rightList, merger);
                    if (actions == null) continue;

                    LightState? s = _sim.Simulate(actions, useDurability);
                    if (!s.HasValue || s.Value.Progress < _sim.Recipe.Difficulty) continue;

                    double score = ScoreState(s);
                    if (_bestSolution != null && score - _bestSolution.Value.Key < 1) continue;

                    bestSolution = callback(actions, score, cpMax);
                } while (Iterate(merger, merger.Length - 1));
            }

            return bestSolution;
        }
        private KeyValuePair<double, List<Action>>? InsertDurability(List<Action> actions, double cpMax)
        {
            List<List<Action>> durabilitySolutions = new();
            Dictionary<Action, int> actionChoices = _durabilityActions.ToDictionary(action => action, _ => 999);
            IEnumerable<List<Action>> results = _durabilityActions.Select(action => new List<Action> { action });

            double durabilityCost = actions.Sum(x => x.DurabilityCost) - actions[^1].DurabilityCost;
            double durabilityNeed = durabilityCost - _sim.Recipe.Durability;
            if (durabilityNeed <= 0)
            {
                return new(ScoreState(_sim.Simulate(actions)), actions);
            }

            do
            {
                results = results.SelectMany(result => Iterate(result, actionChoices, true)).Where(result => result.Sum(action => action.CPCost) <= cpMax).ToList();
                durabilitySolutions.AddRange(results.Where(result => MaxDurabilityGain(result, actions) >= durabilityNeed));
            } while (results.Any());

            var solution = CombineActionLists(actions, durabilitySolutions, progressLeft: true, useDurability: true, MergeDurabilitySuccessCallback);
            return solution;
        }

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
        private static double MaxDurabilityGain(List<Action> durabilityActions, List<Action> actions)
        {
            double maxGain = 0;
            int wnIndex = 0;
            int manipRounds = durabilityActions.Count + actions.Count - 1;
            foreach (var action in durabilityActions)
            {
                switch (action.ShortName)
                {
                    case "mastersMend":
                        maxGain += 30;
                        break;
                    case "wasteNot":
                        for (int i = 0; i < 4; i++)
                        {
                            if (actions.Count > wnIndex + i)
                            {
                                maxGain += actions[wnIndex + i].DurabilityCost / 2F;
                            }
                        }
                        wnIndex += 4;
                        break;
                    case "wasteNot2":
                        for (int i = 0; i < 8; i++)
                        {
                            if (actions.Count > wnIndex + i)
                            {
                                maxGain += actions[wnIndex + i].DurabilityCost / 2F;
                            }
                        }
                        wnIndex += 8;
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
        private static bool Iterate(int[] arr, int ix)
        {
            if (ix == -1)
                return false;
            arr[ix] = (arr[ix] + 1) % 2;
            return arr[ix] != 0 || Iterate(arr, ix - 1);
        }
        private static List<Action>? ZipLists(List<Action> left, List<Action> right, int[] merger)
        {
            int lCount = 0, rCount = 0;
            List<Action> actions = new List<Action>(merger.Length);
            for (int i = 0; i < merger.Length; i++)
            {
                if (merger[i] == 0)
                {
                    if (lCount >= left.Count) return null;
                    actions.Insert(i, left[lCount++]);
                }
                else
                {
                    if (rCount >= right.Count) return null;
                    actions.Insert(i, right[rCount++]);
                }
            }
            return actions;
        }
        #endregion
    }
}

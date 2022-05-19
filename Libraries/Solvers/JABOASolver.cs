using static Libraries.Solver;

namespace Libraries.Solvers
{
    // Just a Bunch of Actions
    public class JaboaSolver : ISolver
    {
        private const double MaxDurabilityCost = 88 / 30D;
        private const double MinDurabilityCost = 96 / 40D;

        private const int ProgressSetMaxLength = 7;
        private const int QualitySetMaxLength = 8;

        private readonly LightSimulator _sim;
        private readonly LightState _startState;
        private readonly LoggingDelegate _logger;

        private readonly Action[] progressActions, qualityActions, durabilityActions;

        private List<KeyValuePair<double, List<Action>>> _scoredProgressLists = new();
        private List<List<Action>> _progressLists = new();
        private KeyValuePair<double, List<Action>>? _bestSolution = null;

        public JaboaSolver(LightSimulator sim, LoggingDelegate loggingDelegate)
        {
            _sim = sim;
            _logger = loggingDelegate;
            _startState = _sim.Simulate(new List<Action>())!.Value;

            progressActions = new Action[Atlas.Actions.ProgressActions.Length + Atlas.Actions.ProgressBuffs.Length];
            Atlas.Actions.ProgressActions.CopyTo(progressActions, 0);
            Atlas.Actions.ProgressBuffs.CopyTo(progressActions, Atlas.Actions.ProgressActions.Length);

            qualityActions = new Action[Atlas.Actions.QualityActions.Length + Atlas.Actions.QualityBuffs.Length - 1];
            Atlas.Actions.QualityActions.CopyTo(qualityActions, 0);
            Atlas.Actions.QualityBuffs.CopyTo(qualityActions, Atlas.Actions.QualityActions.Length - 1);

            durabilityActions = Atlas.Actions.DurabilityActions.ToArray();
        }

        public double ScoreState(LightState? state, bool ignoreProgress = false)
        {
            if (state == null) return -1;

            if (!state.Value.Success(_sim))
            {
                var violations = state.Value.CheckViolations(_sim);
                if (!violations.DurabilityOk || !violations.CpOk) return -1;
            }
            double progress = ignoreProgress ? 0 : (state.Value.Progress > _sim.Recipe.Difficulty ? _sim.Recipe.Difficulty : state.Value.Progress) / _sim.Recipe.Difficulty;
            double maxQuality = _sim.Recipe.MaxQuality * 1.1;
            double quality = (state.Value.Quality > maxQuality ? maxQuality : state.Value.Quality) / _sim.Recipe.MaxQuality;
            double cp = state.Value.CP / _sim.Crafter.CP;
            double dur = state.Value.Durability / _sim.Recipe.Durability;
            return (progress + quality) * 100 + (cp + dur) * 10;
        }

        public List<Action>? Run(int maxTasks)
        {
            int cp = _sim.Crafter.CP + (int)(_sim.Recipe.Durability * MaxDurabilityCost);

            _logger($"\n[{DateTime.Now}] Generating Progress Combinations");
            GenerateDfsActionTree(progressActions, ProgressFailure, ProgressSuccess, ProgressSuccessCallback, ProgressScore, ProgressSetMaxLength, cp, ignoreProgress: false);
            _progressLists = _scoredProgressLists.OrderBy(x => x.Key).Select(x=>x.Value).ToList();
            int cpCost = (int)ListToCpCost(_progressLists.First());
            cp = _sim.Crafter.CP + (int)(_sim.Recipe.Durability * MaxDurabilityCost) - cpCost;
            _logger($"[{DateTime.Now}] Good Lists Found: {_progressLists.Count}\n\t{string.Join(",", _progressLists.First().Select(x => x.ShortName))}");
            _logger($"[{DateTime.Now}] CP Cost: {cpCost}; CP Remaining: {cp}");

            _logger($"\n[{DateTime.Now}] Generating Quality Combinations");
            GenerateDfsActionTree(qualityActions, QualityFailure, QualitySuccess, QualitySuccessCallback, QualityScore, QualitySetMaxLength, cp, ignoreProgress: true);

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
            _scoredProgressLists.Add(new KeyValuePair<double, List<Action>>(score, actions));
        }
        private void QualitySuccessCallback(double score, List<Action> actions)
        {
            if (_bestSolution != null && score < _bestSolution.Value.Key) return;
            CombineActionLists(actions, _progressLists, progressLeft: false, useDurability: false, MergeProgressSuccessCallback);
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

            LightState? s = _sim.Simulate(solution.Value.Value, _startState);
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
            long nodesGenerated = 0, solutionCount = 0;
            ActionNode head = new ActionNode(null, _startState, null!);
            SubDfsActionTree(head, actions, failureCondition, successCondition, successCallback, nodeScore, maxLength, maxLength, cpLimit, ignoreProgress, ref nodesGenerated, ref solutionCount);
            _logger($"[{DateTime.Now}] Nodes Generated: {nodesGenerated}");
            _logger($"[{DateTime.Now}] Sorted");
        }
        private void SubDfsActionTree(ActionNode node, Action[] actions, FailureCondition failureCondition, SuccessCondition successCondition, SuccessCallback successCallback, NodeScore nodeScore, int maxLength, int remainingDepth, double cpLimit, bool ignoreProgress, ref long nodesGenerated, ref long solutionCount)
        {
            if (remainingDepth <= 0) return;
            foreach (Action action in actions)
            {
                LightState? state = _sim.Simulate(action, node.State!.Value, false);
                if (state == null || failureCondition(state.Value)) continue;

                double score = ScoreState(state, ignoreProgress: ignoreProgress);
                if (score <= 0) continue;

                List<Action> path = node.GetPath();
                path.Add(action);
                if (ListToCpCost(path) > cpLimit) continue;

                nodesGenerated++;
                ActionNode newNode = node.Add(action, state.Value);
                SubDfsActionTree(newNode, actions, failureCondition, successCondition, successCallback, nodeScore, maxLength, remainingDepth - 1, cpLimit, ignoreProgress, ref nodesGenerated, ref solutionCount);

                if (successCondition(state.Value, score))
                {
                    solutionCount++;
                    successCallback((int)nodeScore(path, score), path);
                }

                newNode.Parent?.Children.Remove(newNode);

                if (remainingDepth == maxLength)
                {
                    _logger($"{action.ShortName} {{{maxLength}}}: {nodesGenerated} generated, {solutionCount} solutions");
                }
            }
        }
        
        private KeyValuePair<double, List<Action>>? CombineActionLists(List<Action> leftList, List<List<Action>> right, bool progressLeft, bool useDurability, SuccessfulCombinationCallback callback)
        {
            KeyValuePair<double, List<Action>>? bestSolution = null;
            if (leftList.Any(x => x.Equals(Atlas.Actions.DelicateSynthesis))) return bestSolution; // todo: really need to figure out how to merge delicate synthesis
            // double qualityScore = ScoreState(_sim.Simulate(leftList, _startState, useDurability: false));
            for (int i = 0; i < right.Count; i++)
            {
                List<Action> rightList = right[i];
                double cpMax = _sim.Crafter.CP - ListToCpCost(leftList, false) - ListToCpCost(rightList, false);
                if (cpMax <= 0) return bestSolution; // todo: possible edge case where all CP is consumed, but durability actions aren't needed resulting in no actual solution being selected

                int[] merger = new int[leftList.Count + rightList.Count];
                for (int j = 0; j < merger.Length; j++) merger[j] = 0;

                do
                {
                    if (merger[^1] == (progressLeft ? 1 : 0)) continue; // solutions should end with a progress action
                    if (Atlas.Actions.FirstRoundActions.Contains(leftList[0]) && merger[0] == 1) continue;
                    if (Atlas.Actions.FirstRoundActions.Contains(rightList[0]) && merger[0] == 0) continue;

                    List<Action>? actions = ZipLists(leftList, rightList, merger);
                    if (actions == null) continue;

                    LightState? s = _sim.Simulate(actions, _startState, useDurability);
                    if (s == null || s.Value.Progress < _sim.Recipe.Difficulty) continue;

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
            Dictionary<Action, int> actionChoices = durabilityActions.ToDictionary(action => action, action => 999);
            IEnumerable<List<Action>> results = durabilityActions.Select(action => new List<Action> { action });

            double durabilityCost = actions.Sum(x => x.DurabilityCost) - actions[^1].DurabilityCost;
            double durabilityNeed = durabilityCost - _sim.Recipe.Durability;
            if (durabilityNeed <= 0)
            {
                return new(ScoreState(_sim.Simulate(actions, _startState)), actions);
            }

            do
            {
                results = results.SelectMany(result => Iterate(result, actionChoices, true)).Where(result => result.Sum(action => action.CPCost) <= cpMax);
                durabilitySolutions.AddRange(results.Where(result => MaxDurabilityGain(result, actions) >= durabilityNeed));
            } while (results.Any());

            var solution = CombineActionLists(actions, durabilitySolutions, progressLeft: true, useDurability: true, MergeDurabilitySuccessCallback);
            return solution;
        }

        #region Utilities
        public static double ListToCpCost(List<Action> list, bool considerDurability = true)
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
                                maxGain += actions[wnIndex + i].DurabilityCost / 2;
                            }
                        }
                        wnIndex += 4;
                        break;
                    case "wasteNot2":
                        for (int i = 0; i < 8; i++)
                        {
                            if (actions.Count > wnIndex + i)
                            {
                                maxGain += actions[wnIndex + i].DurabilityCost / 2;
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

            var remainingActions = actionChoices.Where(x => !counts.ContainsKey(x.Key) || x.Value > counts[x.Key]).Select(x => x.Key);
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

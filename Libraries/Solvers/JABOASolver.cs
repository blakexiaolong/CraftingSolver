using System.Diagnostics;

using CraftingSolver;
using Action = CraftingSolver.Action;
using static CraftingSolver.Solver;

namespace Libraries.Solvers
{
    public delegate void LoggingDelegate(string message);

    // Just a Bunch of Actions
    public class JABOASolver
    {
        private static double maxDurabilityCost = 88 / 30D;
        private static double minDurabilityCost = 96 / 40D;

        private const int PROGRESS_SET_MAX_LENGTH = 7;
        private const int QUALITY_SET_MAX_LENGTH = 7;
        private const int SOLUTION_MAX_COUNT = 10000000;

        private LoggingDelegate _logger = (string message) => Debug.WriteLine(message);

        public List<Action> Run(Simulator sim, int maxTasks, LoggingDelegate loggingDelegate = null)
        {
            if (loggingDelegate != null) _logger = loggingDelegate;

            int cp = sim.Crafter.CP + (int)(sim.Recipe.Durability * maxDurabilityCost);
            State startState = sim.Simulate(null, new State(sim, null));

            Action[] progressActions = new Action[Atlas.Actions.ProgressActions.Length + Atlas.Actions.ProgressBuffs.Length];
            Atlas.Actions.ProgressActions.CopyTo(progressActions, 0);
            Atlas.Actions.ProgressBuffs.CopyTo(progressActions, Atlas.Actions.ProgressActions.Length);
            Action[] qualityActions = new Action[Atlas.Actions.QualityActions.Length + Atlas.Actions.QualityBuffs.Length - 1];
            Atlas.Actions.QualityActions.Where(x => !x.Equals(Atlas.Actions.Reflect)).ToList().CopyTo(qualityActions, 0);
            Atlas.Actions.QualityBuffs.CopyTo(qualityActions, Atlas.Actions.QualityActions.Length - 1);
            Action[] durabilityActions = Atlas.Actions.DurabilityActions.ToArray();

            _logger($"\n[{DateTime.Now}] Generating Progress Combinations");
            List<KeyValuePair<double, List<Action>>> progressLists = GenerateActionTree(sim, startState, progressActions, ProgressSuccess, ProgressScore, PROGRESS_SET_MAX_LENGTH, cp, ignoreProgress: false);
            int cpCost = (int)ListToCpCost(progressLists.First().Value);
            cp = sim.Crafter.CP + (int)(sim.Recipe.Durability * maxDurabilityCost) - cpCost;
            _logger($"[{DateTime.Now}] Good Lists Found: {progressLists.Count()}\n\t{string.Join(",", progressLists.First().Value.Select(x => x.ShortName))}");
            _logger($"[{DateTime.Now}] CP Cost: {cpCost}; CP Remaining: {cp}");

            _logger($"\n[{DateTime.Now}] Generating Quality Combinations");
            //List<KeyValuePair<double, List<Action>>> qualityLists = GenerateActionTree(sim, startState, qualityActions, QualitySuccess, QualityScore, QUALITY_SET_MAX_LENGTH, cp, ignoreProgress: true);
            List<KeyValuePair<double, List<Action>>> qualityLists = GenerateDFSActionTree(sim, startState, qualityActions, QualitySuccess, QualityScore, QUALITY_SET_MAX_LENGTH, cp, ignoreProgress: true);
            cpCost = (int)ListToCpCost(qualityLists.First().Value);
            cp -= cpCost;
            _logger($"[{DateTime.Now}] Good Lists Found: {qualityLists.Count()}\n\t{string.Join(",", qualityLists.First().Value.Select(x => x.ShortName))}");
            _logger($"[{DateTime.Now}] CP Cost: {cpCost}; CP Remaining: {cp}");

            // zip progress and quality together
            bool skipOut = false;
            int progressIx = 0;
            Tuple<double, List<Action>> bestSolution = new(0, new List<Action>());
            for (int qualityIx = 0; qualityIx < qualityLists.Count && !skipOut; qualityIx++)
            {
                var qualityList = qualityLists[qualityIx].Value;
                var progressList = progressLists[progressIx].Value;

                double cpRemaining = sim.Crafter.CP - ListToCpCost(progressList, considerDurability: false) + ListToCpCost(qualityList, considerDurability: false);
                int durabilityUsed = progressList.Sum(x => x.DurabilityCost) + qualityList.Sum(x => x.DurabilityCost) - progressList[^1].DurabilityCost; // this doesn't consider the last progress action, since the game doesn't either
                int durabilityNeeded = durabilityUsed - sim.Recipe.Durability;
                // if (durabilityNeeded * minDurabilityCost > cpRemaining) continue;  // this doesn't account for WN

                int[] merger = new int[qualityList.Count + progressList.Count];
                for (int k = 0; k < merger.Length; k++) merger[k] = 0;

                bool iterateProgress = false;
                while (Iterate(merger, merger.Length - 1))
                {
                    var actions = ZipLists(progressList, qualityList, merger);
                    if (actions == null) continue;

                    State s = LightSimulator.Simulate(sim, actions, startState, useDurability: false);
                    if (s == null || s.Progress < sim.Recipe.Difficulty) continue;
                    if (s.Step == actions.Count && s.Progress < sim.Recipe.Difficulty) iterateProgress = true;

                    var score = ScoreState(sim, s);
                    List<Action>? solution = InsertDurability(sim, startState, new() { actions }, durabilityActions, s.Cp);
                    if (solution == null) continue;

                    s = LightSimulator.Simulate(sim, solution, startState);
                    score = ScoreState(sim, s);
                    if (s == null && s.Progress < sim.Recipe.Difficulty) continue;
                    
                    if (score.Item1 > bestSolution.Item1)
                    {
                        bestSolution = new(score.Item1, solution);
                        _logger($"[{DateTime.Now}] Best Score: {bestSolution.Item1}\n\t{string.Join(",", bestSolution.Item2.Select(x => x.ShortName))}");
                        LightSimulator.Simulate(sim, solution, startState);
                    }
                    qualityLists.RemoveAll(x => x.Key <= score.Item1);
                }

                if (iterateProgress)
                {
                    progressIx++;
                    qualityIx--;
                }
                else
                {
                    progressIx = 0;
                }
            }

            if (bestSolution.Item1 > 0)
            {
                LightSimulator.Simulate(sim, bestSolution.Item2, startState);
                return bestSolution.Item2;
            }
            else
            {
                return null;
            }
        }

        public delegate bool SuccessCondition(State state);
        public static bool ProgressSuccess(State state)
        {
            return state.Success;
        }
        public static bool QualitySuccess(State state)
        {
            return true;
        }

        public delegate double NodeScore(List<Action> path, double score);
        public double ProgressScore(List<Action> path, double score)
        {
            return ListToCpCost(path);
        }
        public double QualityScore(List<Action> path, double score)
        {
            return -1 * score;
        }

        private List<KeyValuePair<double, List<Action>>> GenerateDFSActionTree(Simulator sim, State startState, Action[] actions, SuccessCondition successCondition, NodeScore nodeScore, int maxLength, double cpLimit, bool ignoreProgress)
        {
            long nodesGenerated = 0, solutionCount = 0;
            ActionNode head = new(null, startState, null);
            List<KeyValuePair<double, List<Action>>> lists = new();
            SubDFSActionTree(sim, head, actions, successCondition, nodeScore, maxLength, cpLimit, ignoreProgress, ref lists, ref nodesGenerated, ref solutionCount);
            _logger($"[{DateTime.Now}] Nodes Generated: {nodesGenerated}");
            _logger($"[{DateTime.Now}] Lists Found: {lists.Count}");

            lists = lists.OrderBy(x => x.Key).ToList();
            _logger($"[{DateTime.Now}] Sorted");

            var s = sim.Simulate(lists.First().Value, startState, useDurability: false);
            return lists;
        }

        private void SubDFSActionTree(Simulator sim, ActionNode node, Action[] actions, SuccessCondition successCondition, NodeScore nodeScore, int remainingDepth, double cpLimit, bool ignoreProgress, ref List<KeyValuePair<double, List<Action>>> lists, ref long nodesGenerated, ref long solutionCount)
        {
            if (remainingDepth < 0) return;
            foreach (Action action in actions)
            {
                nodesGenerated++;
                State state = sim.Simulate(new() { action }, node.State, useDurability: false);
                if (state.WastedActions > 0 || !successCondition(state)) continue;

                Tuple<double, bool> score = ScoreState(sim, state, ignoreProgress: ignoreProgress);
                if (score.Item1 <= 0) continue;

                ActionNode? newNode = node.Add(action, state);
                if (newNode == null) continue;

                List<Action> path = newNode.GetPath();
                if (ListToCpCost(path) > cpLimit) continue;

                solutionCount++;
                lists.Add(new KeyValuePair<double, List<Action>>(nodeScore(path, score.Item1), path));
                SubDFSActionTree(sim, newNode, actions, successCondition, nodeScore, remainingDepth - 1, cpLimit, ignoreProgress, ref lists, ref nodesGenerated, ref solutionCount);

                newNode.State = null;
                //newNode.Children = null;
            }
        }
        public List<KeyValuePair<double, List<Action>>> GenerateActionTree(Simulator sim, State startState, Action[] actions, SuccessCondition successCondition, NodeScore nodeScore, int maxLength, double cpLimit, bool ignoreProgress)
        {
            long round = 0, nodesGenerated = 0, nodesRemoved = 0, solutionCount = 0;
            ActionNode head = new(null, startState, null);
            List<ActionNode> nodesToExpand = new() { head };
            SortedList<double, List<List<Action>>> lists = new();

            do
            {
                List<ActionNode> nextNodes = new();
                for (int i = 0; i < nodesToExpand.Count; i++)
                {
                    ActionNode node = nodesToExpand[i];
                    if (node.Parent == null && round > 0) continue;
                    if (node.State == null) continue;

                    foreach (Action action in actions)
                    {
                        nodesGenerated++;
                        State state = sim.Simulate(new() { action }, node.State, useDurability: false);
                        if (state.WastedActions > 0) continue;

                        Tuple<double, bool> score = ScoreState(sim, state, ignoreProgress: ignoreProgress);
                        if (score.Item1 > 0)
                        {
                            ActionNode? newNode = node.Add(action, state);
                            if (newNode == null) continue;

                            if (successCondition(state))
                            {
                                List<Action> path = newNode.GetPath();
                                if (ListToCpCost(path) > cpLimit) continue;

                                double key = nodeScore(path, score.Item1);
                                if (!lists.ContainsKey(key)) lists.Add(key, new());

                                lists[key].Add(path);
                                solutionCount++;
                                while (solutionCount > SOLUTION_MAX_COUNT)
                                {
                                    var removeKey = lists.Keys[^1];
                                    solutionCount -= lists[removeKey].Count;
                                    foreach (var removeNode in lists[removeKey].Select(list => head.GetNode(list)).Where(removeNode => removeNode != null))
                                    {
                                        removeNode.Parent.Children.Remove(removeNode);
                                        removeNode.Parent = null;
                                        nodesRemoved++;
                                    }
                                    lists.Remove(removeKey);
                                }
                            }
                            nextNodes.Add(newNode);
                        }
                    }

                    // clear state to save memory
                    node.State = null;
                }

                _logger($"{{{round}}} {nodesGenerated} nodes generated, {nodesRemoved} nodes removed; {nodesToExpand.Count} nodes to expand");
                round++;
                nodesToExpand = nextNodes;
            } while (round < maxLength && nodesToExpand.Any());

            _logger($"[{DateTime.Now}] Nodes Generated: {nodesGenerated}");
            var s = sim.Simulate(lists.First().Value.First(), startState, useDurability: false);
            lists.First().Value.SelectMany(x => new List<Action> {  });
            return lists.SelectMany(x => x.Value.Select(y => new KeyValuePair<double, List<Action>>(x.Key, y))).ToList();
        }

        public static double ListToCpCost(List<Action> list, bool considerDurability = true)
        {
            double cpTotal = list.Sum(x => x.CPCost) + (considerDurability ? list.Sum(x => x.DurabilityCost) * minDurabilityCost : 0);
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

            return cpTotal + observeCost - comboSavings;
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
            if (arr[ix] == 0)
            {
                return Iterate(arr, ix - 1);
            }
            return true;
        }

        private static List<Action>? InsertDurability(Simulator sim, State startState, List<List<Action>> actions, Action[] durabilityActions, double cpMax)
        {
            List<List<Action>> durabilitySolutions = new();
            Dictionary<Action, int> actionChoices = new();

            foreach (var action in durabilityActions) actionChoices.Add(action, 999);
            IEnumerable<List<Action>> results = durabilityActions.Select(action => new List<Action> { action });

            do
            {
                results = results.SelectMany(result => Iterate(result, actionChoices, true)).Where(result => result.Sum(action => action.CPCost) <= cpMax);
                durabilitySolutions.AddRange(results);
            } while (results.Any());

            var solution = ZipListSets(sim, startState, actions, durabilitySolutions, true);
            return solution.Any() ? solution[^1].Value : null;
        }
        public static double MaxDurabilityGain(List<Action> durabiltyActions, List<Action> actions)
        {
            double maxGain = 0;
            int wnIndex = 0;
            int manipRounds = durabiltyActions.Count + actions.Count - 1;
            foreach (var action in durabiltyActions)
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
                            if (manipRounds > 0)
                            {
                                manipRounds--;
                                maxGain += 5;
                            }
                        }
                        break;
                }
            }
            return maxGain;
        }
        public static List<KeyValuePair<double, List<Action>>> ZipListSets(Simulator sim, State startState, List<List<Action>> left, List<List<Action>> right, bool useDurability)
        {
            SortedList<double, List<Action>> zippedLists = new();
            foreach (var leftList in left)
            {
                foreach (var rightList in right)
                {
                    if (ListToCpCost(leftList, false) + ListToCpCost(rightList, false) > sim.Crafter.CP) continue;
                    if (leftList.Sum(x => x.DurabilityCost) - leftList[^1].DurabilityCost > sim.Recipe.Durability +
                        MaxDurabilityGain(rightList, leftList.OrderBy(x => x.DurabilityCost).ToList())) continue;

                    var merger = new int[leftList.Count + rightList.Count];
                    for (var k = 0; k < merger.Length; k++) merger[k] = 0;

                    while (Iterate(merger, merger.Length - 1))
                    {
                        var actions = ZipLists(leftList, rightList, merger);
                        if (actions == null) continue;

                        var s = LightSimulator.Simulate(sim, actions.ToList(), startState, useDurability);
                        if (s == null) continue;

                        var score = ScoreState(sim, s);
                        if (s.Progress < sim.Recipe.Difficulty || zippedLists.ContainsKey(score.Item1)) continue;

                        zippedLists.Add(score.Item1, actions);
                    }
                }
            }
            return zippedLists.ToList();
        }
        private static List<Action>? ZipLists(List<Action> progress, List<Action> quality, int[] decider)
        {
            if (decider[^1] != 0) return null;

            int p = 0, q = 0;
            var actions = new Action[decider.Length];
            for (var i = 0; i < decider.Length; i++)
            {
                if (decider[i] == 0)
                {
                    if (p >= progress.Count) return null;
                    actions[i] = progress[p++];
                }
                else
                {
                    if (q >= quality.Count) return null;
                    actions[i] = quality[q++];
                }
            }
            return actions.ToList();
        }
    }
}

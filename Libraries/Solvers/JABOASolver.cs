using System.Collections;
using System.Diagnostics;
using static Libraries.Solver;

namespace Libraries.Solvers
{
    // Just a Bunch of Actions
    public class JaboaSolver : ISolver
    {
        private const double MaxDurabilityCost = 88 / 30D;
        private const double MinDurabilityCost = 96 / 40D;

        private const int ProgressSetMaxLength = 7;
        private const int QualitySetMaxLength = 7;

        private const int ActionBinarySize = 5;

        private LoggingDelegate? _logger;

        public List<Action?> Run(Simulator sim, int maxTasks, LoggingDelegate? loggingDelegate = null)
        {
            if (loggingDelegate != null) _logger = loggingDelegate;

            int cp = sim.Crafter.CP + (int)(sim.Recipe.Durability * MaxDurabilityCost);
            State startState = sim.Simulate(new List<Action>(), new(sim, null));

            Action[] progressActions = new Action[Atlas.Actions.ProgressActions.Length + Atlas.Actions.ProgressBuffs.Length];
            Atlas.Actions.ProgressActions.CopyTo(progressActions, 0);
            Atlas.Actions.ProgressBuffs.CopyTo(progressActions, Atlas.Actions.ProgressActions.Length);
            Action[] qualityActions = new Action[Atlas.Actions.QualityActions.Length + Atlas.Actions.QualityBuffs.Length - 1];
            Atlas.Actions.QualityActions.CopyTo(qualityActions, 0);
            Atlas.Actions.QualityBuffs.CopyTo(qualityActions, Atlas.Actions.QualityActions.Length - 1);
            Action[] durabilityActions = Atlas.Actions.DurabilityActions.ToArray();

            _logger($"\n[{DateTime.Now}] Generating Progress Combinations");
            List<KeyValuePair<double, bool[]>> progressLists = GenerateDFSActionTree(sim, startState, progressActions, ProgressFailure, ProgressSuccess, ProgressScore, ProgressSetMaxLength, cp, ignoreProgress: false);
            int cpCost = (int)ListToCpCost(BinaryToActions(progressLists.First().Value));
            cp = sim.Crafter.CP + (int)(sim.Recipe.Durability * MaxDurabilityCost) - cpCost;
            _logger($"[{DateTime.Now}] Good Lists Found: {progressLists.Count}\n\t{string.Join(",", BinaryToActions(progressLists.First().Value).Select(x => x.ShortName))}");
            _logger($"[{DateTime.Now}] CP Cost: {cpCost}; CP Remaining: {cp}");

            _logger($"\n[{DateTime.Now}] Generating Quality Combinations");
            List<KeyValuePair<double, bool[]>> qualityLists = GenerateDFSActionTree(sim, startState, qualityActions, QualityFailure, QualitySuccess, QualityScore, QualitySetMaxLength, cp, ignoreProgress: true);
            cpCost = (int)ListToCpCost(BinaryToActions(qualityLists.First().Value));
            cp -= cpCost;
            _logger($"[{DateTime.Now}] Good Lists Found: {qualityLists.Count}\n\t{string.Join(",", BinaryToActions(qualityLists.First().Value).Select(x => x.ShortName))}");
            _logger($"[{DateTime.Now}] CP Cost: {cpCost}; CP Remaining: {cp}");

            return null;

            // zip progress and quality together
            bool skipOut = false;
            int progressIx = 0;
            Tuple<double, List<Action>> bestSolution = new(0, new List<Action>());
            for (int qualityIx = 0; qualityIx < qualityLists.Count && !skipOut; qualityIx++)
            {
                var qualityList = BinaryToActions(qualityLists[qualityIx].Value);
                var progressList = BinaryToActions(progressLists[progressIx].Value);

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

                    State s = sim.Simulate(actions, startState, useDurability: false);
                    if (s == null || s.Progress < sim.Recipe.Difficulty) continue;
                    if (s.Step == actions.Count && s.Progress < sim.Recipe.Difficulty) iterateProgress = true;

                    var score = ScoreState(sim, s);
                    if (score <= 0) continue;
                    
                    List<Action>? solution = InsertDurability(sim, startState, new() { actions }, durabilityActions, s.Cp);
                    if (solution == null) continue;

                    s = sim.Simulate(solution, startState);
                    score = ScoreState(sim, s);
                    if (s.Progress < sim.Recipe.Difficulty) continue;
                    
                    if (score > bestSolution.Item1)
                    {
                        bestSolution = new(score, solution);
                        _logger($"[{DateTime.Now}] Best Score: {bestSolution.Item1}\n\t{string.Join(",", bestSolution.Item2.Select(x => x.ShortName))}");
                        sim.Simulate(solution, startState);
                    }

                    for (int i = qualityLists.Count - 1; i >= 0; i--)
                    {
                        if (qualityLists[i].Key <= score) qualityLists.RemoveAt(i);
                    }
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
                sim.Simulate(bestSolution.Item2, startState);
                return bestSolution.Item2;
            }

            return null;
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

        public delegate bool FailureCondition(State state);
        public static bool ProgressFailure(State state)
        {
            return false;
        }
        public static bool QualityFailure(State state)
        {
            return false;
        }

        public delegate double NodeScore(List<Action?> path, double score);
        public double ProgressScore(List<Action?> path, double score)
        {
            return ListToCpCost(path);
        }
        public double QualityScore(List<Action> path, double score)
        {
            return -1 * score;
        }

        private List<KeyValuePair<double, bool[]>> GenerateDFSActionTree(Simulator sim, State startState, Action?[] actions, FailureCondition failureCondition, SuccessCondition successCondition, NodeScore nodeScore, int maxLength, double cpLimit, bool ignoreProgress)
        {
            long nodesGenerated = 0, solutionCount = 0;
            ActionNode head = new ActionNode(null, startState, null);
            List<KeyValuePair<double, bool[]>> lists = new();
            SubDFSActionTree(sim, head, actions, failureCondition, successCondition, nodeScore, maxLength, maxLength, cpLimit, ignoreProgress, ref lists, ref nodesGenerated, ref solutionCount);
            _logger($"[{DateTime.Now}] Nodes Generated: {nodesGenerated}");
            _logger($"[{DateTime.Now}] Lists Found: {lists.Count}");

            lists = lists.OrderBy(x => x.Key).ToList();
            _logger($"[{DateTime.Now}] Sorted");

            var unused = sim.Simulate(BinaryToActions(lists.First().Value), startState, useDurability: false);
            return lists;
        }
        private void SubDFSActionTree(Simulator sim, ActionNode node, Action[] actions, FailureCondition failureCondition, SuccessCondition successCondition, NodeScore nodeScore, int maxLength, int remainingDepth, double cpLimit, bool ignoreProgress, ref List<KeyValuePair<double, bool[]>> lists, ref long nodesGenerated, ref long solutionCount)
        {
            if (remainingDepth <= 0) return;
            foreach (Action action in actions)
            {
                State state = sim.Simulate(action, node.State, useDurability: false);
                if (state.WastedActions > 0 || failureCondition(state)) continue;

                double score = ScoreState(sim, state, ignoreProgress: ignoreProgress);
                if (score <= 0) continue;

                List<Action> path = node.GetPath();
                path.Add(action);
                if (ListToCpCost(path) > cpLimit) continue;

                nodesGenerated++;
                ActionNode newNode = node.Add(action, state);
                SubDFSActionTree(sim, newNode, actions, failureCondition, successCondition, nodeScore, maxLength, remainingDepth - 1, cpLimit, ignoreProgress, ref lists, ref nodesGenerated, ref solutionCount);

                if (successCondition(state))
                {
                    solutionCount++;
                    lists.Add(new(nodeScore(path, score), ActionsToBinary(path)));
                }
                if (newNode.Parent != null && newNode.Parent.Children != null) newNode.Parent.Children.Remove(newNode);

                if (remainingDepth == maxLength)
                {
                    _logger($"{action.ShortName} {{{maxLength}}}: {nodesGenerated} generated, {solutionCount} solutions");
                }
            }
        }

        private static double ListToCpCost(bool[] list, bool considerDurability = true)
        {
            return ListToCpCost(BinaryToActions(list));
        }
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

        private static List<List<Action?>> Iterate(List<Action?> prevSet, Dictionary<Action?, int> actionChoices, bool qualityOnly)
        {
            List<List<Action?>> newSets = new List<List<Action?>>();
            Dictionary<Action, int> counts = new Dictionary<Action, int>();
            foreach (var group in prevSet.GroupBy(x => x.ID))
            {
                counts.Add(group.First(), group.Count());
            }

            var remainingActions = actionChoices.Where(x => !counts.ContainsKey(x.Key) || x.Value > counts[x.Key]).Select(x => x.Key);
            if (!qualityOnly && !remainingActions.Any(x => x.ProgressIncreaseMultiplier > 0)) return newSets;

            foreach (Action? action in remainingActions)
            {
                if (action == prevSet[^1] && Atlas.Actions.Buffs.Contains(action)) continue;

                List<Action?> newSet = prevSet.ToList();
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

        private static List<Action>? InsertDurability(Simulator sim, State startState, List<List<Action?>> actions, Action?[] durabilityActions, double cpMax)
        {
            List<List<Action>> durabilitySolutions = new();
            Dictionary<Action, int> actionChoices = new();

            foreach (var action in durabilityActions) actionChoices.Add(action, 999);
            IEnumerable<List<Action?>> results = durabilityActions.Select(action => new List<Action> { action });

            do
            {
                results = results.SelectMany(result => Iterate(result, actionChoices, true)).Where(result => result.Sum(action => action.CPCost) <= cpMax);
                durabilitySolutions.AddRange(results);
            } while (results.Any());

            var solution = ZipListSets(sim, startState, actions, durabilitySolutions, true);
            return solution.Any() ? solution[^1].Value : null;
        }
        public static double MaxDurabilityGain(List<Action?> durabiltyActions, List<Action> actions)
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
                    if (leftList.Sum(x => x.DurabilityCost) - leftList[^1].DurabilityCost > sim.Recipe.Durability + MaxDurabilityGain(rightList, leftList.OrderBy(x => x.DurabilityCost).ToList())) continue;

                    var merger = new int[leftList.Count + rightList.Count];
                    for (var k = 0; k < merger.Length; k++) merger[k] = 0;

                    while (Iterate(merger, merger.Length - 1))
                    {
                        if (merger[^1] != 0) continue;
            
                        int p = 0, q = 0;
                        double score;
                        State s = startState;
                        for (var i = 0; i < merger.Length; i++)
                        {
                            if (merger[i] == 0)
                            {
                                if (p >= leftList.Count) break;
                                s = sim.Simulate(leftList[p++], s, useDurability);
                            }
                            else
                            {
                                if (q >= rightList.Count) break;
                                s = sim.Simulate(rightList[q++], s, useDurability);
                            }

                            score = ScoreState(sim, s, false);
                            if (s.WastedActions > 0 || score <= 0 || zippedLists.ContainsKey(score)) break;
                            
                            if (i == merger.Length - 1 && s.Progress >= sim.Recipe.Difficulty)
                            {
                                p = 0; q = 0;
                                var actions = new List<Action>(merger.Length);
                                foreach (var t in merger)
                                {
                                    actions.Add(t == 0 ? leftList[p++] : rightList[q++]);
                                }
                                zippedLists.Add(score, actions);
                            }
                        }
                    }
                }
            }
            return zippedLists.ToList();
        }

        public static List<Action>? ZipLists(List<Action> left, List<Action> right, int[] merger)
        {
            int lCount = 0, rCount = 0;
            for (int i = 0; i < merger.Length; i++)
            {
                if (merger[i] == 0)
                {
                    lCount++;
                    if (lCount > left.Count) return null;
                }
                else
                {
                    rCount++;
                    if (rCount > right.Count) return null;
                }
            }
            
            int l = 0, r = 0;
            List<Action> actions = new List<Action>(merger.Length);
            for (int i = 0; i < merger.Length; i++)
            {
                actions.Insert(i, merger[i] == 0 ? left[l++] : right[r++]);
            }

            return actions;
        }

        public static List<Action> BinaryToActions(bool[] bits)
        {
            List<Action> actions = new(bits.Length / ActionBinarySize);
            for (int bigIndex = 0; bigIndex < bits.Length; bigIndex += ActionBinarySize)
            {
                if (bits[bigIndex]) // 1
                {
                    if (bits[bigIndex + 1]) // 11
                    {
                        if (bits[bigIndex + 2]) // 111
                        {
                            if (bits[bigIndex + 3]) // 1111
                            {
                                if (bits[bigIndex + 4])
                                {
                                    // 31
                                }
                                else
                                {
                                    // 30
                                }
                            }
                            else // 1110
                            {
                                if (bits[bigIndex + 4])
                                {
                                    // 29
                                }
                                else
                                {
                                    actions.Add(Atlas.Actions.AdvancedTouch);
                                }
                            }
                        }
                        else // 110
                        {
                            if (bits[bigIndex + 3]) // 1101
                            {
                                if (bits[bigIndex + 4])
                                {
                                    actions.Add(Atlas.Actions.PrudentSynthesis);
                                }
                                else
                                {
                                    actions.Add(Atlas.Actions.TrainedFinesse);
                                }
                            }
                            else // 1100
                            {
                                if (bits[bigIndex + 4])
                                {
                                    actions.Add(Atlas.Actions.TrainedEye);
                                }
                                else
                                {
                                    actions.Add(Atlas.Actions.DelicateSynthesis);
                                }
                            }
                        }
                    }
                    else // 10
                    {
                        if (bits[bigIndex + 2]) // 101
                        {
                            if (bits[bigIndex + 3]) // 1011
                            {
                                if (bits[bigIndex + 4])
                                {
                                    actions.Add(Atlas.Actions.Groundwork);
                                }
                                else
                                {
                                    actions.Add(Atlas.Actions.PreparatoryTouch);
                                }
                            }
                            else // 1010
                            {
                                if (bits[bigIndex + 4])
                                {
                                    actions.Add(Atlas.Actions.Reflect);
                                }
                                else
                                {
                                    actions.Add(Atlas.Actions.FocusedTouch);
                                }
                            }
                        }
                        else // 100
                        {
                            if (bits[bigIndex + 3]) // 1001
                            {
                                if (bits[bigIndex + 4])
                                {
                                    actions.Add(Atlas.Actions.FocusedSynthesis);
                                }
                                else
                                {
                                    actions.Add(Atlas.Actions.PrudentTouch);
                                }
                            }
                            else // 1000
                            {
                                if (bits[bigIndex + 4])
                                {
                                    actions.Add(Atlas.Actions.MuscleMemory);
                                }
                                else
                                {
                                    actions.Add(Atlas.Actions.PreciseTouch);
                                }
                            }
                        }
                    }
                }
                else // 0
                {
                    if (bits[bigIndex + 1]) // 01
                    {
                        if (bits[bigIndex + 2]) // 011
                        {
                            if (bits[bigIndex + 3]) // 0111
                            {
                                if (bits[bigIndex + 4])
                                {
                                    actions.Add(Atlas.Actions.GreatStrides);
                                }
                                else
                                {
                                    actions.Add(Atlas.Actions.Innovation);
                                }
                            }
                            else // 0110
                            {
                                if (bits[bigIndex + 4])
                                {
                                    actions.Add(Atlas.Actions.Veneration);
                                }
                                else
                                {
                                    actions.Add(Atlas.Actions.WasteNot2);
                                }
                            }
                        }
                        else // 010
                        {
                            if (bits[bigIndex + 3]) // 0101
                            {
                                if (bits[bigIndex + 4])
                                {
                                    actions.Add(Atlas.Actions.WasteNot);
                                }
                                else
                                {
                                    actions.Add(Atlas.Actions.Manipulation);
                                }
                            }
                            else // 0100
                            {
                                if (bits[bigIndex + 4])
                                {
                                    actions.Add(Atlas.Actions.InnerQuiet);
                                }
                                else
                                {
                                    actions.Add(Atlas.Actions.TricksOfTheTrade);
                                }
                            }
                        }
                    }
                    else // 00
                    {
                        if (bits[bigIndex + 2]) // 001
                        {
                            if (bits[bigIndex + 3]) // 0011
                            {
                                if (bits[bigIndex + 4])
                                {
                                    actions.Add(Atlas.Actions.MastersMend);
                                }
                                else
                                {
                                    actions.Add(Atlas.Actions.ByregotsBlessing);
                                }
                            }
                            else // 0010
                            {
                                if (bits[bigIndex + 4])
                                {
                                    actions.Add(Atlas.Actions.StandardTouch);
                                }
                                else
                                {
                                    actions.Add(Atlas.Actions.BasicTouch);
                                }
                            }
                        }
                        else // 000
                        {
                            if (bits[bigIndex + 3]) // 0001
                            {
                                if (bits[bigIndex + 4])
                                {
                                    actions.Add(Atlas.Actions.CarefulSynthesis);
                                }
                                else
                                {
                                    actions.Add(Atlas.Actions.BasicSynth);
                                }
                            }
                            else // 0000
                            {
                                if (bits[bigIndex + 4])
                                {
                                    actions.Add(Atlas.Actions.Observe);
                                }
                                else
                                {
                                    actions.Add(Atlas.Actions.DummyAction);
                                }
                            }
                        }
                    }
                }
            }
            return actions;
        }
        public static bool[] ActionsToBinary(List<Action> actions)
        {
            bool[] bitArray = new bool[actions.Count * ActionBinarySize];
            int bigIndex = 0;
            for (int i = 0; i < actions.Count; i++)
            {
                switch (actions[i].ID)
                {
                    case 0:
                        bitArray[bigIndex] = false;
                        bitArray[bigIndex + 1] = false;
                        bitArray[bigIndex + 2] = false;
                        bitArray[bigIndex + 3] = false;
                        bitArray[bigIndex + 4] = false;
                        break;
                    case 1:
                        bitArray[bigIndex] = false;
                        bitArray[bigIndex + 1] = false;
                        bitArray[bigIndex + 2] = false;
                        bitArray[bigIndex + 3] = false;
                        bitArray[bigIndex + 4] = true;
                        break;
                    case 2:
                        bitArray[bigIndex] = false;
                        bitArray[bigIndex + 1] = false;
                        bitArray[bigIndex + 2] = false;
                        bitArray[bigIndex + 3] = true;
                        bitArray[bigIndex + 4] = false;
                        break;
                    case 3:
                        bitArray[bigIndex] = false;
                        bitArray[bigIndex + 1] = false;
                        bitArray[bigIndex + 2] = false;
                        bitArray[bigIndex + 3] = true;
                        bitArray[bigIndex + 4] = true;
                        break;
                    case 4:
                        bitArray[bigIndex] = false;
                        bitArray[bigIndex + 1] = false;
                        bitArray[bigIndex + 2] = true;
                        bitArray[bigIndex + 3] = false;
                        bitArray[bigIndex + 4] = false;
                        break;
                    case 5:
                        bitArray[bigIndex] = false;
                        bitArray[bigIndex + 1] = false;
                        bitArray[bigIndex + 2] = true;
                        bitArray[bigIndex + 3] = false;
                        bitArray[bigIndex + 4] = true;
                        break;
                    case 6:
                        bitArray[bigIndex] = false;
                        bitArray[bigIndex + 1] = false;
                        bitArray[bigIndex + 2] = true;
                        bitArray[bigIndex + 3] = true;
                        bitArray[bigIndex + 4] = false;
                        break;
                    case 7:
                        bitArray[bigIndex] = false;
                        bitArray[bigIndex + 1] = false;
                        bitArray[bigIndex + 2] = true;
                        bitArray[bigIndex + 3] = true;
                        bitArray[bigIndex + 4] = true;
                        break;
                    case 8:
                        bitArray[bigIndex] = false;
                        bitArray[bigIndex + 1] = true;
                        bitArray[bigIndex + 2] = false;
                        bitArray[bigIndex + 3] = false;
                        bitArray[bigIndex + 4] = false;
                        break;
                    case 9:
                        bitArray[bigIndex] = false;
                        bitArray[bigIndex + 1] = true;
                        bitArray[bigIndex + 2] = false;
                        bitArray[bigIndex + 3] = false;
                        bitArray[bigIndex + 4] = true;
                        break;
                    case 10:
                        bitArray[bigIndex] = false;
                        bitArray[bigIndex + 1] = true;
                        bitArray[bigIndex + 2] = false;
                        bitArray[bigIndex + 3] = true;
                        bitArray[bigIndex + 4] = false;
                        break;
                    case 11:
                        bitArray[bigIndex] = false;
                        bitArray[bigIndex + 1] = true;
                        bitArray[bigIndex + 2] = false;
                        bitArray[bigIndex + 3] = true;
                        bitArray[bigIndex + 4] = true;
                        break;
                    case 12:
                        bitArray[bigIndex] = false;
                        bitArray[bigIndex + 1] = true;
                        bitArray[bigIndex + 2] = true;
                        bitArray[bigIndex + 3] = false;
                        bitArray[bigIndex + 4] = false;
                        break;
                    case 13:
                        bitArray[bigIndex] = false;
                        bitArray[bigIndex + 1] = true;
                        bitArray[bigIndex + 2] = true;
                        bitArray[bigIndex + 3] = false;
                        bitArray[bigIndex + 4] = true;
                        break;
                    case 14:
                        bitArray[bigIndex] = false;
                        bitArray[bigIndex + 1] = true;
                        bitArray[bigIndex + 2] = true;
                        bitArray[bigIndex + 3] = true;
                        bitArray[bigIndex + 4] = false;
                        break;
                    case 15:
                        bitArray[bigIndex] = false;
                        bitArray[bigIndex + 1] = true;
                        bitArray[bigIndex + 2] = true;
                        bitArray[bigIndex + 3] = true;
                        bitArray[bigIndex + 4] = true;
                        break;
                    case 16:
                        bitArray[bigIndex] = true;
                        bitArray[bigIndex + 1] = false;
                        bitArray[bigIndex + 2] = false;
                        bitArray[bigIndex + 3] = false;
                        bitArray[bigIndex + 4] = false;
                        break;
                    case 17:
                        bitArray[bigIndex] = true;
                        bitArray[bigIndex + 1] = false;
                        bitArray[bigIndex + 2] = false;
                        bitArray[bigIndex + 3] = false;
                        bitArray[bigIndex + 4] = true;
                        break;
                    case 18:
                        bitArray[bigIndex] = true;
                        bitArray[bigIndex + 1] = false;
                        bitArray[bigIndex + 2] = false;
                        bitArray[bigIndex + 3] = true;
                        bitArray[bigIndex + 4] = false;
                        break;
                    case 19:
                        bitArray[bigIndex] = true;
                        bitArray[bigIndex + 1] = false;
                        bitArray[bigIndex + 2] = false;
                        bitArray[bigIndex + 3] = true;
                        bitArray[bigIndex + 4] = true;
                        break;
                    case 20:
                        bitArray[bigIndex] = true;
                        bitArray[bigIndex + 1] = false;
                        bitArray[bigIndex + 2] = true;
                        bitArray[bigIndex + 3] = false;
                        bitArray[bigIndex + 4] = false;
                        break;
                    case 21:
                        bitArray[bigIndex] = true;
                        bitArray[bigIndex + 1] = false;
                        bitArray[bigIndex + 2] = true;
                        bitArray[bigIndex + 3] = false;
                        bitArray[bigIndex + 4] = true;
                        break;
                    case 22:
                        bitArray[bigIndex] = true;
                        bitArray[bigIndex + 1] = false;
                        bitArray[bigIndex + 2] = true;
                        bitArray[bigIndex + 3] = true;
                        bitArray[bigIndex + 4] = false;
                        break;
                    case 23:
                        bitArray[bigIndex] = true;
                        bitArray[bigIndex + 1] = false;
                        bitArray[bigIndex + 2] = true;
                        bitArray[bigIndex + 3] = true;
                        bitArray[bigIndex + 4] = true;
                        break;
                    case 24:
                        bitArray[bigIndex] = true;
                        bitArray[bigIndex + 1] = true;
                        bitArray[bigIndex + 2] = false;
                        bitArray[bigIndex + 3] = false;
                        bitArray[bigIndex + 4] = false;
                        break;
                    case 25:
                        bitArray[bigIndex] = true;
                        bitArray[bigIndex + 1] = true;
                        bitArray[bigIndex + 2] = false;
                        bitArray[bigIndex + 3] = false;
                        bitArray[bigIndex + 4] = true;
                        break;
                    case 26:
                        bitArray[bigIndex] = true;
                        bitArray[bigIndex + 1] = true;
                        bitArray[bigIndex + 2] = false;
                        bitArray[bigIndex + 3] = true;
                        bitArray[bigIndex + 4] = false;
                        break;
                    case 27:
                        bitArray[bigIndex] = true;
                        bitArray[bigIndex + 1] = true;
                        bitArray[bigIndex + 2] = false;
                        bitArray[bigIndex + 3] = true;
                        bitArray[bigIndex + 4] = true;
                        break;
                    case 28:
                        bitArray[bigIndex] = true;
                        bitArray[bigIndex + 1] = true;
                        bitArray[bigIndex + 2] = true;
                        bitArray[bigIndex + 3] = false;
                        bitArray[bigIndex + 4] = false;
                        break;
                    case 29:
                        bitArray[bigIndex] = true;
                        bitArray[bigIndex + 1] = true;
                        bitArray[bigIndex + 2] = true;
                        bitArray[bigIndex + 3] = false;
                        bitArray[bigIndex + 4] = true;
                        break;
                    case 30:
                        bitArray[bigIndex] = true;
                        bitArray[bigIndex + 1] = true;
                        bitArray[bigIndex + 2] = true;
                        bitArray[bigIndex + 3] = true;
                        bitArray[bigIndex + 4] = false;
                        break;
                    case 31:
                        bitArray[bigIndex] = true;
                        bitArray[bigIndex + 1] = true;
                        bitArray[bigIndex + 2] = true;
                        bitArray[bigIndex + 3] = true;
                        bitArray[bigIndex + 4] = true;
                        break;

                }
                bigIndex += ActionBinarySize;
            }
            return bitArray;
        }
    }
}

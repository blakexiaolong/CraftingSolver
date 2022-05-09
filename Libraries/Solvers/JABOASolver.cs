using System.Diagnostics;

using CraftingSolver;
using Action = CraftingSolver.Action;
using static CraftingSolver.Solver;

namespace Libraries.Solvers
{
    // Just a Bunch of Actions
    public class JABOASolver
    {
        private static double maxDurabilityCost = 88 / 30D;
        private static double minDurabilityCost = 96 / 40D;
        private static double wasteNotPerActionCost = 98 / 8D;

        private const int KEEP_ACTION_LISTS_AMOUNT = 5000;
        private const int PROGRESS_SET_MAX_LENGTH = 7;
        private const int QUALITY_SET_MAX_LENGTH = 10;

        private static long listsGenerated = 0, listsEvaluated = 0, setsEvaluated = 0, progressPercent = 0;

        public List<Action> Run(Simulator sim, int maxTasks)
        {
            int cp = sim.Crafter.CP + (int)(sim.Recipe.Durability * maxDurabilityCost);
            State startState = sim.Simulate(null, new State());

            Action[] progressActions = new Action[Atlas.Actions.ProgressActions.Length + Atlas.Actions.ProgressBuffs.Length];
            Atlas.Actions.ProgressActions.CopyTo(progressActions, 0);
            Atlas.Actions.ProgressBuffs.CopyTo(progressActions, Atlas.Actions.ProgressActions.Length);
            Action[] qualityActions = new Action[Atlas.Actions.QualityActions.Length + Atlas.Actions.QualityBuffs.Length - 1];
            Atlas.Actions.QualityActions.Where(x => !x.Equals(Atlas.Actions.Reflect)).ToList().CopyTo(qualityActions, 0);
            Atlas.Actions.QualityBuffs.CopyTo(qualityActions, Atlas.Actions.QualityActions.Length - 1);
            Action[] durabilityActions = Atlas.Actions.DurabilityActions.ToArray();

            // solve just progress actions, minimize CP
            Debug.WriteLine($"\n[{DateTime.Now}] Generating Progress Combinations");
            List<Dictionary<Action, int>> progressSets = GetCombinations(progressActions, PROGRESS_SET_MAX_LENGTH, cp, progressChecks, sim.BaseProgressIncrease, sim.Recipe.Difficulty);
            Debug.WriteLine($"[{DateTime.Now}] Combinations Generated: {progressSets.Count()}");
            RemoveBadProgress(progressSets, sim.BaseProgressIncrease, sim.Recipe.Difficulty);
            Debug.WriteLine($"[{DateTime.Now}] Removed Bad Progress: Combinations Remaining: {progressSets.Count()}");
            var scoredProgressLists = progressSets.SelectMany(x => CombineActionSets(sim, startState, progressSets.Count, x, progressListChecks)).ToList();
            int minLength = scoredProgressLists.Min(x => x.Value.Count());
            List<List<Action>> progressLists = scoredProgressLists.Where(y => y.Value.Count == minLength).OrderBy(x => ListToCpCost(x.Value)).Select(x => x.Value).ToList();
            Debug.WriteLine($"\n[{DateTime.Now}] Lists Generated: {listsGenerated}");
            Debug.WriteLine($"\n[{DateTime.Now}] Good Lists Found: {progressLists.Count()}\n\t{string.Join(",", progressLists.First().Select(x => x.ShortName))}");
            cp = sim.Crafter.CP + (int)(sim.Recipe.Durability * maxDurabilityCost) - (int)ListToCpCost(progressLists[0]);

            // solve just quality actions, minimize CP
            Debug.WriteLine($"\n[{DateTime.Now}] Generating Quality Combinations");
            List<Dictionary<Action, int>> qualitySets = GetCombinations(qualityActions, QUALITY_SET_MAX_LENGTH, cp, qualityChecks, sim.BaseQualityIncrease, sim.Recipe.MaxQuality);
            Debug.WriteLine($"[{DateTime.Now}] Combinations Generated: {qualitySets.Count()}");
            RemoveBadQuality(qualitySets);
            qualitySets = OrderQuality(sim, qualitySets).ToList();
            Debug.WriteLine($"[{DateTime.Now}] Removed Bad Quality; Combinations Remaining: {qualitySets.Count()}");
            qualitySets = qualitySets.Take(KEEP_ACTION_LISTS_AMOUNT).ToList();
            Debug.WriteLine($"[{DateTime.Now}] Limiting to {KEEP_ACTION_LISTS_AMOUNT}");

            // do we want to compose qualitySets into a tree so we can eliminate some duplicates?
            listsGenerated = 0; listsEvaluated = 0; setsEvaluated = 0; progressPercent = 0;
            var scoredQualityLists = qualitySets.SelectMany(x => CombineActionSets(sim, startState, qualitySets.Count, x, qualityListChecks, qualityOnly: true)).ToList().OrderBy(x => -1 * x.Key).ToList();
            Debug.WriteLine($"\n[{DateTime.Now}] Lists Generated: {listsGenerated};\t Solution Sets Evaluated: {listsEvaluated}");
            Debug.WriteLine($"\n[{DateTime.Now}] Good Lists Found: {scoredQualityLists.Count()}  ||  Best Score: {scoredQualityLists.First().Key}\n\t{string.Join(",", scoredQualityLists.First().Value.Select(x => x.ShortName))}");

            // zip progress and quality together
            bool skipOut = false;
            Tuple<double, List<Action>> bestSolution = new Tuple<double, List<Action>>(0, new List<Action>());
            for (int qualityIx = 0; qualityIx < scoredQualityLists.Count && !skipOut; qualityIx++)
            {
                var qualityList = scoredQualityLists[qualityIx].Value;

                int progressIx = 0;
                var progressList = progressLists[progressIx];

                int[] merger = new int[qualityList.Count + progressList.Count];
                for (int k = 0; k < merger.Length; k++) merger[k] = 0;

                bool success = false;
                while (Iterate(merger, merger.Length - 1))
                {
                    var actions = ZipLists(progressList, qualityList, merger);
                    if (actions == null) continue;

                    State s = LightSimulator.Simulate(sim, actions, startState, useDurability: false);
                    var score = ScoreState(sim, s);
                    if (s == null || s.Progress < sim.Recipe.Difficulty) continue;

                    List<Action>? solution = InsertDurability(sim, startState, new() { actions }, durabilityActions, s.CP);
                    if (solution == null) continue;

                    s = LightSimulator.Simulate(sim, solution, startState);
                    score = ScoreState(sim, s);
                    if (s != null && s.Progress >= sim.Recipe.Difficulty)
                    {
                        success = true;
                        if (score.Item1 > bestSolution.Item1)
                        {
                            bestSolution = new Tuple<double, List<Action>>(score.Item1, solution);
                            Debug.WriteLine($"[{DateTime.Now}] Best Score: {bestSolution.Item1}\n\t{string.Join(",", bestSolution.Item2.Select(x => x.ShortName))}");
                            LightSimulator.Simulate(sim, solution, startState);
                        }
                        scoredQualityLists.RemoveAll(x => x.Key <= score.Item1);
                    }

                }
                if (!success) progressIx++;
            }

            LightSimulator.Simulate(sim, bestSolution.Item2, startState);
            return bestSolution.Item2;
        }

        #region Audits
        public delegate bool CombinationCheck(Dictionary<Action, int> set, double baseGain, int max);
        public static CombinationCheck[] progressChecks = new CombinationCheck[]
        {
                CheckMuscleMemory
        };
        public static bool CheckMuscleMemory(Dictionary<Action, int> set, double baseProgressGain, int progressMax)
        {
            return set[Atlas.Actions.MuscleMemory] <= 1;
        }
        public static CombinationCheck[] qualityChecks = new CombinationCheck[]
        {
                CheckByregots,
                CheckFirstRound
        };
        public static bool CheckByregots(Dictionary<Action, int> set, double baseQualityGain, int qualityMax)
        {
            return set[Atlas.Actions.ByregotsBlessing] <= 1;
        }
        public static bool CheckFirstRound(Dictionary<Action, int> set, double baseQualityGain, int qualityMax)
        {
            if (!set.TryGetValue(Atlas.Actions.Reflect, out int reflect)) reflect = 0;
            if (reflect + set[Atlas.Actions.TrainedEye] > 1)
            {
                return false;
            }
            return !(set[Atlas.Actions.TrainedEye] > 0 && set.Where(y => y.Value > 0 && !y.Key.Equals(Atlas.Actions.TrainedEye)).Any());
        }

        public delegate bool SolutionCheck(Simulator sim, List<Action> set);
        public static SolutionCheck[] solutionChecks = new SolutionCheck[]
        {
                CheckWasteNot,
                CheckDurability
        };
        public static bool CheckWasteNot(Simulator sim, List<Action> set)
        {
            int index = 0;
            while ((index = set.FindIndex(index, x => x.Equals(Atlas.Actions.WasteNot))) >= 0)
            {
                index++;
                var wasteNotActions = set.Skip(index).Take(Atlas.Actions.WasteNot.ActiveTurns);
                if (wasteNotActions.Contains(Atlas.Actions.WasteNot) || wasteNotActions.Contains(Atlas.Actions.WasteNot2))
                    return false;

                if (wasteNotActions.Count() == Atlas.Actions.WasteNot.ActiveTurns && wasteNotActions.Count(x => x.DurabilityCost > 0) > Atlas.Actions.WasteNot.ActiveTurns / 2)
                {
                    return false;
                }
            }

            index = 0;
            while ((index = set.FindIndex(index, x => x.Equals(Atlas.Actions.WasteNot2))) >= 0)
            {
                index++;
                var wasteNotActions = set.Skip(index).Take(Atlas.Actions.WasteNot.ActiveTurns);
                if (wasteNotActions.Contains(Atlas.Actions.WasteNot) || wasteNotActions.Contains(Atlas.Actions.WasteNot2))
                    return false;

                if (wasteNotActions.Count() == Atlas.Actions.WasteNot2.ActiveTurns && wasteNotActions.Count(x => x.DurabilityCost > 0) > Atlas.Actions.WasteNot2.ActiveTurns / 2)
                {
                    return false;
                }
            }
            return true;
        }
        public static bool CheckDurability(Simulator sim, List<Action> set)
        {
            int dur = sim.Recipe.Durability;
            int manipTurns = 0;
            int wasteNotTurns = 0;
            foreach (Action action in set)
            {
                if (dur <= 0)
                {
                    auditDict["AuditDurability"]++;
                    return false;
                }

                if (action.Equals(Atlas.Actions.Groundwork) && dur < action.DurabilityCost) dur -= 10;
                else if (action.DurabilityCost > 0) dur -= wasteNotTurns > 0 ? action.DurabilityCost / 2 : action.DurabilityCost;
                else if (action.Equals(Atlas.Actions.MastersMend))
                {
                    if (dur == sim.Recipe.Durability)
                        return false;
                    dur = Math.Min(sim.Recipe.Durability, dur + 30);
                }

                if (action.Equals(Atlas.Actions.Manipulation)) manipTurns = 8;
                else if (action.Equals(Atlas.Actions.WasteNot)) wasteNotTurns = 4;
                else if (action.Equals(Atlas.Actions.WasteNot2)) wasteNotTurns = 8;
                else if (manipTurns > 0 && dur > 0)
                {
                    dur = Math.Min(sim.Recipe.Durability, dur + 5);
                }
                manipTurns = Math.Max(manipTurns - 1, 0);
                wasteNotTurns = Math.Max(wasteNotTurns - 1, 0);
            }
            return true;
        }
        public static SolutionCheck[] progressListChecks = new SolutionCheck[]
        {

        };
        public static SolutionCheck[] qualityListChecks = new SolutionCheck[]
        {
                CheckByregotsLast
        };
        public static bool CheckByregotsLast(Simulator sim, List<Action> set)
        {
            int bbIx = set.LastIndexOf(Atlas.Actions.ByregotsBlessing);
            return bbIx > 0 ? bbIx == set.Count - 1 : true;
        }
        #endregion

        public List<Dictionary<Action, int>> GetCombinations(Action[] actionSet, int maxLength, int cpMax, CombinationCheck[] combinationChecks, double baseGain, int max)
        {
            List<Dictionary<Action, int>> combinations = new List<Dictionary<Action, int>>();
            List<Dictionary<Action, int>> prevSets = new List<Dictionary<Action, int>>();
            for (int i = 0; i < maxLength; i++)
            {
                List<Dictionary<Action, int>> newCombinations = new List<Dictionary<Action, int>>();
                if (prevSets.Count == 0)
                {
                    foreach (Action action in actionSet)
                    {
                        Dictionary<Action, int> dict = new Dictionary<Action, int>();
                        foreach (KeyValuePair<Action, int> kvp in actionSet.Select(x => new KeyValuePair<Action, int>(x, 0)))
                        {
                            dict.Add(kvp.Key, kvp.Value);
                        }
                        dict[action] = 1;
                        newCombinations.Add(dict);
                    }
                }

                foreach (Dictionary<Action, int> c in prevSets)
                {
                    foreach (Action action in actionSet)
                    {
                        Dictionary<Action, int> newCombination = c.ToDictionary(x => x.Key, x => x.Value);
                        newCombination[action]++;
                        if (SetToCpCost(newCombination) <= cpMax)
                        {
                            newCombinations.Add(newCombination);
                        }
                    }
                }
                prevSets = newCombinations.Distinct(new DictionaryComparer()).Where(x => combinationChecks.All(audit => audit(x, baseGain, max))).ToList();
            }
            combinations.AddRange(prevSets);
            return combinations;
        }
        public class DictionaryComparer : IEqualityComparer<Dictionary<Action, int>>
        {
            public string CompareString(Dictionary<Action, int> obj)
            {
                return string.Join("", obj.OrderBy(x => x.Key.ID).Select(x => x.Value));
            }
            public bool Equals(Dictionary<Action, int>? x, Dictionary<Action, int>? y)
            {
                if (GetHashCode(x) != GetHashCode(y))
                {
                    return false;
                }
                else
                {
                    return CompareString(x) == CompareString(y);
                }
            }
            public int GetHashCode(Dictionary<Action, int> obj)
            {
                return CompareString(obj).GetHashCode();
            }
        }

        public static double SetToCpCost(Dictionary<Action, int> set)
        {
            double cpTotal = set.Sum(y => (y.Key.CPCost + y.Key.DurabilityCost * minDurabilityCost) * y.Value);

            set.TryGetValue(Atlas.Actions.FocusedSynthesis, out int fSynthesis);
            set.TryGetValue(Atlas.Actions.FocusedTouch, out int fTouch);
            int observeCost = (fSynthesis + fTouch) * Atlas.Actions.Observe.CPCost;

            set.TryGetValue(Atlas.Actions.AdvancedTouch, out int aTouch);
            set.TryGetValue(Atlas.Actions.StandardTouch, out int sTouch);
            set.TryGetValue(Atlas.Actions.BasicTouch, out int bTouch);
            int standardCombo = Math.Min(bTouch, sTouch);
            int advancedCombo = Math.Min(standardCombo, aTouch);
            int comboSavings = standardCombo * (Atlas.Actions.StandardTouch.CPCost - Atlas.Actions.BasicTouch.CPCost)
                             + advancedCombo * (Atlas.Actions.AdvancedTouch.CPCost - Atlas.Actions.BasicTouch.CPCost);

            IEnumerable<KeyValuePair<Action, int>> wasteNotTargets = set.Where(x => x.Value > 0 && x.Key.DurabilityCost > 5 && !Atlas.Actions.FirstRoundActions.Contains(x.Key));
            double durabilitySavings = wasteNotTargets.Sum(x => x.Key.DurabilityCost / 2 * x.Value * minDurabilityCost);
            double wasteNotSavings = durabilitySavings + wasteNotPerActionCost * wasteNotTargets.Sum(x => x.Value);

            return cpTotal + observeCost - comboSavings - wasteNotSavings;
        }
        public static double ListToCpCost(List<Action> list)
        {
            double cpTotal = list.Sum(x => x.CPCost + x.DurabilityCost * minDurabilityCost);
            int observeCost = (list.Count(x => x.Equals(Atlas.Actions.FocusedSynthesis)) + list.Count(x => x.Equals(Atlas.Actions.FocusedTouch))) * Atlas.Actions.Observe.CPCost;

            int comboSavings = 0;
            int index = 0;
            while ((index = list.FindIndex(index, x => x.Equals(Atlas.Actions.StandardTouch))) >= 0)
            {
                if (list[index - 1].Equals(Atlas.Actions.BasicTouch))
                {
                    comboSavings += Atlas.Actions.StandardTouch.CPCost - Atlas.Actions.BasicTouch.CPCost;
                    if (list[index + 1].Equals(Atlas.Actions.AdvancedTouch))
                    {
                        comboSavings += Atlas.Actions.AdvancedTouch.CPCost - Atlas.Actions.BasicTouch.CPCost;
                    }
                }
                index++;
            }

            // todo implement some sort of waste not savings here

            return cpTotal + observeCost - comboSavings;
        }

        public static void RemoveBadProgress(List<Dictionary<Action, int>> sets, double baseProgressGain, int progressMax)
        {
            sets.RemoveAll(x =>
            {
                double progress = 0;
                progress = x.Sum(y => y.Key.ProgressIncreaseMultiplier * y.Value);
                if (x[Atlas.Actions.Veneration] > 0)
                {
                    int rounds = Math.Min(x[Atlas.Actions.Veneration] * Atlas.Actions.Veneration.ActiveTurns, x.Where(y => !y.Key.Equals(Atlas.Actions.MuscleMemory) && !y.Key.Equals(Atlas.Actions.Veneration)).Sum(y => y.Value));
                    var maxOption = x.Where(y => y.Value > 0 && !y.Key.Equals(Atlas.Actions.MuscleMemory)).Max(y => y.Key.ProgressIncreaseMultiplier);
                    progress += maxOption * 0.5 * rounds;
                }
                if (x[Atlas.Actions.MuscleMemory] > 1 && x.Values.Sum(y => y) > 1)
                {
                    progress += 1;
                }
                return progress * baseProgressGain < progressMax;
            });
        }

        public static void RemoveBadQuality(List<Dictionary<Action, int>> sets)
        {
            sets.RemoveAll(x => x[Atlas.Actions.TrainedFinesse] > 0 && GetInnerQuiet(x) < 10);
        }
        private static int GetInnerQuiet(Dictionary<Action, int> set)
        {
            if (!set.TryGetValue(Atlas.Actions.Reflect, out int reflect)) reflect = 0;
            return set.Where(x => x.Key.QualityIncreaseMultiplier > 0).Sum(x => x.Value) + set[Atlas.Actions.PreparatoryTouch] + 2 * reflect;
        }
        public static IOrderedEnumerable<Dictionary<Action, int>> OrderQuality(Simulator sim, List<Dictionary<Action, int>> sets)
        {
            return sets.OrderBy(x =>
            {
                if (x[Atlas.Actions.TrainedEye] > 0) return sim.Recipe.MaxQuality;

                List<Action> actions = new List<Action>();
                if (x.TryGetValue(Atlas.Actions.Reflect, out int reflect) && reflect > 0)
                {
                    actions.Add(Atlas.Actions.Reflect);
                }
                for (int i = 0; i < x[Atlas.Actions.PreparatoryTouch]; i++)
                {
                    actions.Add(Atlas.Actions.PreparatoryTouch);
                }

                List<Action> remaining = new List<Action>();
                var kvps = x.Where(y => y.Value > 0);
                foreach (var kvp in kvps)
                {
                    for (int i = 0; i < kvp.Value; i++)
                    {
                        remaining.Add(kvp.Key);
                    }
                }
                remaining.RemoveAll(y =>
                    y.Equals(Atlas.Actions.ByregotsBlessing) ||
                    y.Equals(Atlas.Actions.TrainedFinesse) ||
                    y.Equals(Atlas.Actions.PreparatoryTouch) ||
                    y.Equals(Atlas.Actions.Innovation) ||
                    y.Equals(Atlas.Actions.GreatStrides) ||
                    y.Equals(Atlas.Actions.Reflect));
                remaining.OrderBy(y => y.QualityIncreaseMultiplier);
                foreach (var action in remaining) actions.Add(action);

                for (int i = 0; i < x[Atlas.Actions.TrainedFinesse]; i++)
                {
                    actions.Add(Atlas.Actions.TrainedFinesse);
                }
                if (x[Atlas.Actions.ByregotsBlessing] > 0)
                {
                    actions.Add(Atlas.Actions.ByregotsBlessing);
                }

                int left = x[Atlas.Actions.GreatStrides];
                for (int i = actions.Count - 1; left > 0 && i >= 0; i--)
                {
                    actions.Insert(i, Atlas.Actions.GreatStrides);
                    left--;
                }
                left = x[Atlas.Actions.Innovation];
                for (int i = actions.Count - 4; left > 0 && i >= 0; i -= 4)
                {
                    actions.Insert(i, Atlas.Actions.Innovation);
                    left--;
                }

                for (int i = 0; i < actions.Count; i++)
                {
                    if (actions[i] == Atlas.Actions.FocusedTouch)
                    {
                        actions.Insert(i, Atlas.Actions.Observe);
                        i++;
                    }
                }

                if (!actions.Any())
                {
                    return 0;
                }
                else
                {
                    State result = LightSimulator.Simulate(sim, actions, new State(), useDurability: false);
                    if (result != null && result.Quality > sim.Recipe.MaxQuality * 1.1)
                    {
                        return sim.Recipe.MaxQuality * 1.1 + result.CP;
                    }
                    else
                    {
                        return result?.Quality ?? 0;
                    }
                }
            });
        }

        public static List<KeyValuePair<double, List<Action>>> CombineActionSets(Simulator sim, State startState, int setsCount, Dictionary<Action, int> set, SolutionCheck[] checks, bool qualityOnly = false)
        {
            IEnumerable<Action> firstRoundActions = set.Where(x => x.Value > 0 && Atlas.Actions.FirstRoundActions.Contains(x.Key)).Select(x => x.Key);
            if (firstRoundActions.Count() > 1) return null;

            Dictionary<Action, int> actionChoices = new Dictionary<Action, int>();
            foreach (var pair in set.Where(x => x.Value > 0)) actionChoices.Add(pair.Key, pair.Value);

            List<List<Action>> actionSets = new List<List<Action>>();
            GetLookupDictionary(actionChoices, out Dictionary<Action, int> lookup);

            // generate initial list
            Action firstRound = firstRoundActions.FirstOrDefault();
            if (firstRound != null)
            {
                actionSets.Add(new List<Action>() { firstRound });
            }
            else
            {
                foreach (var action in actionChoices.Where(x => x.Value > 0))
                {
                    actionSets.Add(new List<Action>() { action.Key });
                }
            }

            SortedList<double, List<Action>> solutions = new SortedList<double, List<Action>>();
            for (int i = 0; i < sim.MaxLength; i++)
            {
                List<List<Action>> newActionSets = new List<List<Action>>();
                foreach (var actionSet in actionSets)
                {
                    SortedList<double, Tuple<bool, List<Action>>> scores = new SortedList<double, Tuple<bool, List<Action>>>();
                    IEnumerable<List<Action>> responses = Iterate(actionSet, actionChoices, qualityOnly);
                    listsGenerated += responses.Count();
                    responses = responses.Where(x => checks.All(audit => audit(sim, x)));
                    foreach (var response in responses)
                    {
                        State state = LightSimulator.Simulate(sim, response, startState, useDurability: false);
                        Tuple<double, bool> score = ScoreState(sim, state, ignoreProgress: qualityOnly);

                        listsEvaluated++;
                        if (score.Item1 > 0 && !scores.ContainsKey(score.Item1))
                        {
                            scores.Add(score.Item1, new Tuple<bool, List<Action>>(qualityOnly || state.CheckViolations().ProgressOk, response));
                        }
                    }

                    if (qualityOnly)
                    {
                        newActionSets.AddRange(scores.Select(x => x.Value.Item2));
                        foreach (var score in scores)
                        {
                            if (!solutions.ContainsKey(score.Key))
                            {
                                solutions.Add(score.Key, score.Value.Item2);
                            }
                        }
                    }
                    else
                    {
                        newActionSets.AddRange(scores.Where(x => !x.Value.Item1).Select(x => x.Value.Item2));
                        foreach (var score in scores.Where(x => x.Value.Item1))
                        {
                            if (!solutions.ContainsKey(score.Key))
                            {
                                solutions.Add(score.Key, score.Value.Item2);
                            }
                        }
                    }
                }

                actionSets = newActionSets;
                if (qualityOnly && solutions.Count > KEEP_ACTION_LISTS_AMOUNT)
                {
                    for (int j = KEEP_ACTION_LISTS_AMOUNT; j < solutions.Count; j++)
                    {
                        solutions.RemoveAt(j);
                    }
                }
            }

            setsEvaluated++;
            double increment = setsCount / 100F;
            while (setsEvaluated > (progressPercent + 1) * increment)
            {
                progressPercent++;
                if (progressPercent % 50 == 0)
                    Debug.Write("|");
                else if (progressPercent % 10 == 0)
                    Debug.Write("=");
                else
                    Debug.Write("-");
            }
            return solutions.ToList();
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
                if (action == prevSet.Last() && Atlas.Actions.Buffs.Contains(action)) continue;

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
        private static void GetLookupDictionary(Dictionary<Action, int> actionChoices, out Dictionary<Action, int> lookup)
        {
            List<Action> choiceList = actionChoices.Keys.ToList();
            lookup = new Dictionary<Action, int>();

            for (int i = 0; i < choiceList.Count; i++)
            {
                lookup.Add(choiceList[i], i);
            }
        }

        private static List<Action>? InsertDurability(Simulator sim, State startState, List<List<Action>> actions, Action[] durabilityActions, double cpMax)
        {
            List<List<Action>> durabilitySolutions = new();
            Dictionary<Action, int> actionChoices = new();
            foreach (var action in durabilityActions) actionChoices.Add(action, 999);
            List<List<Action>> results = durabilityActions.Select(action => new List<Action> { action }).ToList();
            do
            {
                results = results.SelectMany(result => Iterate(result, actionChoices, true)).Where(result => result.Sum(action => action.CPCost) <= cpMax).ToList();
                durabilitySolutions.AddRange(results);
            } while (results.Count > 0);

            var solution = ZipListSets(sim, startState, actions, durabilitySolutions, true);
            if (solution.Any())
            {
                return solution.Last().Value;
            }
            else
            {
                return null;
            }
        }
        public static SortedList<double, List<Action>> ZipListSets(Simulator sim, State startState, List<List<Action>> left, List<List<Action>> right, bool useDurability)
        {
            SortedList<double, List<Action>> zippedLists = new();
            for (int leftIx = 0; leftIx < left.Count; leftIx++)
            {
                int rightIx = 0;
                List<Action> leftList = left[leftIx], rightList = right[rightIx];

                int[] merger = new int[leftList.Count + rightList.Count];
                for (int k = 0; k < merger.Length; k++) merger[k] = 0;

                while (Iterate(merger, merger.Length - 1))
                {
                    var actions = ZipLists(leftList, rightList, merger);
                    if (actions == null) continue;

                    State s = LightSimulator.Simulate(sim, actions, startState, useDurability);
                    var score = ScoreState(sim, s);
                    if (s == null || s.Progress < sim.Recipe.Difficulty || zippedLists.ContainsKey(score.Item1)) continue;

                    zippedLists.Add(score.Item1, actions);
                }
            }
            return zippedLists;
        }
        public static List<Action> ZipLists(List<Action> progress, List<Action> quality, int[] decider)
        {
            if (decider.Last() != 0)                                                            return null;
            else if (Atlas.Actions.FirstRoundActions.Contains(progress[0]) && decider[0] != 0)  return null;
            else if (Atlas.Actions.FirstRoundActions.Contains(quality[0]) && decider[0] != 1)   return null;

            int p = 0, q = 0;
            List<Action> actions = new List<Action>();
            for (int i = 0; i < decider.Length; i++)
            {
                if (decider[i] == 0)
                {
                    if (p >= progress.Count)
                    {
                        return null;
                    }
                    else
                    {
                        actions.Add(progress[p++]);
                    }
                }
                else
                {
                    if (q >= quality.Count)
                    {
                        return null;
                    }
                    else
                    {
                        actions.Add(quality[q++]);
                    }
                }
            }
            return actions;
        }
    }
}

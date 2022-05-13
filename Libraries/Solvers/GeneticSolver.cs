using System.Collections.Concurrent;
using System.Diagnostics;
using Action = Libraries.Action;

namespace Libraries.Solvers
{
    public class GeneticSolver : Solver.ISolver
    {
        const int MAX_GENERATION = 50;
        const int ELITE_PERCENTAGE = 10;
        const int MATE_PERCENTAGE = 50;
        const int INITIAL_POPULATION = 5000000; // 5 million
        const int GENERATION_SIZE = 500000; // 500 thousand
        const int PROB_MUTATION = 15;

        Random rand = new Random();
        Action[] genes;
        int probParentX = 100 - (PROB_MUTATION / 2);
        private Solver.LoggingDelegate _logger = (string message) => Debug.WriteLine(message);

        public class ListComparer : IComparer<KeyValuePair<double, List<Action>>>
        {
            int IComparer<KeyValuePair<double, List<Action>>>.Compare(KeyValuePair<double, List<Action>> x, KeyValuePair<double, List<Action>> y) => (int)(y.Key - x.Key);
        }

        public static Tuple<double, bool> ScoreState(Simulator sim, State state)
        {
            if (!state.CheckViolations().DurabilityOk || !state.CheckViolations().CpOk || !state.CheckViolations().TrickOk)
                return new Tuple<double, bool>(-1, false);
            bool perfectSolution = state.Quality >= sim.Recipe.MaxQuality && state.Progress >= sim.Recipe.Difficulty;
            double progress = (state.Progress > sim.Recipe.Difficulty ? sim.Recipe.Difficulty : state.Progress) / sim.Recipe.Difficulty;
            if (progress >= 1)
            {
                double maxQuality = sim.Recipe.MaxQuality * 1.1;
                double quality = (state.Quality > maxQuality ? maxQuality : state.Quality) / sim.Recipe.MaxQuality;
                return new Tuple<double, bool>((progress + quality) * 100, perfectSolution);
            }
            else
            {
                return new Tuple<double, bool>(progress * 100, perfectSolution);
            }
        }

        #region Audits
        public delegate bool Audit(List<Action> actions, List<Action> crafterActions);
        public static Audit[] audits = new Audit[]
        {
                AuditInnerQuiet,
                AuditRepeatBuffs,
                AuditFirstRound,
                AuditFocused,
                AuditTrainedFinesse
        };

        public static bool AuditLastAction(List<Action> actions, List<Action> crafterActions)
        {
            return Atlas.Actions.ProgressActions.Contains(actions.Last());
        }
        public static bool AuditInnerQuiet(List<Action> actions, List<Action> crafterActions)
        {
            if (actions.Any(x => x.Equals(Atlas.Actions.InnerQuiet) || x.Equals(Atlas.Actions.Reflect)))
            {
                if (crafterActions.Contains(Atlas.Actions.ByregotsBlessing))
                {
                    if (!actions.Any(x => x.Equals(Atlas.Actions.ByregotsBlessing))) return false;
                }
                else if (actions.Count(x => x.Equals(Atlas.Actions.InnerQuiet) || x.Equals(Atlas.Actions.Reflect)) > 1) return false;
            }
            return true;
        }
        public static bool AuditRepeatBuffs(List<Action> actions, List<Action> crafterActions)
        {
            return actions.Count < 2 || !(actions[0].Equals(actions[1]) && Atlas.Actions.Buffs.Contains(actions[0]));
        }
        public static bool AuditFirstRound(List<Action> actions, List<Action> crafterActions)
        {
            return !Atlas.Actions.FirstRoundActions.Any(x => actions.LastIndexOf(x) > 0);
        }
        public static bool AuditFocused(List<Action> actions, List<Action> crafterActions)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                Action action = actions[i];
                if (action.Equals(Atlas.Actions.FocusedSynthesis) || action.Equals(Atlas.Actions.FocusedTouch))
                {
                    if (i - 1 < 0 || !actions[i - 1].Equals(Atlas.Actions.Observe)) return false;
                }
            }
            return true;
        }
        private static bool AuditTrainedFinesse(List<Action> actions, List<Action> crafterActions)
        {
            int iq = 0;
            foreach (Action action in actions)
            {
                if (action.QualityIncreaseMultiplier > 0) iq++;
                if (action.Equals(Atlas.Actions.PreparatoryTouch)) iq++;
                if (action.Equals(Atlas.Actions.Reflect)) iq++;
                if (action.Equals(Atlas.Actions.ByregotsBlessing)) iq = 0;

                if (action.Equals(Atlas.Actions.TrainedFinesse) && iq < 10) return false;
            }
            return true;
        }

        public static bool SolutionAudit(List<Action> actions, List<Action> crafterActions) => audits.All(audit => audit(actions, crafterActions));
        public static List<int> GetIndices(List<Action> actions, Action action)
        {
            List<int> res = new List<int>();
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i] == action) res.Add(i);
            }
            return res;
        }
        #endregion

        public Action MutateGene(Random r) => genes[r.Next(genes.Length - 1)];
        public List<Action> CreateChromosome(int length, Random r)
        {
            List<Action> actions = new List<Action>();
            for (int i = 0; i < length; i++)
            {
                Action action = MutateGene(r);
                actions.Add(action);
                if (action.Equals(Atlas.Actions.DummyAction)) break;
            }
            return actions;
        }
        public List<Action> Mate(List<Action> parent1, List<Action> parent2, Random r)
        {
            List<Action> actions = new List<Action>();

            int length = r.Next(2) == 0 ? parent1.Count : parent2.Count;
            for (int i = 0; i < length; i++)
            {
                Action action;
                int p = r.Next(100);

                if (p <= probParentX) action = parent1.Count - 1 > i ? parent2[i] : parent1[i];
                else if (p <= probParentX * 2) action = parent2.Count - 1 > i ? parent1[i] : parent2[i];
                else action = MutateGene(r);

                actions.Add(action);
                if (action.Equals(Atlas.Actions.DummyAction)) break;
            }

            return actions;
        }

        public void BuildPopulation(ConcurrentBag<List<Action>> population, int maxLength, int maxTasks)
        {
            var genesList = genes.ToList();
            Parallel.For(0, maxTasks - 1, (i) =>
            {
                Random r = new Random(rand.Next() % (i + 1));
                for (int x = 0; x < INITIAL_POPULATION; x += maxTasks)
                {
                    var chromosome = CreateChromosome(maxLength, r);
                    if (!SolutionAudit(chromosome, genesList)) continue;
                    population.Add(chromosome);
                }
            });
        }

        public List<Action> GetSucessfulSteps(Simulator sim, List<Action> actions, State startState)
        {
            State finishState = sim.Simulate(actions, startState);
            return actions.Take(finishState.LastStep).ToList();
        }

        public List<Action> Run(Simulator sim, int maxTasks, Solver.LoggingDelegate? loggingDelegate = null)
        {
            int generation = 1;

            genes = sim.Crafter.Actions;
            List<Action> genesList = genes.ToList();
            ListComparer comparer = new ListComparer();

            int maxLength = sim.MaxLength;
            State startState = sim.Simulate(null, new State(sim, null));

            ConcurrentBag<List<Action>> population = new ConcurrentBag<List<Action>>();
            object lockObj = new object();
            bool foundPerfect = false;
            List<Action> perfect = new List<Action>();

            Stopwatch sw = new Stopwatch();
            sw.Start();

            BuildPopulation(population, maxLength, maxTasks);

            while (true)
            {
                GC.Collect();
                if (population.Count == 0) BuildPopulation(population, maxLength, maxTasks);

                ConcurrentBag<KeyValuePair<double, List<Action>>> scoredPopulation = new ConcurrentBag<KeyValuePair<double, List<Action>>>();
                Parallel.For(0, maxTasks - 1, (i) =>
                {
                    while (!foundPerfect && population.TryTake(out List<Action> chromosome))
                    {
                        State state = sim.Simulate(chromosome, startState);
                        Tuple<double, bool> score = ScoreState(sim, state);
                        if (score.Item1 > 0)
                        {
                            if (state.CheckViolations().ProgressOk && score.Item2)
                            {
                                foundPerfect = true;
                                perfect = chromosome;
                                continue;
                            }
                            scoredPopulation.Add(new KeyValuePair<double, List<Action>>(score.Item1, chromosome));
                        }
                    }
                });

                if (foundPerfect)
                    return GetSucessfulSteps(sim, perfect, startState);

                population = new ConcurrentBag<List<Action>>();
                List<KeyValuePair<double, List<Action>>> scores = scoredPopulation.ToList();
                scores.Sort(comparer);
                if (generation == MAX_GENERATION)
                    return GetSucessfulSteps(sim, scores.First().Value, startState);
                scores.RemoveAll(x => x.Key == -1);

                if (scores.Any())
                {
                    var best = scores.First();
                    int generationSize = generation <= 2 ? INITIAL_POPULATION : GENERATION_SIZE;

                    // take top percent of population
                    int populationSize = Math.Min(generationSize, scores.Count);
                    int eliteCount = (int)Math.Ceiling(populationSize * ((double)ELITE_PERCENTAGE / 100));
                    IEnumerable<List<Action>> elites = scores.Take(eliteCount).Select(x => x.Value);
                    foreach (List<Action> elite in elites) population.Add(elite);

                    // mate next percent of population
                    int mateCount = (int)Math.Ceiling(populationSize * ((double)MATE_PERCENTAGE / 100));
                    List<List<Action>> matingPool = scores.Take(mateCount).Select(x => x.Value).ToList();
                    while (true)
                    {
                        List<Action> parent1 = matingPool[rand.Next(mateCount - 1)];
                        List<Action> parent2 = matingPool[rand.Next(mateCount - 1)];
                        var chromosome = Mate(parent1, parent2, rand);
                        if (!SolutionAudit(chromosome, genesList)) continue;
                        if (population.Count >= generationSize) break;
                        population.Add(chromosome);
                    }
                }

                generation++;
            }
        }
    }
}

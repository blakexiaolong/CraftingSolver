namespace Libraries
{
    public class State
    {
        public Simulator Simulator { get; init; }
        public int Step { get; set; }
        public int LastStep { get; set; }
        public Action? Action { get; set; } // the action leading to this state
        public double Durability { get; set; }
        public double Cp { get; set; }
        public double Quality { get; set; }
        public double Progress { get; set; }
        public int WastedActions { get; set; }
        public Dictionary<Action, int> CountUps { get; init; }
        public Dictionary<Action, int> CountDowns { get; init; }
        public Dictionary<Action, int> Indefinites { get; init; }

        private int QualityGain { get; init; }
        private int BProgressGain { get; init; }
        private int BQualityGain { get; init; }
        public bool Success => Progress >= Simulator.Recipe.Difficulty && Cp >= 0;

        public readonly Dictionary<string, int> WastedCounter = new()
        {
            { "BadConditional", 0 },
            { "BBWithoutIQ", 0 },
            { "NonFirstTurn", 0 },
            { "Nameless", 0 },
            { "NonDummyAfterDummy", 0 },
            { "OverProgress", 0 },
            { "OutOfDurability", 0 },
            { "OutOfCP", 0 },
            { "PrudentUnderWasteNot", 0 },
            { "Unfocused", 0 },
            { "UntrainedFinesse", 0 }
        };

        public State(Simulator simulator, Action? action)
        {
            Simulator = simulator;
            Action = action;
            CountDowns = new();
            CountUps = new();
            Indefinites = new();
        }

        public State Clone()
        {
            return new(Simulator, Action)
            {
                Step = Step,
                LastStep = LastStep,
                Durability = Durability,
                Cp = Cp,
                Quality = Quality,
                Progress = Progress,
                WastedActions = WastedActions,
                CountUps = CountUps.ToDictionary(x => x.Key, x => x.Value),
                CountDowns = CountDowns.ToDictionary(x => x.Key, x => x.Value),
                Indefinites = Indefinites.ToDictionary(x => x.Key, x => x.Value),
                QualityGain = QualityGain,
                BProgressGain = BProgressGain,
                BQualityGain = BQualityGain
            };
        }

        private void ApplySpecialActionEffects(Action action)
        {
            if (action.Equals(Atlas.Actions.MastersMend))
            {
                Durability += 30;
            }

            if (CountDowns.ContainsKey(Atlas.Actions.Manipulation) && Durability > 0 && action != Atlas.Actions.Manipulation)
            {
                Durability += 5;
            }

            if (action.Equals(Atlas.Actions.ByregotsBlessing))
            {
                if (CountUps[Atlas.Actions.InnerQuiet] > 0)
                {
                    CountUps[Atlas.Actions.InnerQuiet] = 0;
                }
            }

            if (action.Equals(Atlas.Actions.Reflect))
            {
                if (Step == 1)
                {
                    CountUps[Atlas.Actions.InnerQuiet] = 1;
                }
                else
                {
                    WastedActions++;
                    WastedCounter["NonFirstTurn"]++;
                }
            }

            if (action.QualityIncreaseMultiplier > 0 && CountDowns.ContainsKey(Atlas.Actions.GreatStrides))
            {
                CountDowns.Remove(Atlas.Actions.GreatStrides);
            }
        }

        private void UpdateEffectCounters(Action action)
        {
            List<Action> buffDrops = new();
            foreach (var a in CountDowns)
            {
                CountDowns[a.Key]--;
                if (CountDowns[a.Key] == 0)
                {
                    buffDrops.Add(a.Key);
                }
            }
            foreach (var a in buffDrops)
            {
                CountDowns.Remove(a);
            }

            if (action.Equals(Atlas.Actions.BasicTouch))
            {
                if (CountDowns.ContainsKey(Atlas.Actions.BasicTouch))
                {
                    CountDowns[Atlas.Actions.BasicTouch] = 2;
                }
                else
                {
                    CountDowns.Add(Atlas.Actions.BasicTouch, 2);
                }
            }

            // conditional IQ countups
            if (action.Equals(Atlas.Actions.PreparatoryTouch))
            {
                CountUps[Atlas.Actions.InnerQuiet] += 2;
            }
            else if (action.QualityIncreaseMultiplier > 0 && !action.Equals(Atlas.Actions.ByregotsBlessing))
            {
                CountUps[Atlas.Actions.InnerQuiet] += 1;
            }
            CountUps[Atlas.Actions.InnerQuiet] = Math.Min(CountUps[Atlas.Actions.InnerQuiet], 10);

            switch (action.ActionType)
            {
                case ActionType.CountUp:
                    if (CountUps.ContainsKey(action))
                    {
                        CountUps[action] = 0;
                    }
                    else
                    {
                        CountUps.Add(action, 0);
                    }

                    break;
                case ActionType.Indefinite:
                    Indefinites.Add(action, 1);
                    break;
                case ActionType.CountDown:
                    if (CountDowns.ContainsKey(action))
                    {
                        CountDowns[action] = action.ActiveTurns;
                    }
                    else
                    {
                        CountDowns.Add(action, action.ActiveTurns);
                    }

                    break;
                case ActionType.Immediate:
                    break;
                default:
                    throw new InvalidOperationException($"Action Type {action.ActionType} was unrecognized");
            }
        }

        public void UpdateState(Action action, double progressGain, double qualityGain, double durabilityCost, int cpCost)
        {
            Progress += progressGain;
            Quality += qualityGain;
            Durability -= durabilityCost;
            Cp -= cpCost;
            LastStep += 1;

            if (Cp < 0)
            {
                WastedCounter["OutOfCP"]++;
            }

            ApplySpecialActionEffects(action);
            UpdateEffectCounters(action);

            // Sanity Checking
            Durability = Math.Min(Durability, Simulator.Recipe.Durability);
            Cp = Math.Min(Cp, Simulator.Crafter.CP);
        }
    
        public StateViolations CheckViolations()
        {
            return new StateViolations
            {
                ProgressOk = Progress >= Simulator.Recipe.Difficulty,
                CpOk = Cp >= 0,
                DurabilityOk = Durability >= 0
            };
        }
    }
}

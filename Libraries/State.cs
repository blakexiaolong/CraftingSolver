using System;
using System.Collections.Generic;
using System.Linq;

namespace CraftingSolver
{
    public class State
    {
        public Simulator Simulator { get; set; }
        public int Step { get; set; }
        public int LastStep { get; set; }
        public Action Action { get; set; } // the action leading to this state
        public double Durability { get; set; }
        public double CP { get; set; }
        public int BonusMaxCP { get; set; }
        public double Quality { get; set; }
        public double Progress { get; set; }
        public int WastedActions { get; set; }
        public int TrickUses { get; set; }
        public int NameOfElementUses { get; set; }
        public double Reliability { get; set; }
        public ICondition Condition { get; set; }
        public Dictionary<Action, int> CountUps { get; set; }
        public Dictionary<Action, int> CountDowns { get; set; }
        public Dictionary<Action, int> Indefinites { get; set; }

        public int IQ { get; set; }
        public int Control { get; set; }
        public int QualityGain { get; set; }
        public int BProgressGain { get; set; }
        public int BQualityGain { get; set; }
        public bool Success => Progress >= Simulator.Recipe.Difficulty && CP >= 0;

        public Dictionary<string, int> WastedCounter = new Dictionary<string, int>
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

        public State()
        {
            CountDowns = new Dictionary<Action, int>();
            CountUps = new Dictionary<Action, int>();
            Indefinites = new Dictionary<Action, int>();
        }

        public State Clone()
        {
            return new State
            {
                Simulator = Simulator,
                Step = Step,
                LastStep = LastStep,
                Action = Action,
                Durability = Durability,
                CP = CP,
                BonusMaxCP = BonusMaxCP,
                Quality = Quality,
                Progress = Progress,
                WastedActions = WastedActions,
                TrickUses = TrickUses,
                NameOfElementUses = NameOfElementUses,
                Reliability = Reliability,
                CountUps = CountUps.ToDictionary(x => x.Key, x => x.Value),
                CountDowns = CountUps.ToDictionary(x => x.Key, x => x.Value),
                Indefinites = CountUps.ToDictionary(x => x.Key, x => x.Value),
                Condition = Condition,

                IQ = IQ,
                Control = Control,
                QualityGain = QualityGain,
                BProgressGain = BProgressGain,
                BQualityGain = BQualityGain
            };
        }

        public bool UseConditionalAction()
        {
            if (CP > 0 && Condition.CheckGoodOrExcellent())
            {
                TrickUses += 1;
                return true;
            }
            else
            {
                WastedActions++;
                WastedCounter["BadConditional"]++;
                return false;
            }
        }
        public void ApplySpecialActionEffects(Action action)
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
                CountUps.TryGetValue(Atlas.Actions.InnerQuiet, out int iq);
                if (iq != default)
                {
                    CountUps.Remove(Atlas.Actions.InnerQuiet);
                }
                else
                {
                    WastedActions++;
                    WastedCounter["BBWithoutIQ"]++;
                }
            }

            if (action.Equals(Atlas.Actions.Reflect))
            {
                if (Step == 1)
                {
                    CountUps.Add(Atlas.Actions.InnerQuiet, 1);
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

            if (action.OnExcellent || action.OnGood)
            {
                if (UseConditionalAction())
                {
                    if (action.Equals(Atlas.Actions.TricksOfTheTrade))
                    {
                        CP += 20 * Condition.PGoodOrExcellent();
                    }
                }
            }
        }

        public void UpdateEffectCounters(Action action, double successProbability)
        {
            foreach (Action a in CountDowns.Keys.ToList())
            {
                if (CountDowns[a]-- == 0)
                {
                    CountDowns.Remove(a);
                }
            }

            CountUps.TryGetValue(Atlas.Actions.InnerQuiet, out int iq);
            if (iq > 0)
            {
                if (!action.Equals(Atlas.Actions.ByregotsBlessing) && action.QualityIncreaseMultiplier > 0 && !CountUps.ContainsKey(Atlas.Actions.InnerQuiet))
                {
                    CountUps.Add(Atlas.Actions.InnerQuiet, 0);
                }

                // conditional IQ countups
                if (action.Equals(Atlas.Actions.PreparatoryTouch))
                {
                    CountUps[Atlas.Actions.InnerQuiet] += 2;
                }
                else if (action.Equals(Atlas.Actions.PreciseTouch) && Condition.CheckGoodOrExcellent())
                {
                    CountUps[Atlas.Actions.InnerQuiet] += (int)(2 * successProbability * Condition.PGoodOrExcellent());
                }
                else if (action.QualityIncreaseMultiplier > 0)
                {
                    CountUps[Atlas.Actions.InnerQuiet] += Convert.ToInt32(1 * successProbability);
                }

                CountUps[Atlas.Actions.InnerQuiet] = Math.Min(CountUps[Atlas.Actions.InnerQuiet], 10);
            }

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
        public void UpdateState(Action action, double progressGain, double qualityGain, double durabilityCost, int cpCost, double successProbability)
        {
            Progress += progressGain;
            Quality += qualityGain;
            Durability -= durabilityCost;
            CP -= cpCost;
            LastStep += 1;

            if (CP < 0)
            {
                WastedCounter["OutOfCP"]++;
            }

            ApplySpecialActionEffects(action);
            UpdateEffectCounters(action, successProbability);

            // Sanity Checking
            Durability = Math.Min(Durability, Simulator.Recipe.Durability);
            CP = Math.Min(CP, Simulator.Crafter.CP + BonusMaxCP);
        }
    
        public StateViolations CheckViolations()
        {
            return new StateViolations
            {
                ProgressOk = Progress >= Simulator.Recipe.Difficulty,
                CpOk = CP >= 0,
                DurabilityOk = Durability >= 0,
                TrickOk = TrickUses <= Simulator.MaxTrickUses,
                ReliabilityOk = Reliability >= Simulator.ReliabilityIndex
            };
        }

        public int BuffDuration(Action buff)
        {
            try
            {
                switch (buff.ActionType)
                {
                    case ActionType.CountDown:
                        return CountDowns[buff];
                    case ActionType.CountUp:
                        return CountUps[buff];
                    case ActionType.Immediate:
                        return Action.Equals(buff) ? 1 : 0;
                    case ActionType.Indefinite:
                        return Indefinites[buff];
                    default:
                        throw new Exception("Action Type not recognized");
                }
            }
            catch
            {
                return 0;
            }
        }
    }
}

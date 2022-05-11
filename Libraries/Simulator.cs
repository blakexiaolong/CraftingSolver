using System;
using System.Collections.Generic;

namespace CraftingSolver
{
    public class Simulator
    {
        public Crafter Crafter { get; set; }
        public Recipe Recipe { get; set; }
        public int MaxTrickUses { get; set; }
        public bool UseConditions { get; set; }
        public double ReliabilityIndex { get; set; }
        public int MaxLength { get; set; }

        public int EffectiveCrafterLevel { get; set; }
        public int LevelDifference { get; set; }
        public int PureLevelDifference { get; set; }
        public double BaseProgressIncrease { get; set; }
        public double BaseQualityIncrease { get; set; }

        public void Initialize()
        {
            EffectiveCrafterLevel = GetEffectiveCrafterLevel();
            LevelDifference = Math.Min(49, Math.Max(-30, EffectiveCrafterLevel - Recipe.Level));
            PureLevelDifference = Crafter.Level - Recipe.Level;
            BaseProgressIncrease = CalculateBaseProgressIncrease();
            BaseQualityIncrease = CalculateBaseQualityIncrease();
        }

        public State Simulate(List<Action>? actions, State startState, bool assumeSuccess = false, bool verbose = false, bool debug = false, bool useDurability = true)
        {
            State s = startState.Clone();

            double pGood = ProbabilityGoodForSynth();
            double pExcellent = ProbabilityExcellentForSynth();

            double ppGood = 0, ppExcellent = 0, ppPoor = 0, ppNormal = 1 - (ppGood + ppExcellent + ppPoor);

            if (actions == null || actions.Count == 0)
            {
                return NewStateFromSynth(!UseConditions, ppExcellent, ppGood);
            }

            foreach (Action action in actions)
            {
                s.Step++;
                SimulatorStep r = ApplyModifiers(s, action);

                double progressGain, qualityGain, successProbability = assumeSuccess ? 1 : r.SuccessProbability;

                double condQualityIncreaseMultiplier = 1;
                if (UseConditions)
                {
                    condQualityIncreaseMultiplier *= ppNormal + 1.5 * ppGood * Math.Pow((1 - ppGood * 2) / 2, MaxTrickUses) + 4 * ppExcellent + 0.5 * ppPoor;
                }

                if (assumeSuccess || successProbability == 1)
                {
                    progressGain = Math.Floor(r.BProgressGain);
                    qualityGain = Math.Floor(condQualityIncreaseMultiplier * r.BQualityGain);
                }
                else
                {
                    progressGain = r.BProgressGain;
                    if (progressGain > 0)
                    {
                        s.Reliability *= successProbability;
                    }
                    progressGain = successProbability * Math.Floor(progressGain);

                    qualityGain = successProbability * Math.Floor(condQualityIncreaseMultiplier * r.BQualityGain);
                }

                if (Action.Equals(s.Action, Atlas.Actions.DummyAction) && !action.Equals(Atlas.Actions.DummyAction))
                {
                    s.WastedActions++;
                    s.WastedCounter["NonDummyAfterDummy"]++;
                }
                if (s.Progress >= Recipe.Difficulty && !action.Equals(Atlas.Actions.DummyAction))
                {
                    s.WastedActions++;
                    s.WastedCounter["OverProgress"]++;
                }
                else if(s.Durability <= 0 && !action.Equals(Atlas.Actions.DummyAction))
                {
                    s.WastedActions++;
                    s.WastedCounter["OutOfDurability"]++;
                }
                else if (s.CP < 0 && !action.Equals(Atlas.Actions.DummyAction))
                {
                    s.WastedActions++;
                    s.WastedCounter["OutOfCP"]++;
                }
                else
                {
                    s.UpdateState(action, progressGain, qualityGain, useDurability ? r.DurabilityCost : 0, r.CPCost, successProbability);
                    if (UseConditions)
                    {
                        ppPoor = ppExcellent;
                        ppGood = pGood * ppNormal;
                        ppExcellent = pExcellent * ppNormal;
                        ppNormal = 1 - (ppGood + ppExcellent + ppPoor);
                    }
                }

                s.Action = action;
            }

            s.Action = actions[actions.Count - 1];
            return s;
        }

        public State NewStateFromSynth(bool ignoreConditions, double ppExcellent, double ppGood)
        {
            return new State
            {
                Simulator = this,
                Step = 0,
                LastStep = 0,
                Action = null,
                Durability = Recipe.Durability,
                CP = Crafter.CP,
                BonusMaxCP = 0,
                Quality = Recipe.StartQuality,
                Progress = 0,
                WastedActions = 0,
                TrickUses = 0,
                NameOfElementUses = 0,
                Reliability = 1,
                Indefinites = new Dictionary<Action, int>(),
                CountDowns = new Dictionary<Action, int>(),
                CountUps = new Dictionary<Action, int>(),
                Condition = new SimulatorCondition
                {
                    ConditionQuality = ConditionQuality.Normal,
                    IgnoreConditions = ignoreConditions,
                    PPExcellent = ppExcellent,
                    PPGood = ppGood
                }
            };
        }
        public SimulatorStep ApplyModifiers(State state, Action action)
        {
            int cpCost = action.CPCost;
            double progressIncreaseMultiplier = CalcProgressMultiplier(state, action);
            double qualityIncreaseMultiplier = CalcQualityMultiplier(state, action);
            double bProgressGain = BaseProgressIncrease * action.ProgressIncreaseMultiplier * progressIncreaseMultiplier;
            double bQualityGain = BaseQualityIncrease * action.QualityIncreaseMultiplier * qualityIncreaseMultiplier;

            if (action.Equals(Atlas.Actions.FlawlessSynthesis))
            {
                bProgressGain = 40;
            }

            // combo actions
            if (state.Action != default)
            {
                if (action.Equals(Atlas.Actions.StandardTouch) && state.Action.Equals(Atlas.Actions.BasicTouch))
                {
                    cpCost = 18;
                }
                else if (action.Equals(Atlas.Actions.AdvancedTouch) && state.Action.Equals(Atlas.Actions.StandardTouch))
                {
                    cpCost = 18;
                }
            }

            // can only use Precise Touch if Good or Excellent
            if (action.Equals(Atlas.Actions.PreciseTouch))
            {
                if (state.Condition.CheckGoodOrExcellent())
                {
                    bQualityGain *= state.Condition.PGoodOrExcellent();
                }
                else
                {
                    state.WastedActions++;
                    state.WastedCounter["BadConditional"]++;
                    bQualityGain = 0;
                    cpCost = 0;
                }
            }

            // first round actions
            if (action.Equals(Atlas.Actions.TrainedEye))
            {
                if (state.Step == 1 && PureLevelDifference >= 10 && !Recipe.IsExpert)
                {
                    bQualityGain = Recipe.MaxQuality;
                }
                else
                {
                    state.WastedActions++;
                    state.WastedCounter["NonFirstTurn"]++;
                    bQualityGain = 0;
                    cpCost = 0;
                }
            }
            if (action.Equals(Atlas.Actions.Reflect) && state.Step != 1)
            {
                state.WastedActions++;
                state.WastedCounter["NonFirstTurn"]++;
                bQualityGain = 0;
                cpCost = 0;
            }
            if (action.Equals(Atlas.Actions.MuscleMemory) && state.Step != 1)
            {
                state.WastedActions++;
                state.WastedCounter["NonFirstTurn"]++;
                bProgressGain = 0;
                cpCost = 0;
            }

            // Effects modifying durability cost
            double durabilityCost = action.DurabilityCost;
            if (state.CountDowns.ContainsKey(Atlas.Actions.WasteNot) || state.CountDowns.ContainsKey(Atlas.Actions.WasteNot2))
            {
                if (action.Equals(Atlas.Actions.PrudentTouch) || action.Equals(Atlas.Actions.PrudentSynthesis))
                {
                    bQualityGain = 0;
                    state.WastedActions++;
                    state.WastedCounter["PrudentUnderWasteNot"]++;
                }
                else
                {
                    durabilityCost *= 0.5;
                }
            }

            if (action.Equals(Atlas.Actions.TrainedFinesse))
            {
                state.CountUps.TryGetValue(Atlas.Actions.InnerQuiet, out int iq);
                if (iq != 10)
                {
                    state.WastedActions++;
                    state.WastedCounter["UntrainedFinesse"]++;
                    bQualityGain = 0;
                    cpCost = 0;
                }
            }

            return new SimulatorStep
            {
                Craftsmanship = Crafter.Craftsmanship,
                Control = Crafter.Control,
                EffectiveCrafterLevel = EffectiveCrafterLevel,
                EffectiveRecipeLevel = Recipe.Level,
                LevelDifference = LevelDifference,
                SuccessProbability = CalcSuccessProbability(state, action),
                QualityIncreaseMultiplier = qualityIncreaseMultiplier,
                BProgressGain = bProgressGain,
                BQualityGain = bQualityGain,
                DurabilityCost = durabilityCost,
                CPCost = cpCost
            };
        }

        private double CalcSuccessProbability(State state, Action action)
        {
            double successProbability = action.SuccessProbability;
            if (action.Equals(Atlas.Actions.FocusedSynthesis) || action.Equals(Atlas.Actions.FocusedTouch))
            {
                if (Action.Equals(state.Action, Atlas.Actions.Observe))
                {
                    successProbability = 1;
                }
                else
                {
                    state.WastedActions++;
                    state.WastedCounter["Unfocused"]++;
                }
            }
            return Math.Min(successProbability, 1);
        }
        private double CalcProgressMultiplier(State state, Action action)
        {
            double progressIncreaseMultiplier = 1;
            if (action.ProgressIncreaseMultiplier > 0 && state.CountDowns.ContainsKey(Atlas.Actions.MuscleMemory))
            {
                progressIncreaseMultiplier += 1;
                state.CountDowns.Remove(Atlas.Actions.MuscleMemory);
            }
            if (state.CountDowns.ContainsKey(Atlas.Actions.Veneration))
            {
                progressIncreaseMultiplier += 0.5;
            }
            if (action.Equals(Atlas.Actions.Groundwork) && state.Durability < Atlas.Actions.Groundwork.DurabilityCost)
            {
                progressIncreaseMultiplier *= 0.5;
            }
            return progressIncreaseMultiplier;
        }
        private double CalcQualityMultiplier(State state, Action action)
        {
            double qualityIncreaseMultiplier = 1;
            if (state.CountDowns.ContainsKey(Atlas.Actions.GreatStrides) && qualityIncreaseMultiplier > 0)
            {
                qualityIncreaseMultiplier += 1;
            }
            if (state.CountDowns.ContainsKey(Atlas.Actions.Innovation))
            {
                qualityIncreaseMultiplier += 0.5;
            }

            state.CountUps.TryGetValue(Atlas.Actions.InnerQuiet, out int iq);
            if (action.Equals(Atlas.Actions.ByregotsBlessing))
            {               
                if (iq > 0)
                {
                    qualityIncreaseMultiplier *= Math.Min(3, 1 + iq * 0.2);
                }
                else
                {
                    qualityIncreaseMultiplier = 0;
                    state.WastedActions++;
                    state.WastedCounter["BBWithoutIQ"]++;
                }
            }
            qualityIncreaseMultiplier *= 1 + (0.1 * iq);
            return qualityIncreaseMultiplier;
        }

        public double CalculateBaseProgressIncrease()
        {
            double b = (Crafter.Craftsmanship * 10 / Recipe.ProgressDivider + 2);
            return LevelDifference <= 0 ? b * Recipe.ProgressModifier : b;
        }
        public double CalculateBaseQualityIncrease()
        {
            double b = (Crafter.Control * 10 / Recipe.QualityDivider + 35);
            return LevelDifference <= 0 ? b * Recipe.QualityModifier : b;
        }

        public double ProbabilityGoodForSynth()
        {
            int recipeLevel = Recipe.Level;
            bool qualityAssurance = Crafter.Level >= 63;
            if (recipeLevel >= 300)
            {
                return qualityAssurance ? 0.11 : 0.10;
            }
            else if (recipeLevel >= 276)
            {
                return qualityAssurance ? 0.17 : 0.15;
            }
            else if (recipeLevel >= 255)
            {
                return qualityAssurance ? 0.22 : 0.20;
            }
            else if (recipeLevel >= 150)
            {
                return qualityAssurance ? 0.11 : 0.10;
            }
            else if (recipeLevel >= 136)
            {
                return qualityAssurance ? 0.17 : 0.15;
            }
            else
            {
                return qualityAssurance ? 0.27 : 0.25;
            }
        }
        public double ProbabilityExcellentForSynth()
        {
            var recipeLevel = Recipe.Level;
            if (recipeLevel >= 300)
            {
                return 0.01;
            }
            else if (recipeLevel >= 255)
            {
                return 0.02;
            }
            else if (recipeLevel >= 150)
            {
                return 0.01;
            }
            else
            {
                return 0.02;
            }
        }

        public int GetEffectiveCrafterLevel()
        {
            if (!Atlas.LevelTable.TryGetValue(Crafter.Level, out int effectiveCrafterLevel))
            {
                effectiveCrafterLevel = Crafter.Level;
            }
            return effectiveCrafterLevel;
        }
        public double GetLevelDifferenceFactor(string kind, int levelDifference)
        {
            Dictionary<int, double> factors = Atlas.LevelDifferenceFactors[kind];
            if (factors == default)
            {
                throw new Exception("Unrecognized Level Difference Factor Type");
            }

            return factors[levelDifference];
        }
        
        public double QualityFromHqPercent(double hqPercent)
        {
            return -5.6604E-6 * Math.Pow(hqPercent, 4) + 0.0015369705 * Math.Pow(hqPercent, 3) - 0.1426469573 * Math.Pow(hqPercent, 2) + 5.6122722959 * hqPercent - 5.5950384565;
        }
        public double HqPercentFromQuality(double qualityPercent)
        {
            var hqPercent = 1;
            if (qualityPercent == 0)
            {
                hqPercent = 1;
            }
            else if (qualityPercent >= 100)
            {
                hqPercent = 100;
            }
            else
            {
                while (QualityFromHqPercent(hqPercent) < qualityPercent && hqPercent < 100)
                {
                    hqPercent += 1;
                }
            }
            return hqPercent;
        }
    }
}

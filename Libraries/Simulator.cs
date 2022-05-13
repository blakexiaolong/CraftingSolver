﻿using System;
using System.Collections.Generic;
using Libraries;

namespace CraftingSolver
{
    public class Simulator
    {
        public Crafter Crafter { get; init; }
        public Recipe Recipe { get; init; }
        public int MaxLength { get; init; }
        public double ReliabilityIndex { get; set; }

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

            if (actions == null || actions.Count == 0)
            {
                return NewStateFromSynth();
            }

            foreach (Action action in actions)
            {
                s.Step++;
                SimulatorStep r = ApplyModifiers(s, action);
                
                var progressGain = Math.Floor(r.BProgressGain);
                var qualityGain = Math.Floor(r.BQualityGain);

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
                else if (s.Cp < 0 && !action.Equals(Atlas.Actions.DummyAction))
                {
                    s.WastedActions++;
                    s.WastedCounter["OutOfCP"]++;
                }
                else
                {
                    s.UpdateState(action, progressGain, qualityGain, useDurability ? r.DurabilityCost : 0, r.CPCost);
                }

                s.Action = action;
            }

            s.Action = actions[actions.Count - 1];
            return s;
        }

        public State NewStateFromSynth()
        {
            return new State(this, null)
            {
                Step = 0,
                LastStep = 0,
                Durability = Recipe.Durability,
                Cp = Crafter.CP,
                Quality = Recipe.StartQuality,
                Progress = 0,
                WastedActions = 0,
                Indefinites = new Dictionary<Action, int>(),
                CountDowns = new Dictionary<Action, int>(),
                CountUps = new Dictionary<Action, int> { { Atlas.Actions.InnerQuiet, 0 } }
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
                int iq = state.CountUps[Atlas.Actions.InnerQuiet];
                if (iq != 10)
                {
                    state.WastedActions++;
                    state.WastedCounter["UntrainedFinesse"]++;
                    bQualityGain = 0;
                    cpCost = 0;
                }
            }

            if ((action.Equals(Atlas.Actions.FocusedSynthesis) || action.Equals(Atlas.Actions.FocusedTouch)) &&
                (state.Action == null || !state.Action.Equals(Atlas.Actions.Observe)))
            {
                state.WastedActions++;
                state.WastedCounter["Unfocused"]++;
            }

            return new SimulatorStep
            {
                Craftsmanship = Crafter.Craftsmanship,
                Control = Crafter.Control,
                EffectiveCrafterLevel = EffectiveCrafterLevel,
                EffectiveRecipeLevel = Recipe.Level,
                LevelDifference = LevelDifference,
                QualityIncreaseMultiplier = qualityIncreaseMultiplier,
                BProgressGain = bProgressGain,
                BQualityGain = bQualityGain,
                DurabilityCost = durabilityCost,
                CPCost = cpCost
            };
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
            
            int iq = state.CountUps[Atlas.Actions.InnerQuiet];
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

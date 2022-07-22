namespace Libraries
{
    public class LightSimulator
    {
        public Crafter Crafter { get; }
        public Recipe Recipe { get; }

        public int EffectiveCrafterLevel { get; }
        public int LevelDifference { get; }
        public int PureLevelDifference { get; }
        public double BaseProgressIncrease { get; }
        public double BaseQualityIncrease { get; }

        public LightSimulator(Crafter crafter, Recipe recipe)
        {
            Crafter = crafter;
            Recipe = recipe;

            EffectiveCrafterLevel = GetEffectiveCrafterLevel();
            LevelDifference = Math.Min(49, Math.Max(-30, EffectiveCrafterLevel - Recipe.RLevel));
            PureLevelDifference = Crafter.Level - Recipe.Level;
            BaseProgressIncrease = CalculateBaseProgressIncrease();
            BaseQualityIncrease = CalculateBaseQualityIncrease();
        }

        public LightState? Simulate(IEnumerable<Action> actions, bool useDurability = true)
        {
            ExtractState(null, out double progress, out double quality, out double cp, out double durability, out int innerQuiet, out int step, out Dictionary<Action, int> countdowns, Recipe.StartQuality, Crafter.CP, Recipe.Durability);
            foreach (var action in actions)
                if (!Simulate(action, ref progress, ref quality, ref cp, ref durability, ref innerQuiet, ref step, countdowns, useDurability)) return null;
            if (!useDurability) durability = Recipe.Durability;
            return SetState(progress, quality, cp, durability, innerQuiet, step, countdowns);
        }
        public LightState? Simulate(Action action, bool useDurability = true)
        {
            ExtractState(null, out double progress, out double quality, out double cp, out double durability, out int innerQuiet, out int step, out Dictionary<Action, int> countdowns, Recipe.StartQuality, Crafter.CP, Recipe.Durability);
            if (!Simulate(action, ref progress, ref quality, ref cp, ref durability, ref innerQuiet, ref step, countdowns, useDurability)) return null;
            if (!useDurability) durability = Recipe.Durability;
            return SetState(progress, quality, cp, durability, innerQuiet, step, countdowns);
        }
        public LightState? Simulate(IEnumerable<Action> actions, LightState startState, bool useDurability = true)
        {
            ExtractState(startState, out double progress, out double quality, out double cp, out double durability, out int innerQuiet, out int step, out Dictionary<Action, int> countdowns, Recipe.StartQuality, Crafter.CP, Recipe.Durability);
            foreach (var action in actions)
                if (!Simulate(action, ref progress, ref quality, ref cp, ref durability, ref innerQuiet, ref step, countdowns, useDurability)) return null;
            if (!useDurability) durability = Recipe.Durability;
            return SetState(progress, quality, cp, durability, innerQuiet, step, countdowns);
        }
        public LightState? Simulate(Action action, LightState startState, bool useDurability = true)
        {
            ExtractState(startState, out double progress, out double quality, out double cp, out double durability, out int innerQuiet, out int step, out Dictionary<Action, int> countdowns, Recipe.StartQuality, Crafter.CP, Recipe.Durability);
            if (!Simulate(action, ref progress, ref quality, ref cp, ref durability, ref innerQuiet, ref step, countdowns, useDurability)) return null;
            if (!useDurability) durability = Recipe.Durability;
            return SetState(progress, quality, cp, durability, innerQuiet, step, countdowns);
        }

        private static void ExtractState(LightState? state, out double progress, out double quality, out double cp, out double durability, out int innerQuiet, out int step, out Dictionary<Action, int> countdowns, double startQuality, double startCp, double startDurability)
        {
            if (state == null)
            {
                progress = 0;
                quality = startQuality;
                cp = startCp;
                durability = startDurability;
                innerQuiet = 0;
                step = 0;
                countdowns = new();
            }
            else
            {
                progress = state.Value.Progress;
                quality = state.Value.Quality;
                cp = state.Value.CP;
                durability = state.Value.Durability;
                innerQuiet = state.Value.InnerQuiet;
                step = state.Value.Step;
                countdowns = new(state.Value.CountDowns.Count);
                foreach (var kvp in state.Value.CountDowns)
                {
                    countdowns.Add(kvp.Key, kvp.Value);
                }
            }
        }
        private static LightState SetState(double progress, double quality, double cp, double durability, int innerQuiet, int step, Dictionary<Action, int> countdowns)
        {
            return new LightState
            {
                Progress = progress,
                Quality = quality,
                CP = cp,
                Durability = durability,
                InnerQuiet = innerQuiet,
                Step = step,
                CountDowns = countdowns
            };
        }

        private bool Simulate(Action action, ref double progress, ref double quality, ref double cp, ref double durability, ref int innerQuiet, ref int step, Dictionary<Action, int> countdowns, bool useDurability)
        {
            #region Wasted Action Checks
            if (progress >= Recipe.Difficulty) return false; // throw new WastedActionException("OverProgress");
            if (useDurability && durability <= 0) return false; // throw new WastedActionException("OutOfDurability");
            if (cp - action.CPCost < 0) return false; // throw new WastedActionException("OutOfCp");
            if (step > 0 && (action.Equals(Atlas.Actions.Reflect) || action.Equals(Atlas.Actions.MuscleMemory) || action.Equals(Atlas.Actions.TrainedEye))) return false; // throw new WastedActionException("NonFirstRound");
            if (action.Equals(Atlas.Actions.TrainedEye) && (PureLevelDifference >= 10 || !Recipe.IsExpert)) return false;  // throw new WastedActionException("UntrainedEye");
            if (action.Equals(Atlas.Actions.ByregotsBlessing) && innerQuiet == 0) return false; // throw new WastedActionException("ByregotsWithoutIQ");
            if (action.Equals(Atlas.Actions.TrainedFinesse) && innerQuiet < 10) return false; // throw new WastedActionException("UntrainedFinesse");
            if ((action.Equals(Atlas.Actions.FocusedSynthesis) || action.Equals(Atlas.Actions.FocusedTouch)) && !countdowns.ContainsKey(Atlas.Actions.Observe)) return false; // throw new WastedActionException("Unfocused");
            if ((action.Equals(Atlas.Actions.PrudentTouch) || action.Equals(Atlas.Actions.PrudentSynthesis)) && (countdowns.ContainsKey(Atlas.Actions.WasteNot) || countdowns.ContainsKey(Atlas.Actions.WasteNot2))) return false; //throw new WastedActionException("PrudentUnderWasteNot");
            #endregion

            int cpCost = action.CPCost;
            progress += Math.Floor(BaseProgressIncrease * action.ProgressIncreaseMultiplier * CalcProgressMultiplier(action, countdowns, durability));
            quality += Math.Floor(action.Equals(Atlas.Actions.TrainedEye) ? Recipe.MaxQuality : BaseQualityIncrease * action.QualityIncreaseMultiplier * CalcQualityMultiplier(action, countdowns, innerQuiet));

            #region Combos
            if (action.Equals(Atlas.Actions.StandardTouch) && countdowns.ContainsKey(Atlas.Actions.BasicTouch) && countdowns[Atlas.Actions.BasicTouch] == 2)
            {
                cpCost = 18;
            }
            else if (action.Equals(Atlas.Actions.AdvancedTouch) && countdowns.ContainsKey(Atlas.Actions.StandardTouch) && countdowns.ContainsKey(Atlas.Actions.BasicTouch) && countdowns[Atlas.Actions.StandardTouch] == 1 && countdowns[Atlas.Actions.BasicTouch] == 1)
            {
                cpCost = 18;
            }
            #endregion

            #region Durability
            double durabilityCost = action.DurabilityCost;
                
            #region Waste Not

            bool wn = false;
            if (countdowns.ContainsKey(Atlas.Actions.WasteNot))
            {
                wn = true;
                if (action.DurabilityCost > 0 && countdowns[Atlas.Actions.WasteNot] > 0) countdowns[Atlas.Actions.WasteNot] *= -1;
            }
            else if (countdowns.ContainsKey(Atlas.Actions.WasteNot2))
            {
                wn = true;
                if (action.DurabilityCost > 0 && countdowns[Atlas.Actions.WasteNot2] > 0) countdowns[Atlas.Actions.WasteNot2] *= -1;
            }

            if (wn)
            {
                durabilityCost *= 0.5;
            }

            #endregion

            durability -=  durabilityCost;

            #region Durability Restoration

            if (action.Equals(Atlas.Actions.MastersMend))
            {
                if (Math.Abs(durability - Recipe.Durability) < 0.9) return false;
                durability += 30;
            }
            
            if (countdowns.ContainsKey(Atlas.Actions.Manipulation) && durability > 0 && action != Atlas.Actions.Manipulation)
            {
                if (durability < Recipe.Durability && countdowns[Atlas.Actions.Manipulation] > 0) countdowns[Atlas.Actions.Manipulation] *= -1;
                durability += 5;
            }

            #endregion

            durability = Math.Min(durability, Recipe.Durability);
            #endregion

            #region Inner Quiet
            if (action.Equals(Atlas.Actions.ByregotsBlessing)) innerQuiet = 0;
            else if (action.Equals(Atlas.Actions.Reflect)) innerQuiet = 2;
            else if (action.Equals(Atlas.Actions.PreparatoryTouch)) innerQuiet += 2;
            else if (action.QualityIncreaseMultiplier > 0) innerQuiet += 1;
            #endregion

            #region Countdowns
            foreach (var buff in countdowns)
            {
                switch (countdowns[buff.Key])
                {
                    case > 0 when --countdowns[buff.Key] == 0:
                        return false; // throw new WastedActionException("UnusedBuff");
                    case < 0 when ++countdowns[buff.Key] == 0:
                        countdowns.Remove(buff.Key);
                        break;
                }
            }

            if (action.ActionType == ActionType.CountDown)
            {
                if (countdowns.ContainsKey(action))
                    if (countdowns[action] > 0) return false;
                    else countdowns[action] = action.ActiveTurns;
                else countdowns.Add(action, action.ActiveTurns);
            }
            #endregion

            step      += 1;
            innerQuiet = Math.Min(innerQuiet, 10);
            cp         = Math.Min(cp - cpCost, Crafter.CP);

            return true;
        }
        
        private static double CalcProgressMultiplier(Action action, Dictionary<Action, int> countdowns, double durability)
        {
            if (action.ProgressIncreaseMultiplier == 0) return 0;

            double progressIncreaseMultiplier = 1;
            if (action.ProgressIncreaseMultiplier > 0 && countdowns.ContainsKey(Atlas.Actions.MuscleMemory))
            {
                progressIncreaseMultiplier += 1;
                countdowns.Remove(Atlas.Actions.MuscleMemory);
            }

            if (countdowns.ContainsKey(Atlas.Actions.Veneration))
            {
                progressIncreaseMultiplier += 0.5;
                if (countdowns[Atlas.Actions.Veneration] > 0) countdowns[Atlas.Actions.Veneration] *= -1;
            }

            if (action.Equals(Atlas.Actions.Groundwork) && durability < Atlas.Actions.Groundwork.DurabilityCost)
            {
                progressIncreaseMultiplier *= 0.5;
            }

            return progressIncreaseMultiplier;
        }
        private static double CalcQualityMultiplier(Action action, Dictionary<Action, int> countdowns, int innerQuiet)
        {
            if (action.QualityIncreaseMultiplier == 0) return 0;

            double qualityIncreaseMultiplier = 1;
            if (countdowns.ContainsKey(Atlas.Actions.GreatStrides))
            {
                qualityIncreaseMultiplier += 1;
                countdowns.Remove(Atlas.Actions.GreatStrides);
            }
            if (countdowns.ContainsKey(Atlas.Actions.Innovation))
            {
                qualityIncreaseMultiplier += 0.5;
                if (countdowns[Atlas.Actions.Innovation] > 0) countdowns[Atlas.Actions.Innovation] *= -1;
            }
            
            if (action.Equals(Atlas.Actions.ByregotsBlessing))
            {
                if (innerQuiet > 0)
                {
                    qualityIncreaseMultiplier *= Math.Min(3, 1 + innerQuiet * 0.2);
                }
                else
                {
                    qualityIncreaseMultiplier = 0;
                }
            }
            qualityIncreaseMultiplier *= 1 + (0.1 * innerQuiet);
            return qualityIncreaseMultiplier;
        }

        private double CalculateBaseProgressIncrease()
        {
            double b = (Crafter.Craftsmanship * 10 / Recipe.ProgressDivider + 2);
            return Math.Floor(LevelDifference <= 0 ? b * Recipe.ProgressModifier : b);
        }
        private double CalculateBaseQualityIncrease()
        {
            double b = (Crafter.Control * 10 / Recipe.QualityDivider + 35);
            return Math.Floor(LevelDifference <= 0 ? b * Recipe.QualityModifier : b);
        }

        private int GetEffectiveCrafterLevel()
        {
            if (!Atlas.LevelTable.TryGetValue(Crafter.Level, out int effectiveCrafterLevel))
            {
                effectiveCrafterLevel = Crafter.Level;
            }
            return effectiveCrafterLevel;
        }

        private static double QualityFromHqPercent(double hqPercent)
        {
            return -5.6604E-6 * Math.Pow(hqPercent, 4) + 0.0015369705 * Math.Pow(hqPercent, 3) - 0.1426469573 * Math.Pow(hqPercent, 2) + 5.6122722959 * hqPercent - 5.5950384565;
        }
        public static double HqPercentFromQuality(double qualityPercent)
        {
            var hqPercent = 1;
            switch (qualityPercent)
            {
                case 0:
                    hqPercent = 1;
                    break;
                case >= 100:
                    hqPercent = 100;
                    break;
                default:
                    {
                        while (QualityFromHqPercent(hqPercent) < qualityPercent && hqPercent < 100)
                        {
                            hqPercent += 1;
                        }

                        break;
                    }
            }
            return hqPercent;
        }
    }

    public readonly struct LightState
    {
        public int Step { get; init; }
        public double Durability { get; init; }
        public double CP { get; init; }
        public double Quality { get; init; }
        public double Progress { get; init; }
        public int InnerQuiet { get; init; }
        public Dictionary<Action, int> CountDowns { get; init; }
        public bool Success(LightSimulator sim) => Progress >= sim.Recipe.Difficulty && CP >= 0;
        public StateViolations CheckViolations(LightSimulator sim)
        {
            return new StateViolations
            {
                ProgressOk = Progress >= sim.Recipe.Difficulty,
                CpOk = CP >= 0,
                DurabilityOk = Durability >= 0
            };
        }
    }
}

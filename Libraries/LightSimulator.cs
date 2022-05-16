namespace Libraries
{
    internal static class LightSimulator
    {
        public static State? Simulate(Simulator simulator, List<Action> actions, State startState, bool useDurability = true)
        {
            Recipe recipe = simulator.Recipe;
            Crafter crafter = simulator.Crafter;
            double progress = 0, quality = 0, cp = crafter.CP, durability = recipe.Durability;
            int innerQuiet = 0;
            Dictionary<Action, LightEffect> countdowns = new Dictionary<Action, LightEffect>();

            for (int i = 0; i < actions.Count; i++)
            {
                Action action = actions[i];

                #region Wasted Action Checks
                if (progress >= recipe.Difficulty && !action.Equals(Atlas.Actions.DummyAction)) return null;
                if (durability <= 0 && !action.Equals(Atlas.Actions.DummyAction)) return null;
                if (cp < 0 && !action.Equals(Atlas.Actions.DummyAction)) return null;
                if (action.Equals(Atlas.Actions.TrainedEye) && (i > 0 || simulator.PureLevelDifference < 10 || recipe.IsExpert)) return null;
                if (action.Equals(Atlas.Actions.Reflect) && i > 0) return null;
                if (action.Equals(Atlas.Actions.MuscleMemory) && i > 0) return null;
                if (action.Equals(Atlas.Actions.ByregotsBlessing) && innerQuiet == 0) return null;
                if (action.Equals(Atlas.Actions.TrainedFinesse) && innerQuiet != 10) return null;
                if ((action.Equals(Atlas.Actions.FocusedSynthesis) || action.Equals(Atlas.Actions.FocusedTouch)) && (i == 0 || !actions[i - 1].Equals(Atlas.Actions.Observe))) return null;
                if ((i > 0 && actions[i - 1].Equals(Atlas.Actions.DummyAction)) && !action.Equals(Atlas.Actions.DummyAction)) return null;
                #endregion

                #region Apply Modifiers
                int cpCost = action.CPCost;
                if (action.Equals(Atlas.Actions.StandardTouch) && i > 0 && actions[i - 1].Equals(Atlas.Actions.BasicTouch))
                {
                    cpCost = 18;
                }
                else if (action.Equals(Atlas.Actions.AdvancedTouch) && i > 1 && actions[i - 1].Equals(Atlas.Actions.StandardTouch) && actions[i - 2].Equals(Atlas.Actions.StandardTouch))
                {
                    cpCost = 18;
                }

                double progressIncreaseMultiplier = 1, baseProgressGain = 0;
                if (action.ProgressIncreaseMultiplier > 0)
                {
                    if (countdowns.ContainsKey(Atlas.Actions.MuscleMemory))
                    {
                        progressIncreaseMultiplier += 1;
                        countdowns.Remove(Atlas.Actions.MuscleMemory);
                    }
                    if (countdowns.ContainsKey(Atlas.Actions.Veneration))
                    {
                        progressIncreaseMultiplier += 0.5;
                        countdowns[Atlas.Actions.Veneration].Used = true;
                    }
                    if (action.Equals(Atlas.Actions.Groundwork) && durability < Atlas.Actions.Groundwork.DurabilityCost)
                    {
                        progressIncreaseMultiplier *= 0.5;
                    }

                    baseProgressGain = simulator.BaseProgressIncrease * action.ProgressIncreaseMultiplier * progressIncreaseMultiplier;
                }

                double qualityIncreaseMultiplier = 1, baseQualityGain = 0;
                if (action.QualityIncreaseMultiplier > 0)
                {
                    double buffMultiplier = 1;
                    if (countdowns.ContainsKey(Atlas.Actions.GreatStrides))
                    {
                        buffMultiplier += 1;
                        countdowns[Atlas.Actions.GreatStrides].Used = true;
                    }
                    if (countdowns.ContainsKey(Atlas.Actions.Innovation))
                    {
                        buffMultiplier += 0.5;
                        countdowns[Atlas.Actions.Innovation].Used = true;
                    }
                    qualityIncreaseMultiplier *= buffMultiplier;

                    qualityIncreaseMultiplier *= 1 + (0.1 * innerQuiet);
                    if (action.Equals(Atlas.Actions.ByregotsBlessing))
                    {
                        if (innerQuiet > 0)
                        {
                            qualityIncreaseMultiplier *= Math.Min(3, 1 + innerQuiet * 0.2);
                        }
                        else
                        {
                            qualityIncreaseMultiplier *= 0;
                        }
                    }

                    baseQualityGain = simulator.BaseQualityIncrease * action.QualityIncreaseMultiplier * qualityIncreaseMultiplier;
                    if (i == 0 && action.Equals(Atlas.Actions.TrainedEye) && simulator.PureLevelDifference >= 10 && !recipe.IsExpert)
                    {
                        baseQualityGain = recipe.MaxQuality;
                    }
                }

                double durabilityCost = action.DurabilityCost;
                if (durabilityCost > 0)
                {
                    bool wn = false;
                    if (countdowns.ContainsKey(Atlas.Actions.WasteNot))
                    {
                        countdowns[Atlas.Actions.WasteNot].Used = true;
                        wn = true;
                    }
                    else if (countdowns.ContainsKey(Atlas.Actions.WasteNot2))
                    {
                        countdowns[Atlas.Actions.WasteNot2].Used = true;
                        wn = true;
                    }

                    if (wn)
                    {
                        durabilityCost *= 0.5;
                    }
                }

                double progressGain = Math.Floor(baseProgressGain);
                double qualityGain = Math.Floor(baseQualityGain);
                #endregion

                #region Update State
                progress += progressGain;
                quality += qualityGain;
                durability -= useDurability ? durabilityCost : 0;
                cp -= cpCost;
                if (cp < 0) return null;
                #endregion

                #region Special Action Effects
                if (action.Equals(Atlas.Actions.MastersMend))
                {
                    durability += 30;
                }
                else if (action.Equals(Atlas.Actions.ByregotsBlessing))
                {
                    innerQuiet = 0;
                }
                else if (action.Equals(Atlas.Actions.Reflect) && i == 0)
                {
                    innerQuiet = 1;
                }

                if (action != Atlas.Actions.Manipulation && countdowns.ContainsKey(Atlas.Actions.Manipulation) && durability > 0)
                {
                    if (!countdowns[Atlas.Actions.Manipulation].Used && durability < recipe.Durability)
                    {
                        countdowns[Atlas.Actions.Manipulation].Used = true;
                    }
                    durability += 5;
                }
                if (action.QualityIncreaseMultiplier > 0 && countdowns.ContainsKey(Atlas.Actions.GreatStrides))
                {
                    countdowns.Remove(Atlas.Actions.GreatStrides);
                }
                #endregion

                #region Update Effect Counters
                foreach (Action a in countdowns.Keys.ToList())
                {
                    if (--countdowns[a].RemainingRounds == 0)
                    {
                        if (!countdowns[a].Used) return null;
                        countdowns.Remove(a);
                    }
                }

                if (action.Equals(Atlas.Actions.PreparatoryTouch))
                {
                    innerQuiet += 2;
                }
                else if (action.QualityIncreaseMultiplier > 0 && !action.Equals(Atlas.Actions.ByregotsBlessing))
                {
                    innerQuiet += 1;
                }
                innerQuiet = Math.Min(innerQuiet, 10);

                switch (action.ActionType)
                {
                    case ActionType.Immediate:
                        break;
                    case ActionType.CountDown:
                        if (countdowns.ContainsKey(action))
                        {
                            if (!countdowns[action].Used) return null;
                            countdowns[action].RemainingRounds = action.ActiveTurns;
                        }
                        else
                        {
                            countdowns.Add(action, new(action.ActiveTurns));
                        }
                        break;
                    default:
                        throw new InvalidOperationException($"Action Type {action.ActionType} ({action.Name}) was unrecognized");
                }
                #endregion

                #region Sanity Checking
                cp = Math.Min(cp, crafter.CP);
                durability = Math.Min(durability, recipe.Durability);
                #endregion
            }

            return new(simulator, actions[^1])
            {
                Step = actions.Count - 1,
                LastStep = actions.Count - 1,
                Durability = durability,
                Cp = cp,
                Quality = quality,
                Progress = progress
            };
        }
    }

    internal class LightEffect
    {
        public int RemainingRounds { get; set; }
        public bool Used { get; set; }
        
        public LightEffect(int remainingRounds)
        {
            RemainingRounds = remainingRounds;
            Used = false;
        }
    }
}

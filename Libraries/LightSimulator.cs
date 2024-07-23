using System.Numerics;

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

        public (int, LightState?) SimulateToFailure(byte[] actions, LightState? startState = null, bool useDurability = true)
        {
            ExtractState(startState, out double progress, out double quality, out double cp, out double durability, out int innerQuiet, out int step, out Dictionary<int, int> countdowns, Recipe.StartQuality, Crafter.CP, Recipe.Durability);
            int i = 0;
            foreach (var action in actions)
            {
                if (!Simulate(action, ref progress, ref quality, ref cp, ref durability, ref innerQuiet, ref step, countdowns, useDurability))
                    return (i, Simulate(actions.Take(i), startState, useDurability)); // TODO: This is a bad solution
                i++;
            }
            if (!useDurability) durability = Recipe.Durability;
            return (i, SetState(progress, quality, cp, durability, innerQuiet, step, countdowns));
        }
        public LightState? Simulate(IEnumerable<byte> actions, LightState? startState = null, bool useDurability = true)
        {
            ExtractState(startState, out double progress, out double quality, out double cp, out double durability, out int innerQuiet, out int step, out Dictionary<int, int> countdowns, Recipe.StartQuality, Crafter.CP, Recipe.Durability);
            if (actions.Any(action => !Simulate(action, ref progress, ref quality, ref cp, ref durability, ref innerQuiet, ref step, countdowns, useDurability))) return null;
            if (!useDurability) durability = Recipe.Durability;
            return SetState(progress, quality, cp, durability, innerQuiet, step, countdowns);
        }
        public LightState? Simulate(byte action, LightState? startState = null, bool useDurability = true)
        {
            ExtractState(startState, out double progress, out double quality, out double cp, out double durability, out int innerQuiet, out int step, out Dictionary<int, int> countdowns, Recipe.StartQuality, Crafter.CP, Recipe.Durability);
            if (!Simulate(action, ref progress, ref quality, ref cp, ref durability, ref innerQuiet, ref step, countdowns, useDurability)) return null;
            if (!useDurability) durability = Recipe.Durability;
            return SetState(progress, quality, cp, durability, innerQuiet, step, countdowns);
        }

        private static void ExtractState(LightState? state, out double progress, out double quality, out double cp, out double durability, out int innerQuiet, out int step, out Dictionary<int, int> countdowns, double startQuality, double startCp, double startDurability)
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
        private static LightState SetState(double progress, double quality, double cp, double durability, int innerQuiet, int step, Dictionary<int, int> countdowns)
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

        private bool Simulate(byte action, ref double progress, ref double quality, ref double cp, ref double durability, ref int innerQuiet, ref int step, Dictionary<int, int> countdowns, bool useDurability)
        {
            Action a = Atlas.Actions.AllActions[action];
            #region Wasted Action Checks
            if (progress >= Recipe.Difficulty) return false;
            if (useDurability && durability <= 0) return false;
            if (cp - a.CPCost < 0) return false;
            if (step > 0 && action is (int)Atlas.Actions.ActionMap.Reflect or (int)Atlas.Actions.ActionMap.MuscleMemory or (int)Atlas.Actions.ActionMap.TrainedEye) return false;
            switch (action)
            {
                case (int)Atlas.Actions.ActionMap.TrainedEye when PureLevelDifference < 10 || Recipe.IsExpert:
                case (int)Atlas.Actions.ActionMap.ByregotsBlessing when innerQuiet == 0:
                case (int)Atlas.Actions.ActionMap.TrainedFinesse when innerQuiet < 10:
                case (int)Atlas.Actions.ActionMap.PrudentTouch or (int)Atlas.Actions.ActionMap.PrudentSynthesis when countdowns.ContainsKey((int)Atlas.Actions.ActionMap.WasteNot) || countdowns.ContainsKey((int)Atlas.Actions.ActionMap.WasteNot2):
                    return false;
            }
            #endregion

            #region Multipliers
            double progressIncreaseMultiplier = 0;
            if (a.ProgressIncreaseMultiplier > 0)
            {

                progressIncreaseMultiplier = 1;
                if (a.ProgressIncreaseMultiplier > 0 && countdowns.ContainsKey((int)Atlas.Actions.ActionMap.MuscleMemory))
                {
                    progressIncreaseMultiplier += 1;
                    countdowns.Remove((int)Atlas.Actions.ActionMap.MuscleMemory);
                }

                if (countdowns.ContainsKey((int)Atlas.Actions.ActionMap.Veneration))
                {
                    progressIncreaseMultiplier += 0.5;
                    if (countdowns[(int)Atlas.Actions.ActionMap.Veneration] > 0) countdowns[(int)Atlas.Actions.ActionMap.Veneration] *= -1;
                }

                if (action == (int)Atlas.Actions.ActionMap.Groundwork && (useDurability ? durability : Recipe.Durability) < Atlas.Actions.AllActions[(int)Atlas.Actions.ActionMap.Groundwork].DurabilityCost)
                {
                    progressIncreaseMultiplier *= 0.5;
                }
            }

            double qualityIncreaseMultiplier = 0;
            if (a.QualityIncreaseMultiplier > 0)
            {

                qualityIncreaseMultiplier = 1;
                if (countdowns.ContainsKey((int)Atlas.Actions.ActionMap.GreatStrides))
                {
                    qualityIncreaseMultiplier += 1;
                    countdowns.Remove((int)Atlas.Actions.ActionMap.GreatStrides);
                }

                if (countdowns.ContainsKey((int)Atlas.Actions.ActionMap.Innovation))
                {
                    qualityIncreaseMultiplier += 0.5;
                    if (countdowns[(int)Atlas.Actions.ActionMap.Innovation] > 0) countdowns[(int)Atlas.Actions.ActionMap.Innovation] *= -1;
                }

                if (action == (int)Atlas.Actions.ActionMap.ByregotsBlessing)
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
            }
            #endregion

            int cpCost = a.CPCost;
            progress += Math.Floor(BaseProgressIncrease * a.ProgressIncreaseMultiplier * progressIncreaseMultiplier);
            quality += Math.Floor(action == (int)Atlas.Actions.ActionMap.TrainedEye ? Recipe.MaxQuality : BaseQualityIncrease * a.QualityIncreaseMultiplier * qualityIncreaseMultiplier);

            #region Combos
            switch (action)
            {
                case (int)Atlas.Actions.ActionMap.StandardTouch when countdowns.ContainsKey((int)Atlas.Actions.ActionMap.BasicTouch) && countdowns[(int)Atlas.Actions.ActionMap.BasicTouch] == 1:
                case (int)Atlas.Actions.ActionMap.AdvancedTouch when countdowns.ContainsKey((int)Atlas.Actions.ActionMap.StandardTouch) && countdowns[(int)Atlas.Actions.ActionMap.StandardTouch] == 1:
                case (int)Atlas.Actions.ActionMap.AdvancedTouch when countdowns.ContainsKey((int)Atlas.Actions.ActionMap.Observe) && countdowns[(int)Atlas.Actions.ActionMap.Observe] == 1:
                    cpCost = 18;
                    break;
            }
            #endregion

            #region Durability
            double durabilityCost = a.DurabilityCost;
            if (durabilityCost > 0 && countdowns.ContainsKey((int)Atlas.Actions.ActionMap.TrainedPerfection) && countdowns[(int)Atlas.Actions.ActionMap.TrainedPerfection] > 0)
            {
                durabilityCost = 0;
                countdowns[(int)Atlas.Actions.ActionMap.TrainedPerfection] *= -1;
            }

            #region Waste Not
            bool wn = false;
            if (countdowns.ContainsKey((int)Atlas.Actions.ActionMap.WasteNot))
            {
                wn = true;
                if (a.DurabilityCost > 0 && countdowns[(int)Atlas.Actions.ActionMap.WasteNot] > 0) countdowns[(int)Atlas.Actions.ActionMap.WasteNot] *= -1;
            }
            else if (countdowns.ContainsKey((int)Atlas.Actions.ActionMap.WasteNot2))
            {
                wn = true;
                if (a.DurabilityCost > 0 && countdowns[(int)Atlas.Actions.ActionMap.WasteNot2] > 0) countdowns[(int)Atlas.Actions.ActionMap.WasteNot2] *= -1;
            }

            if (wn)
            {
                durabilityCost *= 0.5;
            }
            #endregion

            durability -= durabilityCost;

            #region Durability Restoration
            if (action == (int)Atlas.Actions.ActionMap.MastersMend)
            {
                if (Math.Abs(durability - Recipe.Durability) < 0.9) return false;
                durability += 30;
            }
            
            if (countdowns.ContainsKey((int)Atlas.Actions.ActionMap.Manipulation) && durability > 0 && action != (int)Atlas.Actions.ActionMap.Manipulation)
            {
                if (durability < Recipe.Durability && countdowns[(int)Atlas.Actions.ActionMap.Manipulation] > 0) countdowns[(int)Atlas.Actions.ActionMap.Manipulation] *= -1;
                durability += 5;
            }

            if (action == (int)Atlas.Actions.ActionMap.ImmaculateMend)
            {
                if (Recipe.Durability - durability <= 30) return false; // just use Masters Mend
                durability += Recipe.Durability;
            }
            #endregion

            durability = Math.Min(durability, Recipe.Durability);
            #endregion

            #region Inner Quiet
            switch (action)
            {
                case (int)Atlas.Actions.ActionMap.ByregotsBlessing:
                    innerQuiet = 0;
                    break;
                case (int)Atlas.Actions.ActionMap.Reflect:
                    innerQuiet = 2;
                    break;
                case (int)Atlas.Actions.ActionMap.PreparatoryTouch:
                    innerQuiet += 2;
                    break;
                case (int)Atlas.Actions.ActionMap.RefinedTouch when countdowns.ContainsKey((int)Atlas.Actions.ActionMap.BasicTouch) && countdowns[(int)Atlas.Actions.ActionMap.BasicTouch] == 1:
                    innerQuiet += 2;
                    break;
                default:
                {
                    if (a.QualityIncreaseMultiplier > 0) innerQuiet += 1;
                    break;
                }
            }
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

            int turns = a.ActiveTurns;
            if (action == (int)Atlas.Actions.ActionMap.BasicTouch)
            {
                turns = -1;
            }
            if (a.ActionType == ActionType.CountDown)
            {
                if (!countdowns.TryAdd(action, turns))
                    if (countdowns[action] > 0) return false;
                    else if (action == (int)Atlas.Actions.ActionMap.TrainedPerfection) return false;
                    else countdowns[action] = turns;
            }
            #endregion

            step      += 1;
            innerQuiet = Math.Min(innerQuiet, 10);
            cp         = Math.Min(cp - cpCost, Crafter.CP);

            return true;
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
        public Dictionary<int, int> CountDowns { get; init; }
        public bool Success(LightSimulator sim) => Progress >= sim.Recipe.Difficulty && CP >= 0;

        public ulong ToULong(out int ix) // (max) 56 bits used
        {
            ix = 0;
            ulong b = 0;
            
            ix += Encode(ref b, (int)CP, ix);
            ix += Encode(ref b, (int)Durability / 5, ix);
            ix += Encode(ref b, (int)Quality, ix);
            ix += Encode(ref b, (int)Progress, ix);

            return b;
        }
        public uint EffectsToUInt() // 22 bits used
        {
            uint b = 0;
            foreach (var countdown in CountDowns)
            {
                switch (countdown.Key)
                {
                    case (int)Atlas.Actions.ActionMap.Observe:
                        b |= 1 << 21;
                        break;
                    case (int)Atlas.Actions.ActionMap.BasicTouch:
                        b |= 1 << 20;
                        break;
                    case (int)Atlas.Actions.ActionMap.StandardTouch:
                        b |= 1 << 19;
                        break;
                    case (int)Atlas.Actions.ActionMap.GreatStrides:
                        b |= 1 << 18;
                        break;
                    case (int)Atlas.Actions.ActionMap.Manipulation:
                        b |= EncodeEffect(countdown.Value, 14, 4);
                        break;
                    case (int)Atlas.Actions.ActionMap.WasteNot:
                        b |= EncodeEffect(countdown.Value, 10, 4);
                        break;
                    case (int)Atlas.Actions.ActionMap.WasteNot2:
                        b |= EncodeEffect(countdown.Value, 10, 4);
                        break;
                    case (int)Atlas.Actions.ActionMap.Veneration:
                        b |= EncodeEffect(countdown.Value, 7, 3);
                        break;
                    case (int)Atlas.Actions.ActionMap.Innovation:
                        b |= EncodeEffect(countdown.Value, 4, 3);
                        break;
                    case (int)Atlas.Actions.ActionMap.MuscleMemory:
                        b |= EncodeEffect(countdown.Value, 0, 4);
                        break;
                }
            }
            return b;
        }

        private int Encode(ref ulong b, int value, int shift)
        {
            uint aValue = (uint)Math.Abs(value);
            int size = 32 - BitOperations.LeadingZeroCount(aValue);
            if (value < 0)
            {
                aValue |= (uint)1 << size++;
            }
            b |= (((ulong)size << size) | aValue) << shift;
            return size + 4;
        }

        private uint EncodeEffect(int value, int shift, int size) => (uint)(Math.Abs(value) | (value < 0 ? 1 << (size - 5) : 0)) << shift;

        public BigInteger ToBigInt()
        {
            ulong state = ToULong(out int stateBits);
            return new BigInteger(EffectsToUInt()) << stateBits | state;
        }
    }
}

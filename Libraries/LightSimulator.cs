namespace Libraries
{
    public class LightSimulator
    {
        public Crafter Crafter { get; }
        public Recipe Recipe { get; }

        private int EffectiveCrafterLevel { get; }
        private int LevelDifference { get; }
        private int PureLevelDifference { get; }
        private double BaseProgressIncrease { get; }
        private double BaseQualityIncrease { get; }

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

        public LightState SimulateToFailure(byte[] actions, LightState startState, bool useDurability = true)
        {
            LightState state = startState;
            foreach (var action in actions)
            {
                LightState prevState = state;
                if (!Simulate(action, ref state, useDurability)) return prevState;
            }
            if (!useDurability) state.Durability = Recipe.Durability;
            return state;
        }

        public LightState SimulateToFailure(byte[] actions, bool useDurability = true)
        {
            LightState state = new LightState(Recipe.StartQuality, Crafter.CP, Recipe.Durability);
            foreach (var action in actions)
            {
                LightState prevState = state;
                if (!Simulate(action, ref state, useDurability)) return prevState;
            }
            if (!useDurability) state.Durability = Recipe.Durability;
            return state;
        }

        public LightState Simulate(IEnumerable<byte> actions, LightState startState, bool useDurability = true)
        {
            LightState state = startState;
            if (actions.Any(action => !Simulate(action, ref state, useDurability)))
            {
                return new LightState { IsError = true };
            }
            if (!useDurability) state.Durability = Recipe.Durability;
            return state;
        }
        public LightState Simulate(byte action, LightState startState, bool useDurability = true)
        {
            LightState state = startState;
            if (!Simulate(action, ref state, useDurability)) return new LightState { IsError = true };
            if (!useDurability) state.Durability = Recipe.Durability;
            return state;
        }
        public LightState Simulate(IEnumerable<byte> actions, bool useDurability = true)
        {
            LightState state = new LightState(Recipe.StartQuality, Crafter.CP, Recipe.Durability);
            if (actions.Any(action => !Simulate(action, ref state, useDurability)))
            {
                return new LightState { IsError = true };
            }
            if (!useDurability) state.Durability = Recipe.Durability;
            return state;
        }
        public LightState Simulate(byte action, bool useDurability = true)
        {
            LightState state = new LightState(Recipe.StartQuality, Crafter.CP, Recipe.Durability);
            if (!Simulate(action, ref state, useDurability)) return new LightState { IsError = true };
            if (!useDurability) state.Durability = Recipe.Durability;
            return state;
        }
        private bool Simulate(byte action, ref LightState state, bool useDurability = true)
        {
            Action a = Atlas.Actions.AllActions[action];
            #region Wasted Action Checks
            if (state.Progress >= Recipe.Difficulty) return false;
            if (useDurability && state.Durability <= 0) return false;
            if (state.CP - a.CPCost < 0) return false;
            if (state.Step > 0 && action is (int)Atlas.Actions.ActionMap.Reflect or (int)Atlas.Actions.ActionMap.MuscleMemory or (int)Atlas.Actions.ActionMap.TrainedEye) return false;
            switch (action)
            {
                case (int)Atlas.Actions.ActionMap.TrainedEye when PureLevelDifference < 10 || Recipe.IsExpert:
                case (int)Atlas.Actions.ActionMap.ByregotsBlessing when state.InnerQuiet == 0:
                case (int)Atlas.Actions.ActionMap.TrainedFinesse when state.InnerQuiet < 10:
                case (int)Atlas.Actions.ActionMap.PrudentTouch or (int)Atlas.Actions.ActionMap.PrudentSynthesis when state.WasteNotActive:
                    return false;
            }
            #endregion

            #region Multipliers
            double progressIncreaseMultiplier = 0;
            if (a.ProgressIncreaseMultiplier > 0)
            {

                progressIncreaseMultiplier = 1;
                if (a.ProgressIncreaseMultiplier > 0 && state.MuscleMemoryActive)
                {
                    progressIncreaseMultiplier += 1;
                    state.MuscleMemoryDuration = 0;
                }

                if (state.VenerationActive)
                {
                    progressIncreaseMultiplier += 0.5;
                    state.VenerationUsed = true;
                }

                if (action == (int)Atlas.Actions.ActionMap.Groundwork && (useDurability ? state.Durability : Recipe.Durability) < Atlas.Actions.AllActions[(int)Atlas.Actions.ActionMap.Groundwork].DurabilityCost)
                {
                    progressIncreaseMultiplier *= 0.5;
                }
            }

            double qualityIncreaseMultiplier = 0;
            if (a.QualityIncreaseMultiplier > 0)
            {

                qualityIncreaseMultiplier = 1;
                if (state.GreatStridesActive)
                {
                    qualityIncreaseMultiplier += 1;
                    state.GreatStridesDuration = 0;
                }

                if (state.InnovationActive)
                {
                    qualityIncreaseMultiplier += 0.5;
                    state.InnovationUsed = true;
                }

                if (action == (int)Atlas.Actions.ActionMap.ByregotsBlessing)
                {
                    if (state.InnerQuiet > 0)
                    {
                        qualityIncreaseMultiplier *= Math.Min(3, 1 + state.InnerQuiet * 0.2);
                    }
                    else
                    {
                        qualityIncreaseMultiplier = 0;
                    }
                }

                qualityIncreaseMultiplier *= 1 + (0.1 * state.InnerQuiet);
            }
            #endregion

            int cpCost = a.CPCost;
            state.Progress += Math.Floor(BaseProgressIncrease * a.ProgressIncreaseMultiplier * progressIncreaseMultiplier);
            state.Quality += Math.Floor(action == (int)Atlas.Actions.ActionMap.TrainedEye ? Recipe.MaxQuality : BaseQualityIncrease * a.QualityIncreaseMultiplier * qualityIncreaseMultiplier);

            #region Combos
            switch (action)
            {
                case (int)Atlas.Actions.ActionMap.StandardTouch when state.BasicTouchActive:
                case (int)Atlas.Actions.ActionMap.AdvancedTouch when state.StandardTouchActive:
                    cpCost = 18;
                    break;
                case (int)Atlas.Actions.ActionMap.AdvancedTouch when state.ObserveActive:
                    cpCost = 18;
                    state.ObserveUsed = true;
                    break;
            }
            #endregion

            #region Durability
            double durabilityCost = a.DurabilityCost;
            if (durabilityCost > 0 && state.TrainedPerfectionActive)
            {
                durabilityCost = 0;
                state.TrainedPerfectionUsed = true;
                state.TrainedPerfectionActive = false;
            }

            if (state.WasteNotActive && a.DurabilityCost > 0)
            {
                state.WasteNotUsed = true;
                durabilityCost *= 0.5;
            }
            
            state.Durability -= durabilityCost;

            #region Durability Restoration
            if (action == (int)Atlas.Actions.ActionMap.MastersMend)
            {
                if (Math.Abs(state.Durability - Recipe.Durability) < 0.9) return false;
                state.Durability += 30;
            }
            
            if (state is { ManipulationActive: true, Durability: > 0 } && action != (int)Atlas.Actions.ActionMap.Manipulation)
            {
                if (state.Durability < Recipe.Durability) state.ManipulationUsed = true;
                state.Durability += 5;
            }

            if (action == (int)Atlas.Actions.ActionMap.ImmaculateMend)
            {
                if (Recipe.Durability - state.Durability <= 30) return false;
                state.Durability += Recipe.Durability;
            }
            #endregion

            state.Durability = Math.Min(state.Durability, Recipe.Durability);
            #endregion

            #region Inner Quiet
            switch (action)
            {
                case (int)Atlas.Actions.ActionMap.ByregotsBlessing:
                    state.InnerQuiet = 0;
                    break;
                case (int)Atlas.Actions.ActionMap.Reflect:
                    state.InnerQuiet = 2;
                    break;
                case (int)Atlas.Actions.ActionMap.PreparatoryTouch:
                    state.InnerQuiet += 2;
                    break;
                case (int)Atlas.Actions.ActionMap.RefinedTouch when state.BasicTouchActive:
                    state.InnerQuiet += 2;
                    break;
                default:
                {
                    if (a.QualityIncreaseMultiplier > 0) state.InnerQuiet += 1;
                    break;
                }
            }
            #endregion

            #region Countdowns
            state.DecrementBuffs();

            if (a.ActionType == ActionType.CountDown) state.SetBuff(action, a.ActiveTurns);
            #endregion

            state.Step      += 1;
            state.InnerQuiet = Math.Min(state.InnerQuiet, 10);
            state.CP         = Math.Min(state.CP - cpCost, Crafter.CP);

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

    public struct LightState
    {
        public bool IsError { get; init; }
        
        public int Step { get; set; }
        public double Durability { get; set; }
        public double CP { get; set; }
        public double Quality { get; set; }
        public double Progress { get; set; }
        
        #region Buffs
        public int InnerQuiet { get; set; }

        public bool WasteNotActive => WasteNotDuration > 0;
        public byte WasteNotDuration { get; set; }
        public bool WasteNotUsed { get; set; }

        public bool MuscleMemoryActive => MuscleMemoryDuration > 0;
        public byte MuscleMemoryDuration { get; set; }

        public bool VenerationActive => VenerationDuration > 0;
        public byte VenerationDuration { get; set; }
        public bool VenerationUsed { get; set; }

        public bool GreatStridesActive => GreatStridesDuration > 0;
        public byte GreatStridesDuration { get; set; }

        public bool InnovationActive => InnovationDuration > 0;
        public byte InnovationDuration { get; set; }
        public bool InnovationUsed { get; set; }

        public bool TrainedPerfectionActive { get; set; }
        public bool TrainedPerfectionUsed { get; set; }

        public bool ManipulationActive => ManipulationDuration > 0;
        public byte ManipulationDuration { get; set; }
        public bool ManipulationUsed { get; set; }
        
        public bool ObserveActive { get; set; }
        public bool ObserveUsed { get; set; }
        
        public bool BasicTouchActive { get; set; }
        public bool StandardTouchActive { get; set; }

        public bool DecrementBuffs()
        {
            if (WasteNotActive && --WasteNotDuration == 0 && !WasteNotUsed) return false;
            if (VenerationActive && --VenerationDuration == 0 && !VenerationUsed) return false;
            if (InnovationActive && --InnovationDuration == 0 && !InnovationUsed) return false;
            if (ManipulationActive && --ManipulationDuration == 0 && !ManipulationUsed) return false;
            
            if (MuscleMemoryActive && --MuscleMemoryDuration == 0) return false;
            if (GreatStridesActive && --GreatStridesDuration == 0) return false;

            if (ObserveActive)
            {
                ObserveActive = false;
                if (!ObserveUsed) return false;
            }
            
            return true;
        }
        public bool SetBuff(byte action, int duration)
        {
            switch (action)
            {
                case (byte)Atlas.Actions.ActionMap.WasteNot:
                case (byte)Atlas.Actions.ActionMap.WasteNot2:
                    if (WasteNotActive && !WasteNotUsed) return false;
                    WasteNotDuration = (byte)duration;
                    WasteNotUsed = false;
                    break;
                case (byte)Atlas.Actions.ActionMap.MuscleMemory:
                    if (MuscleMemoryActive) return false;
                    MuscleMemoryDuration = (byte)duration;
                    break;
                case (byte)Atlas.Actions.ActionMap.Veneration:
                    if (VenerationActive && !VenerationUsed) return false;
                    VenerationDuration = (byte)duration;
                    VenerationUsed = false;
                    break;
                case (byte)Atlas.Actions.ActionMap.GreatStrides:
                    if (GreatStridesActive) return false;
                    GreatStridesDuration = (byte)duration;
                    break;
                case (byte)Atlas.Actions.ActionMap.Innovation:
                    if (InnovationActive && !InnovationUsed) return false;
                    InnovationDuration = (byte)duration;
                    InnovationUsed = false;
                    break;
                case (byte)Atlas.Actions.ActionMap.TrainedPerfection:
                    if (TrainedPerfectionActive || TrainedPerfectionUsed) return false;
                    TrainedPerfectionActive = true;
                    break;
                case (byte)Atlas.Actions.ActionMap.Manipulation:
                    if (ManipulationActive && !ManipulationUsed) return false;
                    ManipulationDuration = (byte)duration;
                    ManipulationUsed = false;
                    break;
                case (byte)Atlas.Actions.ActionMap.Observe:
                    if (ObserveActive) return false;
                    ObserveActive = true;
                    ObserveUsed = false;
                    break;
                case (byte)Atlas.Actions.ActionMap.BasicTouch:
                    BasicTouchActive = true;
                    break;
                case (byte)Atlas.Actions.ActionMap.StandardTouch when BasicTouchActive:
                    StandardTouchActive = true;
                    break;
            }
            return true;
        }
        #endregion

        public LightState(double startQuality, double startCp, double startDurability)
        {
            IsError = false;
            
            Progress = 0;
            Quality = startQuality;
            CP = startCp;
            Durability = startDurability;
            InnerQuiet = 0;
            Step = 0;

            InnerQuiet = 0;
            WasteNotDuration = 0;
            WasteNotUsed = false;
            MuscleMemoryDuration = 0;
            VenerationDuration = 0;
            VenerationUsed = false;
            GreatStridesDuration = 0;
            InnovationDuration = 0;
            InnovationUsed = false;
            TrainedPerfectionActive = false;
            TrainedPerfectionUsed = false;
            ManipulationDuration = 0;
            ManipulationUsed = false;
            ObserveActive = false;
            ObserveUsed = false;
            BasicTouchActive = false;
            StandardTouchActive = false;
        }
        
        public bool Success(LightSimulator sim) => Progress >= sim.Recipe.Difficulty && CP >= 0;
    }
}

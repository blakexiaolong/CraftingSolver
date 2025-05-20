namespace Libraries;

public class NanoSimulator
{
    public Crafter Crafter { get; }
    public Recipe Recipe { get; }

    private int EffectiveCrafterLevel { get; }
    private int LevelDifference { get; }
    private int PureLevelDifference { get; }
    private double BaseProgressIncrease { get; }
    private double BaseQualityIncrease { get; }
    
    public NanoSimulator(Crafter crafter, Recipe recipe)
    {
        Crafter = crafter;
        Recipe = recipe;

        EffectiveCrafterLevel = Atlas.LevelTable.TryGetValue(Crafter.Level, out int effectiveCrafterLevel) ? effectiveCrafterLevel : Crafter.Level;
        LevelDifference = Math.Min(49, Math.Max(-30, EffectiveCrafterLevel - Recipe.RLevel));
        PureLevelDifference = Crafter.Level - Recipe.Level;
        
        // ReSharper disable once PossibleLossOfFraction
        BaseProgressIncrease = Math.Floor((Crafter.Craftsmanship * 10 / Recipe.ProgressDivider + 2) * (LevelDifference <= 0 ? Recipe.ProgressModifier : 1));
        // ReSharper disable once PossibleLossOfFraction
        BaseQualityIncrease = Math.Floor((Crafter.Control * 10 / Recipe.QualityDivider + 35) * (LevelDifference <= 0 ? Recipe.QualityModifier : 1));
    }
    
    public LightState SimulateToFailure(byte[] actions)
    {
        LightState state = new LightState(Recipe.StartQuality, Crafter.CP, Recipe.Durability);
        foreach (var action in actions)
        {
            LightState prevState = state;
            if (!Simulate(action, ref state)) return prevState;
        }
        return state;
    }
    public LightState SimulateToFailure(byte[] actions, int take, LightState startState)
    {
        LightState state = startState;
        for (int i = 0; i < take; i++)
        {
            LightState prevState = state;
            if (!Simulate(actions[i], ref state)) return prevState;
        }
        return state;
    }
    public LightState Simulate(byte action, LightState startState)
    {
        LightState state = startState;
        if (!Simulate(action, ref state))
        {
            return new LightState { IsError = true };
        }
        return state;
    }
    public LightState Simulate(IEnumerable<byte> actions)
    {
        LightState state = new LightState(Recipe.StartQuality, Crafter.CP, Recipe.Durability);
        if (actions.Any(action => !Simulate(action, ref state)))
        {
            return new LightState { IsError = true };
        }
        return state;
    }

    private bool Simulate(byte action, ref LightState state)
    {
        Action a = Atlas.Actions.AllActions[action];

        #region Before Action
        if (state.Durability <= 0) return false;
        if (state.Progress >= Recipe.Difficulty) return false;
        #endregion

        double multiplier = 1;
        switch (action)
        {
            case (int)Atlas.Actions.ActionMap.Groundwork:
                if (state.CP - a.CPCost < 0) return false;
                if (state.MuscleMemoryActive)
                {
                    multiplier += 1;
                    state.MuscleMemoryDuration = 0;
                }
                if (state.VenerationActive)
                {
                    multiplier += 0.5;
                    state.VenerationUsed = true;
                }
                if (state.Durability < 20)
                {
                    multiplier /= 2;
                }
                state.Progress += Math.Floor(BaseProgressIncrease * a.ProgressIncreaseMultiplier * multiplier);
                state.CP -= a.CPCost;
                if (state.TrainedPerfectionActive)
                {
                    state.TrainedPerfectionUsed = true;
                    state.TrainedPerfectionActive = false;
                }
                else if (state.WasteNotActive)
                {
                    state.WasteNotUsed = true;
                    state.Durability -= (short)(a.DurabilityCost / 2);
                }
                else
                {
                    state.Durability -= a.DurabilityCost;
                }
                break;
            case (int)Atlas.Actions.ActionMap.PreparatoryTouch:
                if (state.CP - a.CPCost < 0) return false;
                if (state.GreatStridesActive)
                {
                    multiplier += 1;
                    state.GreatStridesDuration = 0;
                }
                if (state.InnovationActive)
                {
                    multiplier += 0.5;
                    state.InnovationUsed = true;
                }
                multiplier *= 1 + 0.1 * state.InnerQuiet;
                state.Quality += Math.Floor(BaseQualityIncrease * a.QualityIncreaseMultiplier * multiplier);
                state.CP -= a.CPCost;
                if (state.TrainedPerfectionActive)
                {
                    state.TrainedPerfectionUsed = true;
                    state.TrainedPerfectionActive = false;
                }
                else if (state.WasteNotActive)
                {
                    state.WasteNotUsed = true;
                    state.Durability -= (short)(a.DurabilityCost / 2);
                }
                else state.Durability -= a.DurabilityCost;

                state.InnerQuiet = (byte)Math.Min(state.InnerQuiet + 2, 10);
                break;
            case (int)Atlas.Actions.ActionMap.Veneration:
                if (state.CP - a.CPCost < 0) return false;
                if (state.VenerationActive && !state.VenerationUsed) return false;
                state.CP -= a.CPCost;
                break;
            case (int)Atlas.Actions.ActionMap.Innovation:
                if (state.CP - a.CPCost < 0) return false;
                if (state.InnovationActive && !state.InnovationUsed) return false;
                state.CP -= a.CPCost;
                break;
            case (int)Atlas.Actions.ActionMap.GreatStrides:
                if (state.CP - a.CPCost < 0) return false;
                if (state.GreatStridesActive) return false;
                state.CP -= a.CPCost;
                break;
            case (int)Atlas.Actions.ActionMap.BasicSynth:
                if (state.CP - a.CPCost < 0) return false;
                if (state.MuscleMemoryActive)
                {
                    multiplier += 1;
                    state.MuscleMemoryDuration = 0;
                }
                if (state.VenerationActive)
                {
                    multiplier += 0.5;
                    state.VenerationUsed = true;
                }
                state.Progress += Math.Floor(BaseProgressIncrease * a.ProgressIncreaseMultiplier * multiplier);
                state.CP -= a.CPCost;
                if (state.TrainedPerfectionActive)
                {
                    state.TrainedPerfectionUsed = true;
                    state.TrainedPerfectionActive = false;
                }
                else if (state.WasteNotActive)
                {
                    state.WasteNotUsed = true;
                    state.Durability -= (short)(a.DurabilityCost / 2);
                }
                else state.Durability -= a.DurabilityCost;
                break;
            case (int)Atlas.Actions.ActionMap.CarefulSynthesis:
                if (state.CP - a.CPCost < 0) return false;
                if (state.MuscleMemoryActive)
                {
                    multiplier += 1;
                    state.MuscleMemoryDuration = 0;
                }
                if (state.VenerationActive)
                {
                    multiplier += 0.5;
                    state.VenerationUsed = true;
                }
                state.Progress += Math.Floor(BaseProgressIncrease * a.ProgressIncreaseMultiplier * multiplier);
                state.CP -= a.CPCost;
                if (state.TrainedPerfectionActive)
                {
                    state.TrainedPerfectionUsed = true;
                    state.TrainedPerfectionActive = false;
                }
                else if (state.WasteNotActive)
                {
                    state.WasteNotUsed = true;
                    state.Durability -= (short)(a.DurabilityCost / 2);
                }
                else state.Durability -= a.DurabilityCost;
                break;
            case (int)Atlas.Actions.ActionMap.BasicTouch:
                if (state.CP - a.CPCost < 0) return false;
                if (state.BasicTouchActive || state.StandardTouchActive || state.ObserveActive) return false;
                if (state.GreatStridesActive)
                {
                    multiplier += 1;
                    state.GreatStridesDuration = 0;
                }
                if (state.InnovationActive)
                {
                    multiplier += 0.5;
                    state.InnovationUsed = true;
                }
                multiplier *= 1 + 0.1 * state.InnerQuiet;
                state.Quality += Math.Floor(BaseQualityIncrease * a.QualityIncreaseMultiplier * multiplier);
                state.CP -= a.CPCost;
                if (state.TrainedPerfectionActive)
                {
                    state.TrainedPerfectionUsed = true;
                    state.TrainedPerfectionActive = false;
                }
                else if (state.WasteNotActive)
                {
                    state.WasteNotUsed = true;
                    state.Durability -= (short)(a.DurabilityCost / 2);
                }
                else state.Durability -= a.DurabilityCost;
                state.InnerQuiet = (byte)Math.Min(state.InnerQuiet + 1, 10);
                break;
            case (int)Atlas.Actions.ActionMap.StandardTouch:
                if (state.CP - a.CPCost < 0) return false;
                if (state.StandardTouchActive || state.ObserveActive) return false;
                if (state.GreatStridesActive)
                {
                    multiplier += 1;
                    state.GreatStridesDuration = 0;
                }
                if (state.InnovationActive)
                {
                    multiplier += 0.5;
                    state.InnovationUsed = true;
                }
                multiplier *= 1 + 0.1 * state.InnerQuiet;
                state.Quality += Math.Floor(BaseQualityIncrease * a.QualityIncreaseMultiplier * multiplier);
                state.CP -= state.BasicTouchActive ? 18 : a.CPCost;
                if (state.TrainedPerfectionActive)
                {
                    state.TrainedPerfectionUsed = true;
                    state.TrainedPerfectionActive = false;
                }
                else if (state.WasteNotActive)
                {
                    state.WasteNotUsed = true;
                    state.Durability -= (short)(a.DurabilityCost / 2);
                }
                else state.Durability -= a.DurabilityCost;
                state.InnerQuiet = (byte)Math.Min(state.InnerQuiet + 1, 10);
                break;
            case (int)Atlas.Actions.ActionMap.AdvancedTouch:
                if (state.CP - a.CPCost < 0) return false;
                if (state.GreatStridesActive)
                {
                    multiplier += 1;
                    state.GreatStridesDuration = 0;
                }
                if (state.InnovationActive)
                {
                    multiplier += 0.5;
                    state.InnovationUsed = true;
                }
                multiplier *= 1 + 0.1 * state.InnerQuiet;
                state.Quality += Math.Floor(BaseQualityIncrease * a.QualityIncreaseMultiplier * multiplier);
                if (state.ObserveActive)
                {
                    state.CP -= 18;
                    state.ObserveUsed = true;
                }
                else
                {
                    state.CP -= state.StandardTouchActive ? 18 : a.CPCost;
                }
                if (state.TrainedPerfectionActive)
                {
                    state.TrainedPerfectionUsed = true;
                    state.TrainedPerfectionActive = false;
                }
                else if (state.WasteNotActive)
                {
                    state.WasteNotUsed = true;
                    state.Durability -= (short)(a.DurabilityCost / 2);
                }
                else state.Durability -= a.DurabilityCost;
                state.InnerQuiet = (byte)Math.Min(state.InnerQuiet + 1, 10);
                break;
            case (int)Atlas.Actions.ActionMap.RefinedTouch:
                if (state.CP - a.CPCost < 0) return false;
                if (state.GreatStridesActive)
                {
                    multiplier += 1;
                    state.GreatStridesDuration = 0;
                }
                if (state.InnovationActive)
                {
                    multiplier += 0.5;
                    state.InnovationUsed = true;
                }
                multiplier *= 1 + 0.1 * state.InnerQuiet;
                state.Quality += Math.Floor(BaseQualityIncrease * a.QualityIncreaseMultiplier * multiplier);
                state.CP -= a.CPCost;
                if (state.TrainedPerfectionActive)
                {
                    state.TrainedPerfectionUsed = true;
                    state.TrainedPerfectionActive = false;
                }
                else if (state.WasteNotActive)
                {
                    state.WasteNotUsed = true;
                    state.Durability -= (short)(a.DurabilityCost / 2);
                }
                else state.Durability -= a.DurabilityCost;

                if (state.BasicTouchActive)
                {
                    state.InnerQuiet = (byte)Math.Min(state.InnerQuiet + 2, 10);
                }
                else
                {
                    state.InnerQuiet = (byte)Math.Min(state.InnerQuiet + 1, 10);
                }
                break;
            case (int)Atlas.Actions.ActionMap.ByregotsBlessing:
                if (state.CP - a.CPCost < 0) return false;
                byte iq = state.InnerQuiet;
                if (iq < 5) return false;
                if (state.GreatStridesActive)
                {
                    multiplier += 1;
                    state.GreatStridesDuration = 0;
                }
                if (state.InnovationActive)
                {
                    multiplier += 0.5;
                    state.InnovationUsed = true;
                }
                multiplier *= Math.Min(3, 1 + iq * 0.2) * (1 + 0.1 * iq);
                state.Quality += Math.Floor(BaseQualityIncrease * a.QualityIncreaseMultiplier * multiplier);
                state.CP -= a.CPCost;
                if (state.TrainedPerfectionActive)
                {
                    state.TrainedPerfectionUsed = true;
                    state.TrainedPerfectionActive = false;
                }
                else if (state.WasteNotActive)
                {
                    state.WasteNotUsed = true;
                    state.Durability -= (short)(a.DurabilityCost / 2);
                }
                else state.Durability -= a.DurabilityCost;
                state.InnerQuiet = 0;
                break;
            case (int)Atlas.Actions.ActionMap.ImmaculateMend:
                if (state.CP - a.CPCost < 0) return false;
                if (Recipe.Durability - state.Durability <= 30) return false;
                state.CP -= a.CPCost;
                state.Durability = Recipe.Durability;
                break;
            case (int)Atlas.Actions.ActionMap.MastersMend:
                if (state.CP - a.CPCost < 0) return false;
                if (Math.Abs(state.Durability - Recipe.Durability) < 0.9) return false;
                state.CP -= a.CPCost;
                state.Durability = (short)Math.Min(state.Durability + 30, Recipe.Durability);
                break;
            case (int)Atlas.Actions.ActionMap.Manipulation:
                if (state.CP - a.CPCost < 0) return false;
                if (state.ManipulationActive && !state.ManipulationUsed) return false;
                state.CP -= a.CPCost;
                break;
            case (int)Atlas.Actions.ActionMap.WasteNot:
            case (int)Atlas.Actions.ActionMap.WasteNot2:
                if (state.CP - a.CPCost < 0) return false;
                if (state.WasteNotActive && !state.WasteNotUsed) return false;
                state.CP -= a.CPCost;
                break;
            case (int)Atlas.Actions.ActionMap.PrudentTouch:
                if (state.WasteNotActive || state.TrainedPerfectionActive) return false;
                if (state.CP - a.CPCost < 0) return false;
                if (state.GreatStridesActive)
                {
                    multiplier += 1;
                    state.GreatStridesDuration = 0;
                }
                if (state.InnovationActive)
                {
                    multiplier += 0.5;
                    state.InnovationUsed = true;
                }
                multiplier *= 1 + 0.1 * state.InnerQuiet;
                state.Quality += Math.Floor(BaseQualityIncrease * a.QualityIncreaseMultiplier * multiplier);
                state.CP -= a.CPCost;
                if (state.WasteNotActive)
                {
                    state.WasteNotUsed = true;
                    state.Durability -= (short)(a.DurabilityCost / 2);
                }
                else state.Durability -= a.DurabilityCost;
                state.InnerQuiet = (byte)Math.Min(state.InnerQuiet + 1, 10);
                break;
            case (int)Atlas.Actions.ActionMap.DelicateSynthesis:
                if (state.CP - a.CPCost < 0) return false;
                if (state.MuscleMemoryActive)
                {
                    multiplier += 1;
                    state.MuscleMemoryDuration = 0;
                }
                if (state.VenerationActive)
                {
                    multiplier += 0.5;
                    state.VenerationUsed = true;
                }
                state.Progress += Math.Floor(BaseProgressIncrease * a.ProgressIncreaseMultiplier * multiplier);
                
                multiplier = 1;
                if (state.GreatStridesActive)
                {
                    multiplier += 1;
                    state.GreatStridesDuration = 0;
                }
                if (state.InnovationActive)
                {
                    multiplier += 0.5;
                    state.InnovationUsed = true;
                }
                multiplier *= 1 + 0.1 * state.InnerQuiet;
                state.Quality += Math.Floor(BaseQualityIncrease * a.QualityIncreaseMultiplier * multiplier);
                
                state.CP -= a.CPCost;
                if (state.TrainedPerfectionActive)
                {
                    state.TrainedPerfectionUsed = true;
                    state.TrainedPerfectionActive = false;
                }
                else if (state.WasteNotActive)
                {
                    state.WasteNotUsed = true;
                    state.Durability -= (short)(a.DurabilityCost / 2);
                }
                else state.Durability -= a.DurabilityCost;
                state.InnerQuiet = (byte)Math.Min(state.InnerQuiet + 1, 10);
                break;
            case (int)Atlas.Actions.ActionMap.TrainedFinesse:
                if (state.CP - a.CPCost < 0) return false;
                if (state.InnerQuiet < 10) return false;
                if (state.GreatStridesActive)
                {
                    multiplier += 1;
                    state.GreatStridesDuration = 0;
                }
                if (state.InnovationActive)
                {
                    multiplier += 0.5;
                    state.InnovationUsed = true;
                }
                multiplier *= 1 + 0.1 * state.InnerQuiet;
                state.Quality += Math.Floor(BaseQualityIncrease * a.QualityIncreaseMultiplier * multiplier);
                state.CP -= a.CPCost;
                state.InnerQuiet = (byte)Math.Min(state.InnerQuiet + 1, 10);
                break;
            case (int)Atlas.Actions.ActionMap.PrudentSynthesis:
                if (state.WasteNotActive || state.TrainedPerfectionActive) return false;
                if (state.CP - a.CPCost < 0) return false;
                if (state.MuscleMemoryActive)
                {
                    multiplier += 1;
                    state.MuscleMemoryDuration = 0;
                }
                if (state.VenerationActive)
                {
                    multiplier += 0.5;
                    state.VenerationUsed = true;
                }
                state.Progress += Math.Floor(BaseProgressIncrease * a.ProgressIncreaseMultiplier * multiplier);
                state.CP -= a.CPCost;
                if (state.TrainedPerfectionActive)
                {
                    state.TrainedPerfectionUsed = true;
                    state.TrainedPerfectionActive = false;
                }
                else if (state.WasteNotActive)
                {
                    state.WasteNotUsed = true;
                    state.Durability -= (short)(a.DurabilityCost / 2);
                }
                else state.Durability -= a.DurabilityCost;
                break;
            case (int)Atlas.Actions.ActionMap.Observe:
                if (state.CP - a.CPCost < 0) return false;
                if (state.ObserveActive) return false;
                state.CP -= a.CPCost;
                break;
            case (int)Atlas.Actions.ActionMap.TrainedEye:
                if (state.Step > 0) return false;
                if (PureLevelDifference < 10 || Recipe.IsExpert) return false;
                if (state.CP - a.CPCost < 0) return false;
                state.Quality = Recipe.MaxQuality;
                state.CP -= a.CPCost;
                if (state.TrainedPerfectionActive)
                {
                    state.TrainedPerfectionUsed = true;
                    state.TrainedPerfectionActive = false;
                }
                else if (state.WasteNotActive)
                {
                    state.WasteNotUsed = true;
                    state.Durability -= (short)(a.DurabilityCost / 2);
                }
                else state.Durability -= a.DurabilityCost;
                break;
            case (int)Atlas.Actions.ActionMap.Reflect:
                if (state.Step > 0) return false;
                if (state.CP - a.CPCost < 0) return false;
                state.Quality += Math.Floor(BaseQualityIncrease * a.QualityIncreaseMultiplier * multiplier);
                state.CP -= a.CPCost;
                if (state.TrainedPerfectionActive)
                {
                    state.TrainedPerfectionUsed = true;
                    state.TrainedPerfectionActive = false;
                }
                else if (state.WasteNotActive)
                {
                    state.WasteNotUsed = true;
                    state.Durability -= (short)(a.DurabilityCost / 2);
                }
                else state.Durability -= a.DurabilityCost;

                state.InnerQuiet = 2;
                break;
            case (int)Atlas.Actions.ActionMap.MuscleMemory:
                if (state.Step > 0) return false;
                if (state.CP - a.CPCost < 0) return false;
                state.Progress += Math.Floor(BaseProgressIncrease * a.ProgressIncreaseMultiplier * multiplier);
                state.CP -= a.CPCost;
                if (state.TrainedPerfectionActive)
                {
                    state.TrainedPerfectionUsed = true;
                    state.TrainedPerfectionActive = false;
                }
                else if (state.WasteNotActive)
                {
                    state.WasteNotUsed = true;
                    state.Durability -= (short)(a.DurabilityCost / 2);
                }
                else state.Durability -= a.DurabilityCost;
                break;
        }
        
        #region After Action
        if (state is { ManipulationActive: true, Durability: > 0 } && action != (byte)Atlas.Actions.ActionMap.Manipulation)
        {
            if (state.Durability < Recipe.Durability) state.ManipulationUsed = true;
            state.Durability = (short)Math.Min(state.Durability + 5, Recipe.Durability);
        }
        
        if (state.VenerationActive && --state.VenerationDuration == 0 && !state.VenerationUsed) return false;
        if (state.ObserveActive)
        {
            if (!state.ObserveUsed) return false;
            else state.ObserveActive = false;
        }
        if (state.InnovationActive && --state.InnovationDuration == 0 && !state.InnovationUsed) return false;
        if (state.GreatStridesActive && --state.GreatStridesDuration == 0) return false;
        if (state.MuscleMemoryActive && --state.MuscleMemoryDuration == 0) return false;
        if (state.WasteNotActive && --state.WasteNotDuration == 0 && !state.WasteNotUsed) return false;
        if (state.ManipulationActive && --state.ManipulationDuration == 0 && !state.ManipulationUsed) return false;

        state.BasicTouchActive = false;
        state.StandardTouchActive = false;

        switch (action)
        {
            case (byte)Atlas.Actions.ActionMap.WasteNot:
            case (byte)Atlas.Actions.ActionMap.WasteNot2:
                if (state.WasteNotActive && !state.WasteNotUsed) return false;
                state.WasteNotDuration = a.ActiveTurns;
                state.WasteNotUsed = false;
                break;
            case (byte)Atlas.Actions.ActionMap.MuscleMemory:
                if (state.MuscleMemoryActive) return false;
                state.MuscleMemoryDuration = a.ActiveTurns;
                break;
            case (byte)Atlas.Actions.ActionMap.Veneration:
                if (state.VenerationActive && !state.VenerationUsed) return false;
                state.VenerationDuration = a.ActiveTurns;
                state.VenerationUsed = false;
                break;
            case (byte)Atlas.Actions.ActionMap.GreatStrides:
                if (state.GreatStridesActive) return false;
                state.GreatStridesDuration = a.ActiveTurns;
                break;
            case (byte)Atlas.Actions.ActionMap.Innovation:
                if (state.InnovationActive && !state.InnovationUsed) return false;
                state.InnovationDuration = a.ActiveTurns;
                state.InnovationUsed = false;
                break;
            case (byte)Atlas.Actions.ActionMap.TrainedPerfection:
                if (state.TrainedPerfectionActive || state.TrainedPerfectionUsed) return false;
                state.TrainedPerfectionActive = true;
                break;
            case (byte)Atlas.Actions.ActionMap.Manipulation:
                if (state.ManipulationActive && !state.ManipulationUsed) return false;
                state.ManipulationDuration = a.ActiveTurns;
                state.ManipulationUsed = false;
                break;
            case (byte)Atlas.Actions.ActionMap.Observe:
                if (state.ObserveActive) return false;
                state.ObserveActive = true;
                state.ObserveUsed = false;
                break;
            case (byte)Atlas.Actions.ActionMap.BasicTouch:
                state.BasicTouchActive = true;
                break;
            case (byte)Atlas.Actions.ActionMap.StandardTouch when state.BasicTouchActive:
                state.StandardTouchActive = true;
                break;
        }
        
        state.Step += 1;
        #endregion
            
        return true;
    }
}
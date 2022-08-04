namespace Libraries
{
    public static class Atlas
    {
        public static class Actions
        {
            public enum ActionMap
            {
                Observe,
                BasicSynth,
                CarefulSynthesis,
                BasicTouch,
                StandardTouch,
                AdvancedTouch,
                ByregotsBlessing,
                MastersMend,
                TricksOfTheTrade,
                InnerQuiet,
                Manipulation,
                WasteNot,
                WasteNot2,
                Veneration,
                Innovation,
                GreatStrides,
                PreciseTouch,
                MuscleMemory,
                PrudentTouch,
                FocusedSynthesis,
                FocusedTouch,
                Reflect,
                PreparatoryTouch,
                Groundwork,
                DelicateSynthesis,
                TrainedEye,
                TrainedFinesse,
                PrudentSynthesis,
                DummyAction
            }

            public static readonly Dictionary<int, Action> AllActions = new()
            {
                {
                    (int)ActionMap.DummyAction, new()
                    {
                        ID = (int)ActionMap.DummyAction,
                        ShortName = "dummyAction",
                        Name = "______________",
                        DurabilityCost = 0,
                        CPCost = 0,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 0.0,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.Immediate,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 1,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.Observe, new()
                    {
                        ID = (int)ActionMap.Observe,
                        ShortName = "observe",
                        Name = "Observe",
                        DurabilityCost = 0,
                        CPCost = 7,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 0.0,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.CountDown,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 13,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.BasicSynth, new()
                    {
                        ID = (int)ActionMap.BasicSynth,
                        ShortName = "basicSynth",
                        Name = "Basic Synthesis",
                        DurabilityCost = 10,
                        CPCost = 0,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 0.0,
                        ProgressIncreaseMultiplier = 1.0,
                        ActionType = ActionType.Immediate,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 1,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.CarefulSynthesis, new()
                    {
                        ID = (int)ActionMap.CarefulSynthesis,
                        ShortName = "carefulSynthesis",
                        Name = "Careful Synthesis",
                        DurabilityCost = 10,
                        CPCost = 7,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 0.0,
                        ProgressIncreaseMultiplier = 1.5,
                        ActionType = ActionType.Immediate,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 62,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.BasicTouch, new()
                    {
                        ID = (int)ActionMap.BasicTouch,
                        ShortName = "basicTouch",
                        Name = "Basic Touch",
                        DurabilityCost = 10,
                        CPCost = 18,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 1.0,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.CountDown,
                        ActiveTurns = -2,
                        Class = "All",
                        Level = 5,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.StandardTouch, new()
                    {
                        ID = (int)ActionMap.StandardTouch,
                        ShortName = "standardTouch",
                        Name = "Standard Touch",
                        DurabilityCost = 10,
                        CPCost = 32,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 1.25,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.CountDown,
                        ActiveTurns = -1,
                        Class = "All",
                        Level = 18,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.ByregotsBlessing, new()
                    {
                        ID = (int)ActionMap.ByregotsBlessing,
                        ShortName = "byregotsBlessing",
                        Name = "Byregot's Blessing",
                        DurabilityCost = 10,
                        CPCost = 24,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 1.0,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.Immediate,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 50,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.MastersMend, new()
                    {
                        ID = (int)ActionMap.MastersMend,
                        ShortName = "mastersMend",
                        Name = "Master's Mend",
                        DurabilityCost = 0,
                        CPCost = 88,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 0.0,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.Immediate,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 7,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.TricksOfTheTrade, new()
                    {
                        ID = (int)ActionMap.TricksOfTheTrade,
                        ShortName = "tricksOfTheTrade",
                        Name = "Tricks of the Trade",
                        DurabilityCost = 0,
                        CPCost = 0,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 0.0,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.Immediate,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 13,
                        OnGood = true,
                        OnExcellent = true,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.InnerQuiet, new()
                    {
                        ID = (int)ActionMap.InnerQuiet,
                        ShortName = "innerQuiet",
                        Name = "Inner Quiet",
                        DurabilityCost = 0,
                        CPCost = 0,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 0.0,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.CountUp,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 11,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.Manipulation, new()
                    {
                        ID = (int)ActionMap.Manipulation,
                        ShortName = "manipulation",
                        Name = "Manipulation",
                        DurabilityCost = 0,
                        CPCost = 96,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 0.0,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.CountDown,
                        ActiveTurns = 8,
                        Class = "All",
                        Level = 65,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.WasteNot, new()
                    {
                        ID = (int)ActionMap.WasteNot,
                        ShortName = "wasteNot",
                        Name = "Waste Not",
                        DurabilityCost = 0,
                        CPCost = 56,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 0.0,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.CountDown,
                        ActiveTurns = 4,
                        Class = "All",
                        Level = 15,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.WasteNot2, new()
                    {
                        ID = (int)ActionMap.WasteNot2,
                        ShortName = "wasteNot2",
                        Name = "Waste Not II",
                        DurabilityCost = 0,
                        CPCost = 98,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 0.0,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.CountDown,
                        ActiveTurns = 8,
                        Class = "All",
                        Level = 47,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.Veneration, new()
                    {
                        ID = (int)ActionMap.Veneration,
                        ShortName = "veneration",
                        Name = "Veneration",
                        DurabilityCost = 0,
                        CPCost = 18,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 0.0,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.CountDown,
                        ActiveTurns = 4,
                        Class = "All",
                        Level = 15,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.Innovation, new()
                    {
                        ID = (int)ActionMap.Innovation,
                        ShortName = "innovation",
                        Name = "Innovation",
                        DurabilityCost = 0,
                        CPCost = 18,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 0.0,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.CountDown,
                        ActiveTurns = 4,
                        Class = "All",
                        Level = 26,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.GreatStrides, new()
                    {
                        ID = (int)ActionMap.GreatStrides,
                        ShortName = "greatStrides",
                        Name = "Great Strides",
                        DurabilityCost = 0,
                        CPCost = 32,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 0.0,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.CountDown,
                        ActiveTurns = 3,
                        Class = "All",
                        Level = 21,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.PreciseTouch, new()
                    {
                        ID = (int)ActionMap.PreciseTouch,
                        ShortName = "preciseTouch",
                        Name = "Precise Touch",
                        DurabilityCost = 10,
                        CPCost = 18,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 1.5,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.Immediate,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 53,
                        OnGood = true,
                        OnExcellent = true,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.MuscleMemory, new()
                    {
                        ID = (int)ActionMap.MuscleMemory,
                        ShortName = "muscleMemory",
                        Name = "Muscle Memory",
                        DurabilityCost = 10,
                        CPCost = 6,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 0.0,
                        ProgressIncreaseMultiplier = 3.0,
                        ActionType = ActionType.CountDown,
                        ActiveTurns = 5,
                        Class = "All",
                        Level = 54,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.PrudentTouch, new()
                    {
                        ID = (int)ActionMap.PrudentTouch,
                        ShortName = "prudentTouch",
                        Name = "Prudent Touch",
                        DurabilityCost = 5,
                        CPCost = 25,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 1.0,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.Immediate,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 66,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.FocusedSynthesis, new()
                    {
                        ID = (int)ActionMap.FocusedSynthesis,
                        ShortName = "focusedSynthesis",
                        Name = "Focused Synthesis",
                        DurabilityCost = 10,
                        CPCost = 5,
                        SuccessProbability = 0.5,
                        QualityIncreaseMultiplier = 0.0,
                        ProgressIncreaseMultiplier = 2.0,
                        ActionType = ActionType.Immediate,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 67,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.FocusedTouch, new()
                    {
                        ID = (int)ActionMap.FocusedTouch,
                        ShortName = "focusedTouch",
                        Name = "Focused Touch",
                        DurabilityCost = 10,
                        CPCost = 18,
                        SuccessProbability = 0.5,
                        QualityIncreaseMultiplier = 1.5,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.Immediate,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 68,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.Reflect, new()
                    {
                        ID = (int)ActionMap.Reflect,
                        ShortName = "reflect",
                        Name = "Reflect",
                        DurabilityCost = 10,
                        CPCost = 6,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 1.0,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.Immediate,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 69,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.PreparatoryTouch, new()
                    {
                        ID = (int)ActionMap.PreparatoryTouch,
                        ShortName = "preparatoryTouch",
                        Name = "Preparatory Touch",
                        DurabilityCost = 20,
                        CPCost = 40,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 2.0,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.Immediate,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 71,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.Groundwork, new()
                    {
                        ID = (int)ActionMap.Groundwork,
                        ShortName = "groundwork",
                        Name = "Groundwork",
                        DurabilityCost = 20,
                        CPCost = 18,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 0.0,
                        ProgressIncreaseMultiplier = 3.0,
                        ActionType = ActionType.Immediate,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 72,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.DelicateSynthesis, new()
                    {
                        ID = (int)ActionMap.DelicateSynthesis,
                        ShortName = "delicateSynthesis",
                        Name = "Delicate Synthesis",
                        DurabilityCost = 10,
                        CPCost = 32,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 1.0,
                        ProgressIncreaseMultiplier = 1.0,
                        ActionType = ActionType.Immediate,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 76,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.TrainedEye, new()
                    {
                        ID = (int)ActionMap.TrainedEye,
                        ShortName = "trainedEye",
                        Name = "Trained Eye",
                        DurabilityCost = 10,
                        CPCost = 250,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 0.0,
                        ProgressIncreaseMultiplier = 0.0,
                        ActionType = ActionType.Immediate,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 80,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.TrainedFinesse, new()
                    {
                        ID = (int)ActionMap.TrainedFinesse,
                        ShortName = "trainedFinesse",
                        Name = "Trained Finesse",
                        DurabilityCost = 0,
                        CPCost = 32,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 1.0,
                        ProgressIncreaseMultiplier = 0,
                        ActionType = ActionType.Immediate,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 90,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.PrudentSynthesis, new()
                    {
                        ID = (int)ActionMap.PrudentSynthesis,
                        ShortName = "prudentSynthesis",
                        Name = "Prudent Synthesis",
                        DurabilityCost = 5,
                        CPCost = 18,
                        SuccessProbability = 1.0,
                        QualityIncreaseMultiplier = 0,
                        ProgressIncreaseMultiplier = 1.8,
                        ActionType = ActionType.Immediate,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 88,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                },
                {
                    (int)ActionMap.AdvancedTouch, new()
                    {
                        ID = (int)ActionMap.AdvancedTouch,
                        ShortName = "advancedTouch",
                        Name = "Advanced Touch",
                        DurabilityCost = 10,
                        CPCost = 46,
                        SuccessProbability = 100,
                        QualityIncreaseMultiplier = 1.5,
                        ProgressIncreaseMultiplier = 0,
                        ActionType = ActionType.Immediate,
                        ActiveTurns = 1,
                        Class = "All",
                        Level = 84,
                        OnGood = false,
                        OnExcellent = false,
                        OnPoor = false
                    }
                }
            };
            public static readonly int[] DependableActions = {
                (int)ActionMap.CarefulSynthesis,
                (int)ActionMap.DelicateSynthesis,
                (int)ActionMap.Groundwork,
                (int)ActionMap.FocusedSynthesis,
                (int)ActionMap.BasicSynth,

                (int)ActionMap.FocusedTouch,
                (int)ActionMap.PreparatoryTouch,
                (int)ActionMap.BasicTouch,
                (int)ActionMap.StandardTouch,
                (int)ActionMap.AdvancedTouch,
                (int)ActionMap.ByregotsBlessing,
                (int)ActionMap.PrudentTouch,
                (int)ActionMap.TrainedFinesse,

                (int)ActionMap.Manipulation,
                (int)ActionMap.MastersMend,

                (int)ActionMap.Veneration,
                (int)ActionMap.Innovation,
                (int)ActionMap.GreatStrides,
                (int)ActionMap.Observe,
                (int)ActionMap.WasteNot,
                (int)ActionMap.WasteNot2,

                (int)ActionMap.MuscleMemory,
                (int)ActionMap.Reflect,
                (int)ActionMap.TrainedEye
            };
            public static readonly int[] FirstRoundActions = {
                (int)ActionMap.MuscleMemory,
                (int)ActionMap.TrainedEye,
                (int)ActionMap.Reflect
            };
            public static readonly int[] ProgressActions = DependableActions.Where(x => AllActions[x].ProgressIncreaseMultiplier > 0).ToArray();
            public static readonly int[] QualityActions = DependableActions.Where(x => AllActions[x].QualityIncreaseMultiplier > 0).Concat(new[] { (int)ActionMap.TrainedEye }).ToArray();
            public static readonly int[] Buffs = {
                (int)ActionMap.Observe,
                (int)ActionMap.Manipulation,
                (int)ActionMap.WasteNot,
                (int)ActionMap.WasteNot2,
                (int)ActionMap.Veneration,
                (int)ActionMap.Innovation,
                (int)ActionMap.GreatStrides
            };
            public static readonly int[] ProgressBuffs = {
                (int)ActionMap.Veneration,
                // FinalAppraisal // TODO: This
            };
            public static readonly int[] QualityBuffs = {
                (int)ActionMap.Innovation,
                (int)ActionMap.GreatStrides
            };
            public static readonly KeyValuePair<int, double>[] DurabilityActions = {
                new((int)ActionMap.WasteNot2, (20D * 8) / AllActions[(int)ActionMap.WasteNot2].CPCost),
                new((int)ActionMap.WasteNot, (20D * 4) / AllActions[(int)ActionMap.WasteNot].CPCost),
                new((int)ActionMap.Manipulation, 40D / AllActions[(int)ActionMap.Manipulation].CPCost),
                new((int)ActionMap.MastersMend, 30D / AllActions[(int)ActionMap.MastersMend].CPCost)
            };

            public static void UpgradeActionsByLevel(int level)
            {
                if (level > 31)
                {
                    AllActions[(int)ActionMap.BasicSynth].ProgressIncreaseMultiplier = 1.2;
                }
                if (level >= 82)
                {
                    AllActions[(int)ActionMap.CarefulSynthesis].ProgressIncreaseMultiplier = 1.8;
                }

                if (level >= 86)
                {
                    AllActions[(int)ActionMap.Groundwork].ProgressIncreaseMultiplier = 3.6;
                }
            }
        }
        public static readonly Dictionary<int, int> LevelTable = new()
        {
            { 51, 120 },
            { 52, 125 },
            { 53, 130 },
            { 54, 133 },
            { 55, 136 },
            { 56, 139 },
            { 57, 142 },
            { 58, 145 },
            { 59, 148 },
            { 60, 150 },
            { 61, 260 },
            { 62, 265 },
            { 63, 270 },
            { 64, 273 },
            { 65, 276 },
            { 66, 279 },
            { 67, 282 },
            { 68, 285 },
            { 69, 288 },
            { 70, 290 },
            { 71, 390 },
            { 72, 395 },
            { 73, 400 },
            { 74, 403 },
            { 75, 406 },
            { 76, 409 },
            { 77, 412 },
            { 78, 415 },
            { 79, 418 },
            { 80, 420 },
            { 81, 530 },
            { 82, 535 },
            { 83, 540 },
            { 84, 543 },
            { 85, 546 },
            { 86, 549 },
            { 87, 552 },
            { 88, 555 },
            { 89, 558 },
            { 90, 560 }
        };
    }
}

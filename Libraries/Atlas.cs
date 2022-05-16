﻿namespace Libraries
{
    public static class Atlas
    {
        public static class Actions
        {
            public static readonly Action Observe = new Action
            {
                ID = 1,
                ShortName = "observe",
                Name = "Observe",
                DurabilityCost = 0,
                CPCost = 7,
                SuccessProbability = 1.0,
                QualityIncreaseMultiplier = 0.0,
                ProgressIncreaseMultiplier = 0.0,
                ActionType = ActionType.Immediate,
                ActiveTurns = 1,
                Class = "All",
                Level = 13,
                OnGood = false,
                OnExcellent = false,
                OnPoor = false
            };
            public static Action BasicSynth = new Action
            {
                ID = 2,
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
            };
            public static Action CarefulSynthesis = new Action
            {
                ID = 3,
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
            };
            public static readonly Action BasicTouch = new Action
            {
                ID = 4,
                ShortName = "basicTouch",
                Name = "Basic Touch",
                DurabilityCost = 10,
                CPCost = 18,
                SuccessProbability = 1.0,
                QualityIncreaseMultiplier = 1.0,
                ProgressIncreaseMultiplier = 0.0,
                ActionType = ActionType.Immediate,
                ActiveTurns = 1,
                Class = "All",
                Level = 5,
                OnGood = false,
                OnExcellent = false,
                OnPoor = false
            };
            public static readonly Action StandardTouch = new Action
            {
                ID = 5,
                ShortName = "standardTouch",
                Name = "Standard Touch",
                DurabilityCost = 10,
                CPCost = 32,
                SuccessProbability = 1.0,
                QualityIncreaseMultiplier = 1.25,
                ProgressIncreaseMultiplier = 0.0,
                ActionType = ActionType.Immediate,
                ActiveTurns = 1,
                Class = "All",
                Level = 18,
                OnGood = false,
                OnExcellent = false,
                OnPoor = false
            };
            public static readonly Action ByregotsBlessing = new Action
            {
                ID = 6,
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
            };
            public static readonly Action MastersMend = new Action
            {
                ID = 7,
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
            };
            public static readonly Action TricksOfTheTrade = new Action
            {
                ID = 8,
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
            };
            public static readonly Action InnerQuiet = new Action
            {
                ID = 9,
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
            };
            public static readonly Action Manipulation = new Action
            {
                ID = 10,
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
            };
            public static readonly Action WasteNot = new Action
            {
                ID = 11,
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
            };
            public static readonly Action WasteNot2 = new Action
            {
                ID = 12,
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
            };
            public static readonly Action Veneration = new Action
            {
                ID = 13,
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
            };
            public static readonly Action Innovation = new Action
            {
                ID = 14,
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
            };
            public static readonly Action GreatStrides = new Action
            {
                ID = 15,
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
            };
            public static readonly Action PreciseTouch = new Action
            {
                ID = 16,
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
            };
            public static readonly Action MuscleMemory = new Action
            {
                ID = 17,
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
            };
            public static readonly Action PrudentTouch = new Action
            {
                ID = 18,
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
            };
            public static readonly Action FocusedSynthesis = new Action
            {
                ID = 19,
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
            };
            public static readonly Action FocusedTouch = new Action
            {
                ID = 20,
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
            };
            public static readonly Action Reflect = new Action
            {
                ID = 21,
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
            };
            public static readonly Action PreparatoryTouch = new Action
            {
                ID = 22,
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
            };
            public static Action Groundwork = new Action
            {
                ID = 23,
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
            };
            public static readonly Action DelicateSynthesis = new Action
            {
                ID = 24,
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
            };
            public static readonly Action TrainedEye = new Action
            {
                ID = 25,
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
            };
            public static readonly Action TrainedFinesse = new Action
            {
                ID = 26,
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
            };
            public static readonly Action PrudentSynthesis = new Action
            {
                ID = 27,
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
            };
            public static readonly Action AdvancedTouch = new Action
            {
                ID = 28,
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
            };
            public static readonly Action DummyAction = new Action
            {
                ID = 0,
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
            };

            public static Action[] AllActions = {
                Observe,
                BasicSynth,
                CarefulSynthesis,
                BasicTouch,
                StandardTouch,
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
                DummyAction
            };
            public static readonly Action[] DependableActions = {
                CarefulSynthesis,
                DelicateSynthesis,
                Groundwork,
                FocusedSynthesis,
                BasicSynth,

                FocusedTouch,
                PreparatoryTouch,
                BasicTouch,
                StandardTouch,
                AdvancedTouch,
                ByregotsBlessing,
                PrudentTouch,
                TrainedFinesse,

                Manipulation,
                MastersMend,

                Veneration,
                Innovation,
                GreatStrides,
                Observe,
                WasteNot,
                WasteNot2,

                MuscleMemory,
                Reflect,
                TrainedEye
            };
            public static readonly Action[] FirstRoundActions = {
                MuscleMemory,
                TrainedEye,
                Reflect
            };
            public static readonly Action[] ProgressActions = DependableActions.Where(x => x.ProgressIncreaseMultiplier > 0).ToArray();
            public static readonly Action[] QualityActions = DependableActions.Where(x => x.QualityIncreaseMultiplier > 0).Concat(new[] { TrainedEye }).ToArray();
            public static readonly Action[] Buffs = {
                Observe,
                Manipulation,
                WasteNot,
                WasteNot2,
                Veneration,
                Innovation,
                GreatStrides
            };
            public static readonly Action[] ProgressBuffs = {
                Veneration,
                //FinalAppraisal
            };
            public static readonly Action[] QualityBuffs = {
                Innovation,
                GreatStrides
            };
            public static readonly Action[] DurabilityActions = {
                Manipulation,
                MastersMend,
                WasteNot,
                WasteNot2
            };

            public static void UpgradeActionsByLevel(int level)
            {
                if (level > 31)
                {
                    Action a = BasicSynth;
                    a.ProgressIncreaseMultiplier = 1.2;
                    BasicSynth = a;
                }
                if (level >= 82)
                {
                    Action a = CarefulSynthesis;
                    a.ProgressIncreaseMultiplier = 1.8;
                    CarefulSynthesis = a;
                }

                if (level >= 86)
                {
                    Action a = Groundwork;
                    a.ProgressIncreaseMultiplier = 3.6;
                    Groundwork = a;
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
        public static readonly Dictionary<string, Dictionary<int, double>> LevelDifferenceFactors = new Dictionary<string, Dictionary<int, double>>
        {
            {
                "control", new()
                {
                    { -30, 0.6 },
                    { -29, 0.64 },
                    { -28, 0.68 },
                    { -27, 0.72 },
                    { -26, 0.76 },
                    { -25, 0.80 },
                    { -24, 0.84 },
                    { -23, 0.88 },
                    { -22, 0.92 },
                    { -21, 0.96 },
                    { -20, 1 },
                    { -19, 1 },
                    { -18, 1 },
                    { -17, 1 },
                    { -16, 1 },
                    { -15, 1 },
                    { -14, 1 },
                    { -13, 1 },
                    { -12, 1 },
                    { -11, 1 },
                    { -10, 1 },
                    { -9, 1 },
                    { -8, 1 },
                    { -7, 1 },
                    { -6, 1 },
                    { -5, 1 },
                    { -4, 1 },
                    { -3, 1 },
                    { -2, 1 },
                    { -1, 1 },
                    { 0, 1 },
                    { 1, 1 },
                    { 2, 1 },
                    { 3, 1 },
                    { 4, 1 },
                    { 5, 1 },
                    { 6, 1 },
                    { 7, 1 },
                    { 8, 1 },
                    { 9, 1 },
                    { 10, 1 },
                    { 11, 1 },
                    { 12, 1 },
                    { 13, 1 },
                    { 14, 1 },
                    { 15, 1 },
                    { 16, 1 },
                    { 17, 1 },
                    { 18, 1 },
                    { 19, 1 },
                    { 20, 1 },
                    { 21, 1 },
                    { 22, 1 },
                    { 23, 1 },
                    { 24, 1 },
                    { 25, 1 },
                    { 26, 1 },
                    { 27, 1 },
                    { 28, 1 },
                    { 29, 1 },
                    { 30, 1 },
                    { 31, 1 },
                    { 32, 1 },
                    { 33, 1 },
                    { 34, 1 },
                    { 35, 1 },
                    { 36, 1 },
                    { 37, 1 },
                    { 38, 1 },
                    { 39, 1 },
                    { 40, 1 },
                    { 41, 1 },
                    { 42, 1 },
                    { 43, 1 },
                    { 44, 1 },
                    { 45, 1 },
                    { 46, 1 },
                    { 47, 1 },
                    { 48, 1 },
                    { 49, 1 }
                }
            },
            {
                "craftsmanship", new Dictionary<int, double>
                {
                    { -30, 0.8 },
                    { -29, 0.82 },
                    { -28, 0.84 },
                    { -27, 0.86 },
                    { -26, 0.88 },
                    { -25, 0.90 },
                    { -24, 0.92 },
                    { -23, 0.94 },
                    { -22, 0.96 },
                    { -21, 0.98 },
                    { -20, 1 },
                    { -19, 1 },
                    { -18, 1 },
                    { -17, 1 },
                    { -16, 1 },
                    { -15, 1 },
                    { -14, 1 },
                    { -13, 1 },
                    { -12, 1 },
                    { -11, 1 },
                    { -10, 1 },
                    { -9, 1 },
                    { -8, 1 },
                    { -7, 1 },
                    { -6, 1 },
                    { -5, 1 },
                    { -4, 1 },
                    { -3, 1 },
                    { -2, 1 },
                    { -1, 1 },
                    { 0, 1 },
                    { 1, 1.05 },
                    { 2, 1.1 },
                    { 3, 1.15 },
                    { 4, 1.2 },
                    { 5, 1.25 },
                    { 6, 1.27 },
                    { 7, 1.29 },
                    { 8, 1.31 },
                    { 9, 1.33 },
                    { 10, 1.35 },
                    { 11, 1.37 },
                    { 12, 1.39 },
                    { 13, 1.41 },
                    { 14, 1.43 },
                    { 15, 1.45 },
                    { 16, 1.47 },
                    { 17, 1.47 },
                    { 18, 1.48 },
                    { 19, 1.49 },
                    { 20, 1.5 },
                    { 21, 1.5 },
                    { 22, 1.5 },
                    { 23, 1.5 },
                    { 24, 1.5 },
                    { 25, 1.5 },
                    { 26, 1.5 },
                    { 27, 1.5 },
                    { 28, 1.5 },
                    { 29, 1.5 },
                    { 30, 1.5 },
                    { 31, 1.5 },
                    { 32, 1.5 },
                    { 33, 1.5 },
                    { 34, 1.5 },
                    { 35, 1.5 },
                    { 36, 1.5 },
                    { 37, 1.5 },
                    { 38, 1.5 },
                    { 39, 1.5 },
                    { 40, 1.5 },
                    { 41, 1.5 },
                    { 42, 1.5 },
                    { 43, 1.5 },
                    { 44, 1.5 },
                    { 45, 1.5 },
                    { 46, 1.5 },
                    { 47, 1.5 },
                    { 48, 1.5 },
                    { 49, 1.5 },
                }
            }
        };
    }
}

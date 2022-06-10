namespace Libraries
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
                ActionType = ActionType.CountDown,
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
                ActionType = ActionType.CountDown,
                ActiveTurns = -2,
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
                ActionType = ActionType.CountDown,
                ActiveTurns = -1,
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
            public static readonly KeyValuePair<Action, double>[] DurabilityActions = {
                new(WasteNot2, (20D * 8) / WasteNot2.CPCost),
                new(WasteNot, (20D * 4) / WasteNot.CPCost),
                new(Manipulation, 40D / Manipulation.CPCost),
                new(MastersMend, 30D / MastersMend.CPCost)
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
    }
}

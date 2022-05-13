using CraftingSolver;

Recipe
    newNeoIshgardian = new Recipe
    {
        Level = 480,
        Difficulty = 2800,
        Durability = 70,
        StartQuality = 0,
        MaxQuality = 8500,
        SuggestedCraftsmanship = 2480,
        SuggestedControl = 2195,
        Stars = 3,
        ProgressDivider = 110,
        QualityDivider = 90,
        ProgressModifier = 0.8,
        QualityModifier = 0.7
    },
    newExarchic = new Recipe
    {
        Level = 510,
        Difficulty = 3600,
        Durability = 70,
        StartQuality = 0,
        MaxQuality = 9500,
        SuggestedCraftsmanship = 2620,
        SuggestedControl = 2540,
        Stars = 4,
        ProgressDivider = 110,
        QualityDivider = 90,
        ProgressModifier = 0.8,
        QualityModifier = 0.7
    },
    tsai = new Recipe
    {
        Level = 555,
        Difficulty = 3400,
        StartQuality = 0,
        MaxQuality = 7100,
        Durability = 80,
        SuggestedCraftsmanship = 2748,
        SuggestedControl = 2589,
        Stars = 0,
        ProgressDivider = 129,
        QualityDivider = 113,
        ProgressModifier = 1.0,
        QualityModifier = 1.0
    },
    chondriteAlembic = new Recipe
    {
        Level = 560,
        Difficulty = 3500,
        StartQuality = 0,
        MaxQuality = 7200,
        Durability = 80,
        SuggestedCraftsmanship = 2805,
        SuggestedControl = 2635,
        Stars = 0,
        ProgressDivider = 130,
        QualityDivider = 115,
        ProgressModifier = 0.9,
        QualityModifier = 0.8
    },
    bluefeatherBarding = new Recipe
    {
        Level = 570,
        Difficulty = 3700,
        StartQuality = 0,
        MaxQuality = 7400,
        Durability = 80,
        SuggestedCraftsmanship = 2924,
        SuggestedControl = 2703,
        Stars = 1,
        ProgressDivider = 130,
        QualityDivider = 115,
        ProgressModifier = 90,
        QualityModifier = 80
    },
    classicalMilpreves = new Recipe
    {
        Level = 580,
        Difficulty = 3900,
        StartQuality = 0,
        MaxQuality = 10920,
        Durability = 70,
        SuggestedCraftsmanship = 3180,
        SuggestedControl = 3080,
        Stars = 2,
        ProgressDivider = 130,
        QualityDivider = 115,
        ProgressModifier = 0.8,
        QualityModifier = 0.7
    };

Crafter
    unbuffed = new Crafter
    {
        Class = "Armorer",
        Craftsmanship = 2737,
        Control = 2810,
        CP = 548,
        Level = 80,
        Specialist = false,
        Actions = Atlas.Actions.DependableActions
    },
    chiliCrabCunning = new Crafter
    {
        Class = "Armorer",
        Craftsmanship = 2737,
        Control = 2880,
        CP = 636,
        Level = 80,
        Specialist = false,
        Actions = Atlas.Actions.DependableActions
    },
    newUnbuffed = new Crafter
    {
        Class = "Armorer",
        Craftsmanship = 3286,
        Control = 3396,
        CP = 565,
        Level = 90,
        Specialist = false,
        Actions = Atlas.Actions.DependableActions
    },
    newBuffed = new Crafter
    {
        Class = "Armorer",
        Craftsmanship = 3324,
        Control = 3462,
        CP = 675,
        Level = 90,
        Specialist = false,
        Actions = Atlas.Actions.DependableActions
    };

Simulator sim = new Simulator
{
    Crafter = newBuffed,
    Recipe = chondriteAlembic,
    ReliabilityIndex = 1,
    MaxLength = 30,
};

int maxTasks = 20;
List<CraftingSolver.Action>? solution = new List<CraftingSolver.Action>();
sim.Initialize();
Atlas.Actions.UpgradeActionsByLevel(sim.Crafter.Level);
solution = new Libraries.Solvers.JABOASolver().Run(sim, maxTasks, loggingDelegate: (message) => Console.WriteLine(message));
Console.WriteLine(string.Join(",", solution.Select(x => x.ShortName)));
Console.WriteLine("Press Enter to exit");
Console.ReadLine();

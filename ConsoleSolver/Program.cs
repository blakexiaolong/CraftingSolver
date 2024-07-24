using Libraries;
using Libraries.Solvers;

Recipe
    newNeoIshgardian = new()
    {
        Level = 80,
        RLevel = 480,
        Difficulty = 2800,
        Durability = 70,
        StartQuality = 0,
        MaxQuality = 8500,
        ProgressDivider = 110,
        QualityDivider = 90,
        ProgressModifier = 0.8,
        QualityModifier = 0.7
    },
    newExarchic = new()
    {
        Level = 80,
        RLevel = 510,
        Difficulty = 3600,
        Durability = 70,
        StartQuality = 0,
        MaxQuality = 9500,
        ProgressDivider = 110,
        QualityDivider = 90,
        ProgressModifier = 0.8,
        QualityModifier = 0.7
    },
    tsai = new()
    {
        Level = 89,
        RLevel = 555,
        Difficulty = 3400,
        StartQuality = 0,
        MaxQuality = 7100,
        Durability = 80,
        ProgressDivider = 129,
        QualityDivider = 113,
        ProgressModifier = 1.0,
        QualityModifier = 1.0
    },
    chondriteAlembic = new()
    {
        Level = 90,
        RLevel = 560,
        Difficulty = 3500,
        StartQuality = 0,
        MaxQuality = 7200,
        Durability = 80,
        ProgressDivider = 130,
        QualityDivider = 115,
        ProgressModifier = 0.9,
        QualityModifier = 0.8
    },
    bluefeatherBarding = new()
    {
        Level = 90,
        RLevel = 570,
        Difficulty = 3700,
        StartQuality = 0,
        MaxQuality = 7400,
        Durability = 80,
        ProgressDivider = 130,
        QualityDivider = 115,
        ProgressModifier = 0.90,
        QualityModifier = 0.80
    },
    classicalMilpreves = new()
    {
        Level = 90,
        RLevel = 580,
        Difficulty = 3900,
        StartQuality = 0,
        MaxQuality = 10920,
        Durability = 70,
        ProgressDivider = 130,
        QualityDivider = 115,
        ProgressModifier = 0.8,
        QualityModifier = 0.7
    },
    rinascitaSword = new()
    {
        Level = 90,
        RLevel = 610,
        Difficulty = 5060,
        StartQuality = 0,
        MaxQuality = 12628,
        Durability = 70,
        ProgressDivider = 130,
        QualityDivider = 115,
        ProgressModifier = 0.8,
        QualityModifier = 0.7
    },
    diadochosSword = new()
    {
        Level = 90,
        RLevel = 640,
        Difficulty = 6600,
        StartQuality = 0,
        MaxQuality = 14040,
        Durability = 70,
        ProgressDivider = 130,
        QualityDivider = 115,
        ProgressModifier = 0.8,
        QualityModifier = 0.7
    },
    g8Alkahest = new()
    {
        Level = 90,
        RLevel = 640,
        Difficulty = 4488,
        StartQuality = 0,
        MaxQuality = 9090,
        Durability = 35,
        ProgressDivider = 130,
        QualityDivider = 115,
        ProgressModifier = 0.8,
        QualityModifier = 0.7,
        IsExpert = true
    },
    orangeScrip = new()
    {
        Level = 100,
        RLevel = 690,
        Difficulty = 6600,
        StartQuality = 0,
        MaxQuality = 12000,
        Durability = 80,
        ProgressDivider = 170,
        QualityDivider = 150,
        ProgressModifier = 0.9,
        QualityModifier = 0.75,
        IsExpert = false
    },
    thunderyardsCrafting = new()
    {
        Level = 99,
        RLevel = 685,
        Difficulty = 6300,
        StartQuality = 0,
        MaxQuality = 11400,
        Durability = 80,
        ProgressDivider = 167,
        QualityDivider = 147,
        ProgressModifier = 1,
        QualityModifier = 1,
        IsExpert = false
    };

Crafter
    unbuffed = new()
    {
        Craftsmanship = 2737,
        Control = 2810,
        CP = 548,
        Level = 80,
        Actions = Atlas.Actions.DependableActions
    },
    chiliCrabCunning = new()
    {
        Craftsmanship = 2737,
        Control = 2880,
        CP = 636,
        Level = 80,
        Actions = Atlas.Actions.DependableActions
    },
    newUnbuffed = new()
    {
        Craftsmanship = 4132,
        Control = 3890,
        CP = 687,
        Level = 90,
        Actions = Atlas.Actions.DependableActions
    },
    newBuffed = new()
    {
        Craftsmanship = 4132,
        Control = 3974,
        CP = 687,
        Level = 90,
        Actions = Atlas.Actions.DependableActions
    },
    dawntrailTemp = new()
    {
        Craftsmanship = 4842,
        Control = 4716,
        CP = 658,
        Level = 100,
        Actions = Atlas.Actions.DependableActions
    };

LightSimulator sim = new(dawntrailTemp, thunderyardsCrafting);
Atlas.Actions.UpgradeActionsByLevel(sim.Crafter.Level);

var solution = await new SawStepSolver(sim, Console.WriteLine).Run();

Console.WriteLine(string.Join(",", solution.Select(x => x.ShortName)));
Console.WriteLine("Press Enter to exit");
Console.ReadLine();

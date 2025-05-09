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
    },
    rareBlackStarEarrings = new()
    {
        Level = 100,
        RLevel = 690,
        Difficulty = 6600,
        StartQuality = 3016,
        MaxQuality = 12000,
        Durability = 80,
        ProgressDivider = 170,
        QualityDivider = 150,
        ProgressModifier = 0.9,
        QualityModifier = 0.75,
        IsExpert = false
    },
    archeoBow = new()
    {
        Level = 100,
        RLevel = 710,
        Difficulty = 7500,
        StartQuality = 0,
        MaxQuality = 16500,
        Durability = 70,
        ProgressDivider = 170,
        QualityDivider = 150,
        ProgressModifier = 0.9,
        QualityModifier = 0.75,
        IsExpert = false
    },
    archeoPrecraft = new()
    {
        Level = 100,
        RLevel = 710,
        Difficulty = 4_125,
        StartQuality = 0,
        MaxQuality = 12_000,
        Durability = 35,
        ProgressDivider = 170,
        QualityDivider = 150,
        ProgressModifier = 0.9,
        QualityModifier = 0.75,
        IsExpert = false
    },
    everseekerPrecraft = new()
    {
        Level = 100,
        RLevel = 710,
        Difficulty = 4125,
        StartQuality = 0,
        MaxQuality = 12000,
        Durability = 35,
        ProgressDivider = 170,
        QualityDivider = 150,
        ProgressModifier = 0.9,
        QualityModifier = 0.75,
        IsExpert = false
    },
    everseeker = new()
    {
        Level = 100,
        RLevel = 720,
        Difficulty = 8050,
        StartQuality = 0,
        MaxQuality = 17600,
        Durability = 70,
        ProgressDivider = 170,
        QualityDivider = 150,
        ProgressModifier = 0.9,
        QualityModifier = 0.75,
        IsExpert = false
    },
    indurateSpring = new()
    {
        Level = 100,
        RLevel = 702,
        Difficulty = 9_900,
        StartQuality = 0,
        MaxQuality = 20_300,
        Durability = 80,
        ProgressDivider = 180,
        QualityDivider = 180,
        ProgressModifier = 1.0,
        QualityModifier = 1.0,
        IsExpert = false
    };

Crafter
    ashBuffed = new()
    {
        Craftsmanship = 4823,
        Control = 4338,
        CP = 658,
        Level = 100,
        Actions = Atlas.Actions.DependableActions
    },
    dawntrail = new()
    {
        Craftsmanship = 5419,
        Control = 4990,
        CP = 630,
        Level = 100,
        Actions = Atlas.Actions.DependableActions
    },
    dawntrailBuffed = new()
    {
        Craftsmanship = 5419+120,
        Control = 4990,
        CP = 630+109,
        Level = 100,
        Actions = Atlas.Actions.DependableActions
    };

LightSimulator sim = new(dawntrail, everseeker);
Atlas.Actions.UpgradeActionsByLevel(sim.Crafter.Level);

var solution = await new SawStepSolver(sim, Console.WriteLine).Run();

Console.WriteLine(string.Join(",", solution.Select(x => x.ShortName)));
Console.WriteLine("Press Enter to exit");
Console.ReadLine();

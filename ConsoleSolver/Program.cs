using Libraries;
using Libraries.Solvers;
using Action = Libraries.Action;

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
        Control = 3980,
        CP = 794,
        Level = 90,
        Actions = Atlas.Actions.DependableActions
    };

LightSimulator sim = new(newBuffed, diadochosSword);
Atlas.Actions.UpgradeActionsByLevel(sim.Crafter.Level);

var solver = new SawStepSolver(sim, Console.WriteLine);
var solution = await solver.Run();

Console.WriteLine(string.Join(",", solution.Select(x => x.ShortName)));
Console.WriteLine("Press Enter to exit");
Console.ReadLine();

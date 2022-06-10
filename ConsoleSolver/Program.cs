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
        Craftsmanship = 3286,
        Control = 3396,
        CP = 565,
        Level = 90,
        Actions = Atlas.Actions.DependableActions
    },
    newBuffed = new()
    {
        Craftsmanship = 3324,
        Control = 3462,
        CP = 675,
        Level = 90,
        Actions = Atlas.Actions.DependableActions
    };

LightSimulator sim = new(newBuffed, bluefeatherBarding);
const int maxTasks = 20;
Atlas.Actions.UpgradeActionsByLevel(sim.Crafter.Level);

var solver = new JaboaSolver(sim, Console.WriteLine);
var actions = new List<Action>
{
    Atlas.Actions.MuscleMemory, //Atlas.Actions.Manipulation, Atlas.Actions.WasteNot2,
    Atlas.Actions.Veneration, Atlas.Actions.Groundwork, Atlas.Actions.Innovation,
    Atlas.Actions.CarefulSynthesis, Atlas.Actions.PreparatoryTouch, Atlas.Actions.PreparatoryTouch,
    Atlas.Actions.PreparatoryTouch, Atlas.Actions.Innovation,
    Atlas.Actions.PreparatoryTouch, Atlas.Actions.PreparatoryTouch, Atlas.Actions.GreatStrides,
    Atlas.Actions.ByregotsBlessing, Atlas.Actions.CarefulSynthesis
};
var e = solver.GetDurabilityCost(actions, out int stopIx);
solver.GreedySolveDurability(actions, (int)sim.Simulate(actions, useDurability: false)!.Value.CP, out var neat);

var solution = solver.Run(maxTasks);
if (solution == null) return;
Console.WriteLine(string.Join(",", solution.Select(x => x.ShortName)));
Console.WriteLine("Press Enter to exit");
Console.ReadLine();

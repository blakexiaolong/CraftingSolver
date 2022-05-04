using CraftingSolver;
using static CraftingSolver.Solver;

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
    Recipe = newNeoIshgardian
    ,
    MaxTrickUses = 0,
    UseConditions = false,
    ReliabilityIndex = 1,
    MaxLength = 30,
};

int maxTasks = 20;
List<CraftingSolver.Action>? solution = new List<CraftingSolver.Action>();
try
{
    sim.Initialize();
    Atlas.Actions.UpgradeActionsByLevel(sim.Crafter.Level);
    solution = new JABOASolver().Run(sim, maxTasks);
}
catch (Exception)
{

}
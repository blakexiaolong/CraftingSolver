using Libraries;
using Libraries.Solvers;

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
    },
    test = new()
    {
        Craftsmanship = 5589,
        Control = 5014,
        CP = 741,
        Level = 100,
        Actions = Atlas.Actions.DependableActions
    };

Crafter crafter = dawntrail;
Recipe recipe = await SawStepSolver.Lookup("Ceviche", crafter);

NanoSimulator sim = new(crafter, recipe);
Atlas.Actions.UpgradeActionsByLevel(sim.Crafter.Level);

var solution = new SawStepSolver(sim, Console.WriteLine).Run();
Console.WriteLine(string.Join(",", solution.Select(x => x.ShortName)));

Console.WriteLine("Press Enter to exit");
Console.ReadLine();

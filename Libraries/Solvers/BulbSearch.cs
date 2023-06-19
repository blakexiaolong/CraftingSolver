using System.Collections;
using System.Collections.Concurrent;
using System.Linq;

namespace Libraries.Solvers;

public class BulbSearch
{
    private int _beamSize = 10000;
    private readonly ConcurrentDictionary<ActionNode?, int> _beam = new();
    private readonly LightSimulator _sim;
    private readonly ActionNodeComparer _comparer;

    public BulbSearch(LightSimulator sim)
    {
        _sim = sim;
        _comparer = new(sim, ScoreState);
    }

    public List<Action> Run()
    {
        int discrepancies = 0;
        // what is g(startState) = 0?
        foreach (var action in _sim.Crafter.Actions)
            _beam.TryAdd(new ActionNode(action, null, null, 0), 0);

        while (true)
        {
            List<int>? solution = Probe(0, discrepancies);
            if (solution != null) return solution.Select(x=>Atlas.Actions.AllActions[x]).ToList();
            discrepancies += 1;
        }
    }

    private List<int>? Probe(int depth, int discrepancies)
    {
        var results = NextSlice(depth, 0);
        if (results.Item2?.Count > 0) return results.Item2;
        if (discrepancies == 0)
        {
            if (results.Item1.Length == 0) return null;
            List<int>? path = Probe(depth + 1, 0);
            foreach (var s in results.Item1) _beam.Remove(s, out int _);
            return path;
        }
        else
        {
            if (results.Item1.Length == 0)
            {
                foreach (var s in results.Item1)_beam.Remove(s, out int _);
            }

            List<int>? path;
            while (true)
            {
                results = NextSlice(depth, results.Item3);
                if (results.Item2?.Count > 0) return results.Item2;
                else if (results.Item2 == null) break;
                if (!results.Item1.Any()) continue;
                path = Probe(depth + 1, discrepancies - 1);
                foreach (var s in results.Item1) _beam.Remove(s, out int _);
                if (path?.Count > 0) return path;
            }

            results = NextSlice(depth, 0);
            if (results.Item2?.Count > 0) return results.Item2;
            if (!results.Item1.Any()) return null;
            path = Probe(depth + 1, discrepancies);
            foreach (var s in results.Item1) _beam.Remove(s, out int _);
            return path;
        }
    }

    // slice, value, index
    private Tuple<ActionNode?[], List<int>?, int> NextSlice(int depth, int index)
    {
        List<ActionNode?> successors = GenerateNewSuccessors();
        if (!successors.Any() || successors.Count == index) return new(Array.Empty<ActionNode>(), null, -1);
        if (ScoreState(successors[0].State) >= 200) return new(Array.Empty<ActionNode>(), successors[0].GetPath(depth), -1);

        List<ActionNode?> tempSlice = new List<ActionNode?>();
        int i = index;

        while (i < successors.Count && tempSlice.Count < _beamSize)
        {
            if (!_beam.ContainsKey(successors[i]))
            {
                tempSlice.Add(successors[i]);
                _beam.TryAdd(successors[i], 0);
                if (_beam.Count >= _beamSize)
                {
                    foreach (var s in tempSlice) _beam.Remove(s, out int _);
                    return new(Array.Empty<ActionNode>(), null, -1);
                }
            }

            i += 1;
        }

        return new(tempSlice.ToArray(), new(), i);
    }

    private List<ActionNode?> GenerateNewSuccessors()
    {
        List<ActionNode?> successors = new();
        foreach (var kvp in _beam)
        {
            foreach (var action in _sim.Crafter.Actions)
            {
                ActionNode? successor = new(action, null, kvp.Key, kvp.Key.Depth + 1);
                if (!_beam.ContainsKey(successor))
                {
                    successors.Add(successor);
                }
            }
        }

        successors.Sort(_comparer);
        return successors.ToList();
    }
    
    private double ScoreState(LightState? state)
    {
        if (!state.HasValue) return -1;
        bool success = state.Value.Success(_sim);
        if (!success && (state.Value.Durability < 0 || state.Value.CP < 0)) return -1;

        double progress = (state.Value.Progress > _sim.Recipe.Difficulty ? _sim.Recipe.Difficulty : state.Value.Progress) / _sim.Recipe.Difficulty; // max 100
        double maxQuality = _sim.Recipe.MaxQuality * 1.1;
        double quality = (state.Value.Quality > maxQuality ? maxQuality : state.Value.Quality) / _sim.Recipe.MaxQuality; // max 110

        return (progress + quality) * 100; // max 210
    }

    private class ActionNodeComparer : IComparer<ActionNode>
    {
        private LightSimulator _sim;
        private Scorer _scorer;

        public delegate double Scorer(LightState? state);

        public ActionNodeComparer(LightSimulator sim, Scorer scorer)
        {
            _sim = sim;
            _scorer = scorer;
        }

        public int Compare(ActionNode x, ActionNode y)
        {
            x.State ??= x.Parent.State == null ? _sim.Simulate(x.Action!.Value) : _sim.Simulate(x.Action!.Value, x.Parent.State.Value);
            y.State ??= y.Parent.State == null ? _sim.Simulate(y.Action!.Value) : _sim.Simulate(y.Action!.Value, y.Parent.State.Value);

            LightState? xState = x.State,yState = y.State;
            return _scorer(xState).CompareTo(_scorer(yState));
        }
    }
}

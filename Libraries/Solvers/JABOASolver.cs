using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Runtime;
using static Libraries.Solver;
namespace Libraries.Solvers;

public class JABOASolver
{
	private const double MaxDurabilityCost = 88 / 30D;
	private const double MinDurabilityCost = 98 / 160D;
	private const int MaxProgressListCount = 15000;
	private const int ProgressSetMaxLength = 7;
	private const int MaxQualityListCount = 10000000;
	private const int QualitySetMaxLength = 12;
	private const int DurabilityLossThreshold = 8;

	private readonly LightSimulator _sim;
	private readonly LightState _startState;
	private readonly LoggingDelegate _logger;

	private readonly int[] _progressActions, _qualityActions;
	private readonly KeyValuePair<int, double>[] _durabilityActions;
	private bool _merging;

	private int _countdown = 4;
	private readonly object _lockObject = new();

	private readonly ConcurrentBag<KeyValuePair<double, List<int>>> _scoredProgressLists = new();
	private List<List<int>> _progressLists = new();
	private readonly ConcurrentBag<KeyValuePair<int, List<int>>> _scoredQualityLists = new();
	private ConcurrentQueue<KeyValuePair<int, List<int>>> _qualityQueue = new();

	private KeyValuePair<double, List<int>>? _bestSolution;

	public JABOASolver(LightSimulator sim, LoggingDelegate loggingDelegate)
	{
		_sim = sim;
		_logger = loggingDelegate;
		_startState = _sim.Simulate(new List<int>())!.Value;

		_progressActions =
			Atlas.Actions.ProgressActions.Where(x => _sim.Crafter.Actions.Contains(x)).ToArray();

		var q =
			Atlas.Actions.QualityActions.Where(x => _sim.Crafter.Actions.Contains(x)).ToList();
		if (sim.PureLevelDifference < 10 || sim.Recipe.IsExpert) q.Remove((int)Atlas.Actions.ActionMap.TrainedEye);
		q.Remove((int)Atlas.Actions.ActionMap.DelicateSynthesis); // todo: put this back in
		_qualityActions = q.ToArray();

		_durabilityActions = Atlas.Actions.DurabilityActions.Where(x => _sim.Crafter.Actions.Contains(x.Key)).ToArray();
	}

	public List<Action>? Run(int maxTasks)
	{
		int cp = _sim.Crafter.CP + (int)(_sim.Recipe.Durability * MaxDurabilityCost);

		_logger($"\n[{DateTime.Now}] Generating Progress Combinations");
		GenerateProgressTree(cp);
		_progressLists = _scoredProgressLists.OrderBy(x => x.Key).Take(MaxProgressListCount).Select(x => x.Value).ToList();
		_scoredProgressLists.Clear();
		int cpCost = (int)ListToCpCost(_progressLists.First());
		cp = _sim.Crafter.CP + (int)(_sim.Recipe.Durability * MaxDurabilityCost) - cpCost;
		_logger($"\t{string.Join(",", _progressLists.First().Select(x => Atlas.Actions.AllActions[x].ShortName))}");
		_logger($"[{DateTime.Now}] CP Cost: {cpCost}; CP Remaining: {cp}");

		_logger($"\n[{DateTime.Now}] Generating Quality Combinations");
		GenerateQualityTree(cp);

		return _bestSolution?.Value.Select(x => Atlas.Actions.AllActions[x]).ToList();
	}

	#region Progress Tree
	private void GenerateProgressTree(int cpLimit)
	{
		DfsState state = new();
		foreach (var action in _progressActions)
		{
			state.Add(GenerateProgressTreeInner(new(null, _startState, null!), action, 0, ProgressSetMaxLength, cpLimit));
		}
		_logger($"[{DateTime.Now}] Nodes Generated: {state.NodesGenerated} | Solutions Found: {state.SolutionCount}");
	}
	private DfsState GenerateProgressTreeInner(ActionNode node, int action, double prevScore, int remainingDepth, int cpLimit)
	{
		DfsState dfsState = new DfsState();
		if (remainingDepth <= 0) return dfsState;

		LightState? state = _sim.Simulate(action, node.State!.Value, false);
		if (state == null) return dfsState;

		double score = ScoreState(state, ignoreProgress: false);
		if (score < 0) return dfsState;
		if (_sim.Crafter.CP - state.Value.CP > cpLimit) return dfsState;
		if (Atlas.Actions.AllActions[action].ActiveTurns <= 0 && score <= prevScore) return dfsState;

		ActionNode newNode;
		lock (node) { newNode = node.Add(action, state.Value); }

		if (state.Value.Success(_sim))
		{
			dfsState.SolutionCount++;
			List<int> path = newNode.GetPath(state.Value.Step);
			_scoredProgressLists.Add(new((int)ListToCpCost(path), path));
		}

		dfsState.NodesGenerated++;
		foreach (int nextAction in _progressActions) dfsState.Add(GenerateProgressTreeInner(newNode, nextAction, score, remainingDepth - 1, cpLimit));
		lock (node) { node.Remove(newNode); }

		if (remainingDepth == ProgressSetMaxLength) _logger($"-> {Atlas.Actions.AllActions[action].Name} ({dfsState.NodesGenerated} generated)");
		return dfsState;
	}
	#endregion

	#region Quality Tree
	private void GenerateQualityTree(int cpLimit)
	{
		GCLatencyMode oldMode = GCSettings.LatencyMode;
		try
		{
			GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
			DfsState state = new();
			state.Add(GenerateQualityTreeThreading(new(), _startState, 0, QualitySetMaxLength, cpLimit));
			_logger($"[{DateTime.Now}] Nodes Generated: {state.NodesGenerated} | Solutions Found: {state.SolutionCount}");
		}
		finally
		{
			GCSettings.LatencyMode = oldMode;
		}
	}
	private DfsState GenerateQualityTreeThreading(List<int> node, LightState state, double prevScore, int remainingDepth, int cpLimit)
	{
		DfsState dfsState = new();
		switch (remainingDepth)
		{
			case <= 0: return dfsState;
			case > 4 when _countdown > 0:
			{
				lock (_lockObject) _countdown = Math.Max(_countdown - 1, 0);
				Thread[] threads = new Thread[_qualityActions.Length];
				for (int i = 0; i < threads.Length; i++)
				{
					int action = _qualityActions[i];
					threads[i] = new(() => dfsState.Add(GenerateQualityTreeInner(node, state, action, prevScore, remainingDepth, cpLimit)));
					threads[i].Start();
				}
				foreach (var thread in threads) thread.Join();
				lock (_lockObject) _countdown += 1;
				break;
			}
			default:
			{
				foreach (int action in _qualityActions)
				{
					dfsState.Add(GenerateQualityTreeInner(node, state, action, prevScore, remainingDepth, cpLimit));
				}
				break;
			}
		}
		return dfsState;
	}

	private DfsState GenerateQualityTreeInner(List<int> parentNode, LightState parentState, int action, double prevScore, int remainingDepth, int cpLimit)
	{
		DfsState dfsState = new DfsState();
		if (ListToCpCost(parentNode) > cpLimit) return dfsState;
		
		var state = _sim.Simulate(action, parentState, false);
		if (state == null) return dfsState;

		double score = ScoreState(state, ignoreProgress: true);
		if (score < 0) return dfsState;
		if (_sim.Crafter.CP - state.Value.CP > cpLimit) return dfsState;
		if (Atlas.Actions.AllActions[action].ActiveTurns <= 0 && score <= prevScore) return dfsState;

		List<int> newNode = new List<int>(parentNode);
		newNode.Add(action);
		dfsState.SolutionCount++;

		InlineMerger(newNode);
		
		// _scoredQualityLists.Add(new((int)score, newNode));
		// if (_scoredQualityLists.Count >= MaxQualityListCount)
		// {
		// 	IOrderedEnumerable<KeyValuePair<int, List<int>>> sorted;
		// 	lock (_scoredQualityLists)
		// 	{
		// 		sorted = _scoredQualityLists.OrderByDescending(x => x.Key);
		// 		foreach (var kvp in sorted) _qualityQueue.Enqueue(kvp);
		// 		_scoredQualityLists.Clear();
		// 		if (!_merging)
		// 		{
		// 			Console.Write(".");
		// 			_merging = true;
		// 		}
		// 	}
		// }
		// if (_merging)
		// {
		// 	while (_qualityQueue.TryDequeue(out var qualityList))
		// 	{
		// 		if (_bestSolution.HasValue && qualityList.Key - DurabilityLossThreshold < _bestSolution.Value.Key - 100) break;
		// 		InlineMerger(qualityList.Value);
		// 	}
		//
		// 	lock (_scoredQualityLists)
		// 	{
		// 		if (_merging)
		// 		{
		// 			_merging = false;
		// 			_qualityQueue.Clear();
		// 		}
		// 	}
		// }

		dfsState.Add(GenerateQualityTreeThreading(newNode, state.Value, score, remainingDepth - 1, cpLimit));
		dfsState.NodesGenerated++;

		if (remainingDepth == QualitySetMaxLength)
			_logger($"-> {Atlas.Actions.AllActions[action].Name} ({dfsState.NodesGenerated} generated)");
		return dfsState;
	}

	#endregion

	#region Merger
	
	#region Troubleshooting Variables
	ulong callCount = 0;

	ulong progressLoops = 0;
	ulong notEnoughCpFirst = 0;
	ulong bothFirstRound = 0;

	ulong zipLoops = 0;
	ulong notEnoughProgressCount = 0;
	ulong craftNotFinishedFirst = 0;
	ulong zipCount = 0;
	ulong craftNotFinishedSecond = 0;
	ulong subZeroScore = 0;
	ulong belowBestScoreFirst = 0;
	ulong notEnoughCpSecond = 0;
	ulong failedSolveDurability = 0;
	ulong durabilitiesSolved = 0;
	ulong belowBestScoreSecond = 0;
	#endregion

	private bool InlineMerger(List<int> qualityList)
	{
		//return true;
		bool ret = true;
		callCount++;
		double qualityCp = ListToCpCost(qualityList);
		foreach (var progressList in _progressLists)
		{
			progressLoops++;
			double cpMax = _sim.Crafter.CP;
			double progressCp = ListToCpCost(progressList);
			if (progressCp + qualityCp >= cpMax + _sim.Recipe.Durability * MaxDurabilityCost)
			{
				notEnoughCpFirst++;
				break;
			}

			uint progressMerger = 1;
			int progressMergeLength = qualityList.Count + progressList.Count;
			uint maxProgressCombinations = (uint)Math.Pow(2, progressMergeLength);

			if (Atlas.Actions.FirstRoundActions.Contains(progressList[0]))
				progressMerger = (uint)Math.Pow(2, progressMergeLength - 1) + 1;
			if (Atlas.Actions.FirstRoundActions.Contains(qualityList[0]))
				maxProgressCombinations = (uint)Math.Pow(2, progressMergeLength - 1);
			if (progressMerger > maxProgressCombinations)
			{
				bothFirstRound++;
				continue;
			}

			do
			{
				zipLoops++;
				if (System.Numerics.BitOperations.PopCount(progressMerger) != progressList.Count)
				{
					notEnoughProgressCount++;
					continue;
				}
				if (!TestSpacedProgress(progressList, progressMerger, progressMergeLength))
				{
					craftNotFinishedFirst++;
					continue;
				}

				zipCount++;
				List<int> preDurabilityActions = ZipLists(qualityList, progressList, progressMerger, progressMergeLength);

				LightState? progressState = _sim.Simulate(preDurabilityActions, false);
				if (!progressState.HasValue || progressState.Value.Progress < _sim.Recipe.Difficulty)
				{
					craftNotFinishedSecond++;
					continue;
				}

				double progressScore = ScoreState(progressState);
				if (progressScore < 0)
				{
					subZeroScore++;
					continue;
				}
				if (_bestSolution != null && progressScore - DurabilityLossThreshold <= _bestSolution.Value.Key)
				{
					belowBestScoreFirst++;
					continue;
				}
				if (cpMax - ListToCpCost(preDurabilityActions, false) - MinDurabilityCpCost(preDurabilityActions, cpMax) < 0)
				{
					notEnoughCpSecond++;
					break;
				}
				if (!MinMaxSolveDurability(preDurabilityActions, progressState.Value.CP, out var postDurabilityActions))
				{
					failedSolveDurability++;
					continue;
				}

				durabilitiesSolved++;
				LightState? postDurabilityState = _sim.Simulate(postDurabilityActions!);
				double postDurabilityScore = ScoreState(postDurabilityState);
				lock (_lockObject)
				{
					if (_bestSolution != null && postDurabilityScore <= _bestSolution.Value.Key)
					{
						belowBestScoreSecond++;
						continue;
					}

					if (_bestSolution == null || postDurabilityScore - 1 > _bestSolution.Value.Key)
					{
						_logger($"[{DateTime.Now}] New Best Solution Found ({postDurabilityScore}):");
						_logger($"\t{string.Join(",", postDurabilityActions!.Select(x => Atlas.Actions.AllActions[x].ShortName))}");
					}

					_bestSolution = new(postDurabilityScore, postDurabilityActions!);
				}
			} while ((progressMerger += 2) < maxProgressCombinations);
		}

		return ret;
	}

	private bool TestSpacedProgress(List<int> progressList, uint merger, int length)
	{
		double progress = 0;
		bool muMe = false;
		int venerationRounds = 0;
		int ix = 0;

		for (int i = length - 1; i >= 0; i--)
		{
			if (((merger >> i) & 1) == 1) // 1 indicates its a progress action
			{
				int action = progressList[ix++];

				progress += Atlas.Actions.AllActions[action].ProgressIncreaseMultiplier * _sim.BaseProgressIncrease * (1 + (muMe ? 1 : 0) + (venerationRounds > 0 ? 0.5 : 0));
				switch (action)
				{
					case (int)Atlas.Actions.ActionMap.MuscleMemory:
						muMe = true;
						break;
					case (int)Atlas.Actions.ActionMap.Veneration:
						venerationRounds = Atlas.Actions.AllActions[action].ActiveTurns + 1;
						break;
					default:
					{
						if (Atlas.Actions.AllActions[action].ProgressIncreaseMultiplier > 0)
						{
							muMe = false;
						}
						break;
					}
				}

				if (progress >= _sim.Recipe.Difficulty) return true;
			}

			venerationRounds--;
		}

		return false;
	}
	private bool MinMaxSolveDurability(List<int> actions, double cpMax, out List<int>? durabilityActions)
	{
		durabilityActions = null;
		double baseDurabilityCost = GetDurabilityCost(actions, out int stopIx);
		if (stopIx == actions.Count)
		{
			durabilityActions = actions;
			return true;
		}

		List<KeyValuePair<double, List<int>>> durabilityLists = new();
		foreach (var durabilityAction in _durabilityActions)
		{
			if (cpMax < Atlas.Actions.AllActions[durabilityAction.Key].CPCost) continue;
			for (int i = 0; i < stopIx; i++)
			{
				actions.Insert(i, durabilityAction.Key);
				try
				{
					LightState? s = _sim.Simulate(actions, useDurability: false);
					if (s == null) continue;

					double newDurabilityCost = GetDurabilityCost(actions, out int _);
					if (newDurabilityCost >= baseDurabilityCost) continue;

					//double durabilitySavings = (baseDurabilityCost - newDurabilityCost) / durabilityAction.Key.CPCost;
					durabilityLists.Add(new(cpMax - Atlas.Actions.AllActions[durabilityAction.Key].CPCost, actions.ToList()));
				}
				finally
				{
					actions.RemoveAt(i);
				}
			}
		}

		double bestScore = 0;
		foreach (var durabilityList in durabilityLists)
		{
			if (!MinMaxSolveDurability(durabilityList.Value, durabilityList.Key, out var solved)) continue;

			LightState? s = _sim.Simulate(solved!, useDurability: true);
			if (s == null || !s.Value.Success(_sim)) continue;

			double newScore = ScoreState(s);
			if (!(newScore > bestScore)) continue;

			bestScore = newScore;
			durabilityActions = solved;
		}

		return bestScore > 0;
	}
	private double MinDurabilityCpCost(List<int> actions, double cpMax)
	{
		double remainingCp = cpMax;
		List<int> chosenActions = actions.ToList();

		do
		{
			int? chosenAction = null;
			List<int>? bestActions = null;
			double baseDurabilityCost = GetDurabilityCost(chosenActions, out int _);
			if (baseDurabilityCost == 0) return 0;

			double bestEfficiency = 0;
			foreach (var durabilityAction in _durabilityActions)
			{
				if (remainingCp < Atlas.Actions.AllActions[durabilityAction.Key].CPCost) continue;
				for (int i = 0; i < chosenActions.Count; i++)
				{
					chosenActions.Insert(i, durabilityAction.Key);
					try
					{
						double newDurabilityCost = GetDurabilityCost(chosenActions, out int _);
						if (newDurabilityCost >= baseDurabilityCost) continue;

						double newEfficiency = (baseDurabilityCost - newDurabilityCost) / Atlas.Actions.AllActions[durabilityAction.Key].CPCost;
						if (newEfficiency <= bestEfficiency) continue;

						LightState? s = _sim.Simulate(chosenActions, useDurability: false);
						if (s == null) continue;

						bestEfficiency = newEfficiency;
						chosenAction = durabilityAction.Key;
						bestActions = chosenActions.ToList();
					}
					finally
					{
						chosenActions.RemoveAt(i);
					}
				}
			}

			if (chosenAction == null) return cpMax + 1;
			remainingCp -= Atlas.Actions.AllActions[chosenAction.Value].CPCost;

			if (bestActions == null) continue;
			chosenActions = bestActions.ToList();

			LightState? x = _sim.Simulate(bestActions, useDurability: true);
			if (x == null) continue;

			return cpMax - remainingCp;
		} while (true);
	}
	private static List<int> ZipLists(List<int> left, List<int> right, uint merger, int length)
	{
		int l = left.Count, r = right.Count;
		List<int> actions = new(length);
		for (int i = 0; i < length; i++)
		{
			actions.Insert(0, ((merger >> i) & 1) == 0 ? left[--l] : right[--r]);
		}
		return actions;
	}
	#endregion

	#region Utilities

	private double ScoreState(LightState? state, bool ignoreProgress = false)
	{
		if (!state.HasValue) return -1;
		bool success = state.Value.Success(_sim);
		if (!success && (state.Value.Durability < 0 || state.Value.CP < 0)) return -1;

		double progress = ignoreProgress ? 0
			: (state.Value.Progress > _sim.Recipe.Difficulty ? _sim.Recipe.Difficulty : state.Value.Progress) / _sim.Recipe.Difficulty; // max 100
		double maxQuality = _sim.Recipe.MaxQuality * 1.1;
		double quality = (state.Value.Quality > maxQuality ? maxQuality : state.Value.Quality) / _sim.Recipe.MaxQuality; // max 110

		double cp = state.Value.CP / _sim.Crafter.CP;
		double steps = (100 - state.Value.Step) / 100D;
		double extraCredit = 0;//(cp + steps) * 10 + (success ? 20 : 0); // max 40

		return (progress + quality) * 100 + extraCredit; // max 250
	}

	private static double ListToCpCost(List<int> list, bool considerDurability = true)
	{
		double cpTotal = list.Sum(x => Atlas.Actions.AllActions[x].CPCost);
		double durabilityTotal = considerDurability
			? list.Sum(x => Atlas.Actions.AllActions[x].DurabilityCost) * MinDurabilityCost
			: 0;
		int observeCost =
			(list.Count(x => x == (int)Atlas.Actions.ActionMap.FocusedSynthesis) +
			 list.Count(x => x == (int)Atlas.Actions.ActionMap.FocusedTouch)) *
			Atlas.Actions.AllActions[(int)Atlas.Actions.ActionMap.Observe].CPCost;

		int comboSavings = 0;
		int index = 0;
		while ((index = list.FindIndex(index, x => x == (int)Atlas.Actions.ActionMap.StandardTouch)) >= 0)
		{
			if (index > 0 && list[index - 1] == (int)Atlas.Actions.ActionMap.BasicTouch)
			{
				comboSavings += Atlas.Actions.AllActions[(int)Atlas.Actions.ActionMap.StandardTouch].CPCost -
				                Atlas.Actions.AllActions[(int)Atlas.Actions.ActionMap.BasicTouch].CPCost;
				if (list.Count > index + 1 && list[index + 1] == (int)Atlas.Actions.ActionMap.AdvancedTouch)
				{
					comboSavings += Atlas.Actions.AllActions[(int)Atlas.Actions.ActionMap.AdvancedTouch].CPCost -
					                Atlas.Actions.AllActions[(int)Atlas.Actions.ActionMap.BasicTouch].CPCost;
				}
			}

			index++;
		}

		return cpTotal + observeCost + durabilityTotal - comboSavings;
	}

	private double GetDurabilityCost(List<int> actions, out int stopIx)
	{
		double dur = _sim.Recipe.Durability;
		double cost = 0;
		int wnRounds = 0;
		int manipRounds = 0;
		stopIx = actions.Count;

		for (int i = 0; i < actions.Count - 1; i++)
		{
			var action = actions[i];
			switch (action)
			{
				case (int)Atlas.Actions.ActionMap.WasteNot:
					wnRounds = 4;
					break;
				case (int)Atlas.Actions.ActionMap.WasteNot2:
					wnRounds = 8;
					break;
				case (int)Atlas.Actions.ActionMap.Manipulation:
					manipRounds = 8;
					break;
				case (int)Atlas.Actions.ActionMap.MastersMend:
					cost -= 30;
					dur += 30;
					break;
				default:
					cost += wnRounds > 0
						? Atlas.Actions.AllActions[action].DurabilityCost / 2
						: Atlas.Actions.AllActions[action].DurabilityCost;
					dur -= wnRounds > 0
						? Atlas.Actions.AllActions[action].DurabilityCost / 2
						: Atlas.Actions.AllActions[action].DurabilityCost;
					break;
			}

			if (dur <= 0 && stopIx > i)
				stopIx = i;

			wnRounds--;
			if (action != (int)Atlas.Actions.ActionMap.Manipulation) manipRounds--;
			if (manipRounds <= 0) continue;

			cost -= 5;
			dur += 5;
		}

		return cost;
	}

	#endregion
}

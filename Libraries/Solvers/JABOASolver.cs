using System.Collections.Concurrent;
using System.Runtime;
using static Libraries.Solver;

namespace Libraries.Solvers
{
	// Just a Bunch of Actions
	public class JaboaSolver : ISolver
	{
		private const double MaxDurabilityCost = 88 / 30D;
		private const double MinDurabilityCost = 98 / 160D;
		private const int MaxQualityListCount = 8000000;

		private const int ProgressSetMaxLength = 7;
		private const int QualitySetMaxLength = 14;

		private readonly LightSimulator _sim;
		private readonly LightState _startState;
		private readonly LoggingDelegate _logger;

		private readonly Action[] _progressActions, _qualityActions;
		private readonly KeyValuePair<Action, double>[] _durabilityActions;

		private int _countdown = 4;
		private readonly object _lockObject = new();
		private bool _merging = false;

		private readonly ConcurrentBag<KeyValuePair<double, List<Action>>> _scoredProgressLists = new();
		private readonly ConcurrentBag<KeyValuePair<double, List<Action>>> _scoredQualityLists = new();
		private readonly ConcurrentQueue<KeyValuePair<double, List<Action>>> _qualityQueue = new();
		private List<List<Action>> _progressLists = new();
		private KeyValuePair<double, List<Action>>? _bestSolution;

		public JaboaSolver(LightSimulator sim, LoggingDelegate loggingDelegate)
		{
			_sim = sim;
			_logger = loggingDelegate;
			_startState = _sim.Simulate(new List<Action>())!.Value;
			
			_progressActions = Atlas.Actions.ProgressActions.Where(x => _sim.Crafter.Actions.Contains(x))
				.Concat(Atlas.Actions.ProgressBuffs.Where(y => _sim.Crafter.Actions.Contains(y))).ToArray();

			var q = Atlas.Actions.QualityActions.Where(x => _sim.Crafter.Actions.Contains(x))
				.Concat(Atlas.Actions.QualityBuffs.Where(y => _sim.Crafter.Actions.Contains(y))).ToList();
			if (sim.PureLevelDifference < 10 || sim.Recipe.IsExpert) q.Remove(Atlas.Actions.TrainedEye);
			q.Remove(Atlas.Actions.DelicateSynthesis); // todo: put this back in
			_qualityActions = q.ToArray();

			_durabilityActions = Atlas.Actions.DurabilityActions.Where(x => _sim.Crafter.Actions.Contains(x.Key)).ToArray();
		}

		public List<Action>? Run(int maxTasks)
		{
			int cp = _sim.Crafter.CP + (int)(_sim.Recipe.Durability * MaxDurabilityCost);

			_logger($"\n[{DateTime.Now}] Generating Progress Combinations");
			GenerateDfsActionTree(_progressActions, ProgressFailure, ProgressSuccess, ProgressSuccessCallback, ProgressScore, ProgressSetMaxLength, cp, ignoreProgress: false);
			_progressLists = _scoredProgressLists.OrderBy(x => x.Key).Select(x => x.Value).ToList();
			int cpCost = (int)ListToCpCost(_progressLists.First());
			cp = _sim.Crafter.CP + (int)(_sim.Recipe.Durability * MaxDurabilityCost) - cpCost;
			_logger($"\t{string.Join(",", _progressLists.First().Select(x => x.ShortName))}");
			_logger($"[{DateTime.Now}] CP Cost: {cpCost}; CP Remaining: {cp}");

			_logger($"\n[{DateTime.Now}] Generating Quality Combinations");
			GenerateDfsActionTree(_qualityActions, QualityFailure, QualitySuccess, QualitySuccessCallback, QualityScore, QualitySetMaxLength, cp, ignoreProgress: true);

			return _bestSolution?.Value;
		}
		
		#region Combination Generator
		#region Delegates
		private delegate bool SuccessCondition(LightState state, double score);
		private bool ProgressSuccess(LightState state, double score)
		{
			return state.Success(_sim);
		}
		private bool QualitySuccess(LightState state, double score)
		{
			return _bestSolution == null || score - _bestSolution.Value.Key > 1;
		}

		private delegate void SuccessCallback(double score, List<Action> actions);
		private void ProgressSuccessCallback(double score, List<Action> actions)
		{
			_scoredProgressLists.Add(new(score, actions));
		}
		private void QualitySuccessCallback(double score, List<Action> actions)
		{
			if (_bestSolution != null && _bestSolution.Value.Key - 140 > score) return;
			InlineMerger(actions);

			// blocking this off for now, testing the inline merge again
			// lock (_scoredQualityLists)
			// {
			// 	_scoredQualityLists.Add(new KeyValuePair<double, List<Action>>(score, actions));
			// 	if (!_merging && _scoredQualityLists.Count > MaxQualityListCount)
			// 	{
			// 		_merging = true;
			// 		_logger("Merging");

			// 		if (_qualityQueue.IsEmpty)
			// 		{
			// 			foreach (var kvp in _scoredQualityLists.OrderByDescending(x => x.Key))
			// 			{
			// 				_qualityQueue.Enqueue(kvp);
			// 			}
			// 			_scoredQualityLists.Clear();
			// 		}
			// 	}
			// }

			// if (_merging) Merger();
		}

		private delegate bool FailureCondition(LightState state);
		private bool ProgressFailure(LightState state)
		{
			return false;
		}
		private bool QualityFailure(LightState state)
		{
			return false;
		}

		private delegate double NodeScore(List<Action> path, double score);
		private double ProgressScore(List<Action> path, double score)
		{
			return ListToCpCost(path);
		}
		private double QualityScore(List<Action> path, double score)
		{
			return score;
		}
		#endregion

		private void GenerateDfsActionTree(Action[] actions, FailureCondition failureCondition, SuccessCondition successCondition, SuccessCallback successCallback, NodeScore nodeScore, int maxLength, double cpLimit, bool ignoreProgress)
		{
			GCLatencyMode oldMode = GCSettings.LatencyMode;
			ActionNode head = new ActionNode(null, _startState, null!);
			try
			{
				GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
				var s = SubDfsActionTree(head, actions, failureCondition, successCondition, successCallback, nodeScore, 0, maxLength, maxLength, cpLimit, ignoreProgress);
				_logger($"[{DateTime.Now}] Nodes Generated: {s.NodesGenerated} | Solutions Found: {s.SolutionCount}");
			}
			finally
			{
				GCSettings.LatencyMode = oldMode;
			}
		}
		private DfsState SubDfsActionTree(ActionNode node, Action[] actions, FailureCondition failureCondition, SuccessCondition successCondition, SuccessCallback successCallback, NodeScore nodeScore, double prevScore, int maxLength, int remainingDepth, double cpLimit, bool ignoreProgress)
		{
			DfsState state = new();
			switch (remainingDepth)
			{
				case <= 0: return state;
				case > 4 when _countdown > 0:
				{
					lock (_lockObject) _countdown = Math.Max(_countdown - 1, 0);
					Thread[] threads = new Thread[actions.Length];
					for (int i = 0; i < threads.Length; i++)
					{
						Action action = actions[i];
						threads[i] = new Thread(() => state.Add(SubDfsActionTreeInner(node, action, actions, failureCondition, successCondition, successCallback, nodeScore, prevScore, maxLength, remainingDepth, cpLimit, ignoreProgress)));
						threads[i].Start();
					}
					foreach (var thread in threads) thread.Join();
					lock (_lockObject) _countdown += 1;
					break;
				}
				default:
				{
					foreach (var action in actions)
					{
						state.Add(SubDfsActionTreeInner(node, action, actions, failureCondition, successCondition, successCallback, nodeScore, prevScore, maxLength, remainingDepth, cpLimit, ignoreProgress));
					}
					break;
				}
			}
			return state;
		}
		private DfsState SubDfsActionTreeInner(ActionNode node, Action action, Action[] actions, FailureCondition failureCondition, SuccessCondition successCondition, SuccessCallback successCallback, NodeScore nodeScore, double prevScore, int maxLength, int remainingDepth, double cpLimit, bool ignoreProgress)
		{
			var dfsState = new DfsState();
			LightState? state = _sim.Simulate(action, node.State, false);
			if (state == null || failureCondition(state.Value)) return dfsState;

			double score = ScoreState(state, ignoreProgress: ignoreProgress);
			if (score < 0) return dfsState;
			if (_sim.Crafter.CP - state.Value.CP > cpLimit) return dfsState;
			if (action.ActiveTurns <= 0 && score <= prevScore) return dfsState;

			ActionNode newNode;
			lock (node)
			{
				newNode = node.Add(action, state.Value);
			}
			
			bool doNextLevel = true;
			if (successCondition(state.Value, score))
			{
				dfsState.SolutionCount++;
				List<Action> path = node.GetPath(state.Value.Step - 1);
				path.Add(action);
				//var s = _sim.Simulate(path, false);
				successCallback((int)nodeScore(path, score), path);
			}

			dfsState.NodesGenerated++;
			dfsState.Add(SubDfsActionTree(newNode, actions, failureCondition, successCondition, successCallback, nodeScore, score, maxLength, remainingDepth - 1, cpLimit, ignoreProgress));

			lock (newNode.Parent)
			{
				newNode.Parent.Remove(newNode);
			}

			if (remainingDepth == maxLength) _logger($"-> {action.Name} ({dfsState.NodesGenerated} generated)");
			return dfsState;
		}
		#endregion

		#region Merger
		private void Merger()
		{
			while (_qualityQueue.TryDequeue(out var qualityList))
			{
				if (_bestSolution != null && _bestSolution.Value.Key - 140 > qualityList.Key) continue;

				for (int i = 0; i < _progressLists.Count; i++)
				{
					List<Action> progressList = _progressLists[i];
					double cpMax = _sim.Crafter.CP;
					if (cpMax - ListToCpCost(qualityList.Value, false) - ListToCpCost(progressList, false) - MinDurabilityCPCost(qualityList.Value, cpMax) < 0) break;

					uint progressMerger = 1;
					int progressLength = qualityList.Value.Count + progressList.Count;
					uint maxProgressCombinations = (uint)Math.Pow(2, progressLength);

					if (Atlas.Actions.FirstRoundActions.Contains(progressList[0]))
					{
						progressMerger = (uint)Math.Pow(2, progressLength - 1) + 1;
					}
					if (Atlas.Actions.FirstRoundActions.Contains(qualityList.Value[0]))
					{
						maxProgressCombinations = (uint)Math.Pow(2, progressLength - 1);
					}
					if (progressMerger > maxProgressCombinations) continue;

					do
					{
						if (System.Numerics.BitOperations.PopCount(progressMerger) != progressList.Count) continue;

						List<Action> preDurabilityActions = ZipLists(qualityList.Value, progressList, progressMerger, progressLength);
						if (cpMax - ListToCpCost(preDurabilityActions, false) - MinDurabilityCPCost(preDurabilityActions, cpMax) < 0) break;

						LightState? progressState = _sim.Simulate(preDurabilityActions, false);
						if (!progressState.HasValue || progressState.Value.Progress < _sim.Recipe.Difficulty) continue;

						double progressScore = ScoreState(progressState);
						if (progressScore < 0) continue;
						if (_bestSolution != null && progressScore - _bestSolution.Value.Key < 3) continue;

						// insert durability
						if (MinMaxSolveDurability(preDurabilityActions, progressState.Value.CP, out var postDurabilityActions))
						{
							double newScore = ScoreState(_sim.Simulate(postDurabilityActions!));
							lock (_lockObject)
							{
								if (_bestSolution != null && newScore - _bestSolution.Value.Key < 3) continue;

								_logger($"[{DateTime.Now}] New Best Solution Found ({newScore}):");
								_logger($"\t{string.Join(",", postDurabilityActions!.Select(x => x.ShortName))}");
								_bestSolution = new(newScore, postDurabilityActions!);
							}
						}
					} while ((progressMerger += 2) < maxProgressCombinations);
				}
			}

			_merging = false;
		}

		private void InlineMerger(List<Action> qualityList)
		{
			for (int i = 0; i < _progressLists.Count; i++)
			{
				List<Action> progressList = _progressLists[i];
				double cpMax = _sim.Crafter.CP;
				if (cpMax - ListToCpCost(qualityList, false) - ListToCpCost(progressList, false) - MinDurabilityCPCost(qualityList, cpMax) < 0) break;

				uint progressMerger = 1;
				int progressLength = qualityList.Count + progressList.Count;
				uint maxProgressCombinations = (uint)Math.Pow(2, progressLength);

				if (Atlas.Actions.FirstRoundActions.Contains(progressList[0]))
				{
					progressMerger = (uint)Math.Pow(2, progressLength - 1) + 1;
				}
				if (Atlas.Actions.FirstRoundActions.Contains(qualityList[0]))
				{
					maxProgressCombinations = (uint)Math.Pow(2, progressLength - 1);
				}
				if (progressMerger > maxProgressCombinations) continue;

				do
				{
					if (System.Numerics.BitOperations.PopCount(progressMerger) != progressList.Count) continue;

					List<Action> preDurabilityActions = ZipLists(qualityList, progressList, progressMerger, progressLength);
					if (cpMax - ListToCpCost(preDurabilityActions, false) - MinDurabilityCPCost(preDurabilityActions, cpMax) < 0) break;

					LightState? progressState = _sim.Simulate(preDurabilityActions, false);
					if (!progressState.HasValue || progressState.Value.Progress < _sim.Recipe.Difficulty) continue;

					double progressScore = ScoreState(progressState);
					if (progressScore < 0) continue;
					if (_bestSolution != null && progressScore - _bestSolution.Value.Key < 3) continue;

					// insert durability
					if (MinMaxSolveDurability(preDurabilityActions, progressState.Value.CP, out var postDurabilityActions))
					{
						double newScore = ScoreState(_sim.Simulate(postDurabilityActions!));
						lock (_lockObject)
						{
							if (_bestSolution != null && newScore - _bestSolution.Value.Key < 3) continue;

							_logger($"[{DateTime.Now}] New Best Solution Found ({newScore}):");
							_logger($"\t{string.Join(",", postDurabilityActions!.Select(x => x.ShortName))}");
							_bestSolution = new(newScore, postDurabilityActions!);
						}
					}
				} while ((progressMerger += 2) < maxProgressCombinations);
			}
		}
		
		private bool MinMaxSolveDurability(List<Action> actions, double cpMax, out List<Action>? durabilityActions)
		{
			durabilityActions = null;
			double baseDurabilityCost = GetDurabilityCost(actions, out int stopIx);
			if (stopIx == actions.Count)
			{
				durabilityActions = actions;
				return true;
			}

			List<KeyValuePair<double, List<Action>>> durabilityLists = new();
			foreach (var durabilityAction in _durabilityActions)
			{
				if (cpMax < durabilityAction.Key.CPCost) continue;
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
						durabilityLists.Add(new(cpMax - durabilityAction.Key.CPCost, actions.ToList()));
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
		private double MinDurabilityCPCost(List<Action> actions, double cpMax)
		{
			double remainingCp = cpMax;
			List<Action> chosenActions = actions.ToList();

			do
			{
				Action? chosenAction = null;
				List<Action>? bestActions = null;
				double baseDurabilityCost = GetDurabilityCost(chosenActions, out int _);
				double bestEfficiency = 0;
				foreach (var durabilityAction in _durabilityActions)
				{
					if (remainingCp < durabilityAction.Key.CPCost) continue;
					for (int i = 0; i < chosenActions.Count; i++)
					{
						chosenActions.Insert(i, durabilityAction.Key);
						try
						{
							double newDurabilityCost = GetDurabilityCost(chosenActions, out int _);
							if (newDurabilityCost >= baseDurabilityCost) continue;

							double newEfficiency = (baseDurabilityCost - newDurabilityCost) / durabilityAction.Key.CPCost;
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
				remainingCp -= chosenAction.CPCost;

				if (bestActions == null) continue;
				chosenActions = bestActions.ToList();

				LightState? x = _sim.Simulate(bestActions, useDurability: true);
				if (x == null) continue;

				return cpMax - remainingCp;
			} while (true);
		}
		#endregion

		#region Utilities
		private double ScoreState(LightState? state, bool ignoreProgress = false)
		{
			if (!state.HasValue) return -1;
			bool success = state.Value.Success(_sim);
			if (!success)
			{
				var violations = state.Value.CheckViolations(_sim);
				if (!violations.DurabilityOk || !violations.CpOk) return -1;
			}

			double progress = ignoreProgress ? 0 : (state.Value.Progress > _sim.Recipe.Difficulty ? _sim.Recipe.Difficulty : state.Value.Progress) / _sim.Recipe.Difficulty;
			double maxQuality = _sim.Recipe.MaxQuality * 1.1;
			double quality = (state.Value.Quality > maxQuality ? maxQuality : state.Value.Quality) / _sim.Recipe.MaxQuality;

			double cp = state.Value.CP / _sim.Crafter.CP;
			double steps = (100 - state.Value.Step) / 100D;
			double extraCredit = (cp + steps) * 10;

			return (progress + quality) * 100 + extraCredit + (success ? 20 : 0);
		}

		private static double ListToCpCost(List<Action> list, bool considerDurability = true)
		{
			double cpTotal = list.Sum(x => x.CPCost);
			double durabilityTotal = considerDurability ? list.Sum(x => x.DurabilityCost) * MinDurabilityCost : 0;
			int observeCost = (list.Count(x => x.Equals(Atlas.Actions.FocusedSynthesis)) + list.Count(x => x.Equals(Atlas.Actions.FocusedTouch))) * Atlas.Actions.Observe.CPCost;

			int comboSavings = 0;
			int index = 0;
			while ((index = list.FindIndex(index, x => x.Equals(Atlas.Actions.StandardTouch))) >= 0)
			{
				if (index > 0 && list[index - 1].Equals(Atlas.Actions.BasicTouch))
				{
					comboSavings += Atlas.Actions.StandardTouch.CPCost - Atlas.Actions.BasicTouch.CPCost;
					if (list.Count > index + 1 && list[index + 1].Equals(Atlas.Actions.AdvancedTouch))
					{
						comboSavings += Atlas.Actions.AdvancedTouch.CPCost - Atlas.Actions.BasicTouch.CPCost;
					}
				}
				index++;
			}

			return cpTotal + observeCost + durabilityTotal - comboSavings;
		}

		private double GetDurabilityCost(List<Action> actions, out int stopIx)
		{
			double dur = _sim.Recipe.Durability;
			double cost = 0;
			int wnRounds = 0;
			int manipRounds = 0;
			stopIx = actions.Count;

			for (int i = 0; i < actions.Count - 1; i++)
			{
				var action = actions[i];
				if (action.Equals(Atlas.Actions.WasteNot))
				{
					wnRounds = 4;
				}
				else if (action.Equals(Atlas.Actions.WasteNot2))
				{
					wnRounds = 8;
				}
				else if (action.Equals(Atlas.Actions.Manipulation))
				{
					manipRounds = 8;
				}
				else if (action.Equals(Atlas.Actions.MastersMend))
				{
					cost -= 30;
					dur += 30;
				}
				else
				{
					cost += wnRounds > 0 ? action.DurabilityCost / 2 : action.DurabilityCost;
					dur -= wnRounds > 0 ? action.DurabilityCost / 2 : action.DurabilityCost;
				}

				if (dur <= 0 && stopIx > i)
					stopIx = i;

				wnRounds--;
				if (!action.Equals(Atlas.Actions.Manipulation)) manipRounds--;
				if (manipRounds > 0)
				{
					cost -= 5;
					dur += 5;
				}
			}

			return cost;
		}
		private static List<Action> ZipLists(List<Action> left, List<Action> right, uint merger, int length)
		{
			int l = left.Count, r = right.Count;
			List<Action> actions = new(length);
			for (int i = 0; i < length; i++)
			{
				actions.Insert(0, ((merger >> i) & 1) == 0 ? left[--l] : right[--r]);
			}
			return actions;
		}
		#endregion
	}
}

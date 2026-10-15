using System.Collections.Generic;
using MMDPlayerForVR.Pose.Application;
using MMDPlayerForVR.Pose.Domain;
using UnityEngine;

namespace MMDPlayerForVR.Pose.Infrastructure
{
    /// <summary>変形階層とボーンインデックス順で付与変形とIKを評価する。</summary>
    public class PoseTransformEvaluator : IPoseTransformEvaluator
    {
        private const string LogPrefix = "[PoseEval]";

        private readonly IAppendTransformSolver _appendTransformSolver;
        private readonly IIkSolver _ikSolver;
        private readonly Dictionary<int, int> _evaluationRanks = new Dictionary<int, int>();
        private readonly HashSet<string> _warnedKeys = new HashSet<string>();

        public PoseTransformEvaluator(IAppendTransformSolver appendTransformSolver, IIkSolver ikSolver, IPoseEvaluationContext context)
        {
            _appendTransformSolver = appendTransformSolver;
            _ikSolver = ikSolver;

            if (context?.EvaluationSteps == null) return;
            for (int i = 0; i < context.EvaluationSteps.Count; i++)
            {
                BoneEvaluationStep step = context.EvaluationSteps[i];
                if (!_evaluationRanks.ContainsKey(step.BoneIndex))
                {
                    _evaluationRanks.Add(step.BoneIndex, i);
                }
            }
        }

        /// <inheritdoc/>
        public void Evaluate(IPoseEvaluationContext context, IReadOnlyDictionary<string, bool> ikStates)
        {
            if (context?.EvaluationSteps == null) return;

            foreach (BoneEvaluationStep step in context.EvaluationSteps)
            {
                if (step.AppendTransform != null)
                {
                    WarnIfAppendParentIsEvaluatedLater(step.AppendTransform);
                    _appendTransformSolver?.Apply(step.AppendTransform);
                }

                if (step.IkChain != null)
                {
                    _ikSolver?.Solve(step.IkChain, ikStates);
                }
            }
        }

        private void WarnIfAppendParentIsEvaluatedLater(AppendTransform appendTransform)
        {
            if (!_evaluationRanks.TryGetValue(appendTransform.BoneIndex, out int boneRank)) return;
            if (!_evaluationRanks.TryGetValue(appendTransform.ParentBoneIndex, out int parentRank)) return;
            if (parentRank <= boneRank) return;

            string key = $"order:{appendTransform.BoneName}";
            if (_warnedKeys.Add(key))
            {
                Debug.LogWarning($"{LogPrefix} '{appendTransform.BoneName}' の付与親 '{appendTransform.ParentBoneName}' は付与先より後に評価されます。");
            }
        }
    }
}

using System.Collections.Generic;
using MMDPlayerForVR.PmxImporter.Core;
using MMDPlayerForVR.Pose.Application;
using MMDPlayerForVR.Pose.Domain;

namespace MMDPlayerForVR.Pose.Infrastructure
{
    /// <summary>PMXボーン情報を、付与変形とIKの統合評価順序へ変換する。</summary>
    public class PoseEvaluationContextAdapter : IPoseEvaluationContext
    {
        /// <inheritdoc/>
        public IReadOnlyList<BoneEvaluationStep> EvaluationSteps { get; }

        public PoseEvaluationContextAdapter(PmxBone[] pmxBones, IReadOnlyList<IkChain> ikChains)
        {
            var ikByBoneIndex = new Dictionary<int, IkChain>();
            if (ikChains != null)
            {
                foreach (var chain in ikChains)
                {
                    if (!ikByBoneIndex.ContainsKey(chain.IkBoneIndex))
                    {
                        ikByBoneIndex.Add(chain.IkBoneIndex, chain);
                    }
                }
            }

            var steps = new List<BoneEvaluationStep>(pmxBones.Length);
            for (int i = 0; i < pmxBones.Length; i++)
            {
                PmxBone bone = pmxBones[i];
                ikByBoneIndex.TryGetValue(i, out IkChain ikChain);
                steps.Add(new BoneEvaluationStep
                {
                    BoneIndex = i,
                    BoneName = bone.Name,
                    DeformLayer = bone.DeformLayer,
                    AppendTransform = CreateAppendTransform(pmxBones, bone, i),
                    IkChain = ikChain,
                });
            }

            steps.Sort((a, b) =>
            {
                int cmp = a.DeformLayer.CompareTo(b.DeformLayer);
                return cmp != 0 ? cmp : a.BoneIndex.CompareTo(b.BoneIndex);
            });

            EvaluationSteps = steps;
        }

        private static AppendTransform CreateAppendTransform(PmxBone[] pmxBones, PmxBone bone, int boneIndex)
        {
            bool rotationAppend = (bone.Flags & PmxBoneFlags.RotationAppend) != 0;
            bool movementAppend = (bone.Flags & PmxBoneFlags.MovementAppend) != 0;
            if (!rotationAppend && !movementAppend)
            {
                return null;
            }

            string parentName = bone.AppendParentBoneIndex >= 0 && bone.AppendParentBoneIndex < pmxBones.Length
                ? pmxBones[bone.AppendParentBoneIndex].Name
                : string.Empty;

            return new AppendTransform
            {
                BoneIndex = boneIndex,
                BoneName = bone.Name,
                ParentBoneIndex = bone.AppendParentBoneIndex,
                ParentBoneName = parentName,
                Ratio = bone.AppendRatio,
                AppliesRotation = rotationAppend,
                AppliesTranslation = movementAppend,
                IsLocal = (bone.Flags & PmxBoneFlags.LocalAppend) != 0,
                DeformLayer = bone.DeformLayer,
            };
        }
    }
}

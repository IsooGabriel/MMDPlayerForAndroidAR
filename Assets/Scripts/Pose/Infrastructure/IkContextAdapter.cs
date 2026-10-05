using System.Collections.Generic;
using System.Linq;
using MMDPlayerForVR.Pose.Application;
using MMDPlayerForVR.Pose.Domain;
using MMDPlayerForVR.PmxImporter.Core;

namespace MMDPlayerForVR.Pose.Infrastructure
{
    public class IkContextAdapter : IIkContext
    {
        public IReadOnlyList<IkChain> IkChains { get; }

        public IkContextAdapter(PmxBone[] pmxBones)
        {
            var chains = new List<IkChain>();
            for (int i = 0; i < pmxBones.Length; i++)
            {
                var bone = pmxBones[i];
                if (bone.Ik == null) continue;

                var links = bone.Ik.Links.Select((l, idx) => new IkLinkData
                {
                    BoneIndex = l.BoneIndex,
                    BoneName = l.BoneIndex >= 0 && l.BoneIndex < pmxBones.Length ? pmxBones[l.BoneIndex].Name : string.Empty,
                    HasAngleLimit = l.HasAngleLimit,
                    LowerLimit = l.LowerLimit,
                    UpperLimit = l.UpperLimit,
                }).ToArray();

                chains.Add(new IkChain
                {
                    IkBoneIndex = i,
                    IkBoneName = bone.Name,
                    TargetBoneIndex = bone.Ik.TargetBoneIndex,
                    TargetBoneName = bone.Ik.TargetBoneIndex >= 0 && bone.Ik.TargetBoneIndex < pmxBones.Length
                        ? pmxBones[bone.Ik.TargetBoneIndex].Name : string.Empty,
                    LoopCount = bone.Ik.LoopCount,
                    LimitAngleRadians = bone.Ik.LimitAngleRadians,
                    Links = links,
                    DeformLayer = bone.DeformLayer,
                });
            }

            // 変形階層→インデックス順にソート
            chains.Sort((a, b) =>
            {
                int cmp = a.DeformLayer.CompareTo(b.DeformLayer);
                return cmp != 0 ? cmp : a.IkBoneIndex.CompareTo(b.IkBoneIndex);
            });

            IkChains = chains;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using MMDPlayerForVR.Pose.Application;
using MMDPlayerForVR.Pose.Domain;
using MMDPlayerForVR.PmxImporter.Core;
using UnityEngine;

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

                var links = bone.Ik.Links.Select((l, idx) =>
                {
                    var limit = ConvertLimit(l.LowerLimit, l.UpperLimit);
                    return new IkLinkData
                    {
                        BoneIndex = l.BoneIndex,
                        BoneName = l.BoneIndex >= 0 && l.BoneIndex < pmxBones.Length ? pmxBones[l.BoneIndex].Name : string.Empty,
                        HasAngleLimit = l.HasAngleLimit,
                        LowerLimit = limit.Lower,
                        UpperLimit = limit.Upper,
                        IsXAxisOnlyLimit = IsXAxisOnlyLimit(l.LowerLimit, l.UpperLimit),
                    };
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

        private static (Vector3 Lower, Vector3 Upper) ConvertLimit(Vector3 lowerRadians, Vector3 upperRadians)
        {
            Vector3 lower = new Vector3(
                ConvertAxisLimit(lowerRadians.x, Vector3.right),
                ConvertAxisLimit(lowerRadians.y, Vector3.up),
                ConvertAxisLimit(lowerRadians.z, Vector3.forward));
            Vector3 upper = new Vector3(
                ConvertAxisLimit(upperRadians.x, Vector3.right),
                ConvertAxisLimit(upperRadians.y, Vector3.up),
                ConvertAxisLimit(upperRadians.z, Vector3.forward));

            return (Vector3.Min(lower, upper), Vector3.Max(lower, upper));
        }

        private static float ConvertAxisLimit(float radians, Vector3 axis)
        {
            Quaternion mmdRotation = Quaternion.AngleAxis(radians * Mathf.Rad2Deg, axis);
            Quaternion unityRotation = MmdCoordinateConverter.ToUnityRotation(mmdRotation);
            unityRotation.ToAngleAxis(out float angle, out Vector3 convertedAxis);

            angle = NormalizeAngle(angle);
            if (Vector3.Dot(convertedAxis, axis) < 0f)
            {
                angle = -angle;
            }

            return NormalizeAngle(angle);
        }

        private static bool IsXAxisOnlyLimit(Vector3 lowerRadians, Vector3 upperRadians)
        {
            const float Epsilon = 1e-5f;
            bool hasXLimit = Mathf.Abs(lowerRadians.x - upperRadians.x) > Epsilon;
            bool yIsFixed = Mathf.Abs(lowerRadians.y) <= Epsilon && Mathf.Abs(upperRadians.y) <= Epsilon;
            bool zIsFixed = Mathf.Abs(lowerRadians.z) <= Epsilon && Mathf.Abs(upperRadians.z) <= Epsilon;
            return hasXLimit && yIsFixed && zIsFixed;
        }

        private static float NormalizeAngle(float angle)
        {
            angle = Mathf.Repeat(angle + 180f, 360f) - 180f;
            return angle == -180f ? 180f : angle;
        }
    }
}

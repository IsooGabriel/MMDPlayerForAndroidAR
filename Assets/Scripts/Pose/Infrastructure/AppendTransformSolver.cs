using System.Collections.Generic;
using MMDPlayerForVR.Pose.Application;
using MMDPlayerForVR.Pose.Domain;
using UnityEngine;

namespace MMDPlayerForVR.Pose.Infrastructure
{
    /// <summary>Transformの現在姿勢にPMX付与変形を適用する。</summary>
    public class AppendTransformSolver : IAppendTransformSolver
    {
        private const string LogPrefix = "[Append]";
        private const float ZeroRatioEpsilon = 1e-6f;

        private readonly Dictionary<string, Transform> _boneMap;
        private readonly Dictionary<string, (Vector3 Position, Quaternion Rotation)> _rest;
        private readonly HashSet<string> _warnedKeys = new HashSet<string>();

        public AppendTransformSolver(Dictionary<string, Transform> boneMap)
        {
            _boneMap = boneMap;
            _rest = new Dictionary<string, (Vector3 Position, Quaternion Rotation)>(boneMap.Count);

            foreach (var kv in boneMap)
            {
                _rest[kv.Key] = (kv.Value.localPosition, kv.Value.localRotation);
            }
        }

        /// <inheritdoc/>
        public void Apply(AppendTransform appendTransform)
        {
            if (appendTransform == null) return;
            if (Mathf.Abs(appendTransform.Ratio) <= ZeroRatioEpsilon) return;
            if (appendTransform.ParentBoneIndex < 0) return;
            if (appendTransform.ParentBoneIndex == appendTransform.BoneIndex)
            {
                WarnOnce($"self:{appendTransform.BoneName}", $"{LogPrefix} '{appendTransform.BoneName}' の付与親が自分自身のためスキップします。");
                return;
            }

            if (appendTransform.IsLocal)
            {
                WarnOnce($"local:{appendTransform.BoneName}", $"{LogPrefix} '{appendTransform.BoneName}' はローカル付与ですが、通常付与として扱います。");
            }

            if (!_boneMap.TryGetValue(appendTransform.BoneName, out Transform target))
            {
                WarnOnce($"target:{appendTransform.BoneName}", $"{LogPrefix} 付与先ボーン '{appendTransform.BoneName}' が見つかりません。");
                return;
            }

            if (!_boneMap.TryGetValue(appendTransform.ParentBoneName, out Transform parent))
            {
                WarnOnce($"parent:{appendTransform.BoneName}", $"{LogPrefix} '{appendTransform.BoneName}' の付与親 '{appendTransform.ParentBoneName}' が見つかりません。");
                return;
            }

            if (!_rest.TryGetValue(appendTransform.BoneName, out var targetRest)
                || !_rest.TryGetValue(appendTransform.ParentBoneName, out var parentRest))
            {
                return;
            }

            if (appendTransform.AppliesTranslation)
            {
                Vector3 parentMove = parent.localPosition - parentRest.Position;
                target.localPosition = target.localPosition + parentMove * appendTransform.Ratio;
            }

            if (appendTransform.AppliesRotation)
            {
                Quaternion parentDelta = Quaternion.Inverse(parentRest.Rotation) * parent.localRotation;
                Quaternion appendRotation = Quaternion.SlerpUnclamped(Quaternion.identity, Normalize(parentDelta), appendTransform.Ratio);
                Quaternion result = target.localRotation * Normalize(appendRotation);

                if (IsFinite(result))
                {
                    target.localRotation = Normalize(result);
                }
                else
                {
                    target.localRotation = targetRest.Rotation;
                    WarnOnce($"nan:{appendTransform.BoneName}", $"{LogPrefix} '{appendTransform.BoneName}' の回転付与で不正な値を検出したため基準回転に戻しました。");
                }
            }
        }

        private void WarnOnce(string key, string message)
        {
            if (_warnedKeys.Add(key))
            {
                Debug.LogWarning(message);
            }
        }

        private static Quaternion Normalize(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
            if (magnitude <= Mathf.Epsilon || float.IsNaN(magnitude))
            {
                return Quaternion.identity;
            }

            float inv = 1f / magnitude;
            return new Quaternion(value.x * inv, value.y * inv, value.z * inv, value.w * inv);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

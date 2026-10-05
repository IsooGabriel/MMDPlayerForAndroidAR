using System.Collections.Generic;
using UnityEngine;
using MMDPlayerForVR.Pose.Application;
using MMDPlayerForVR.Pose.Domain;

namespace MMDPlayerForVR.Pose.Infrastructure
{
    /// <summary>
    /// CCD法によるIKソルバー。FK適用後のTransformに対してインプレースで解く。
    /// GCAllocを避けるため、Transformの配列は毎回newするが将来的には事前確保に変更できる。
    /// </summary>
    public class CcdIkSolver : IIkSolver
    {
        /// <summary>IKが到達したとみなす許容誤差（Unityワールド空間、スケール0.1を考慮）</summary>
        private const float ConvergenceThreshold = 1e-4f;

        private const string LogPrefix = "[IK]";

        private readonly Dictionary<string, Transform> _boneMap;

        public CcdIkSolver(Dictionary<string, Transform> boneMap)
        {
            _boneMap = boneMap;
        }

        /// <inheritdoc/>
        public void Solve(IReadOnlyList<IkChain> chains, IReadOnlyDictionary<string, bool> ikStates)
        {
            if (chains == null) return;

            foreach (var chain in chains)
            {
                Solve(chain, ikStates);
            }
        }

        /// <inheritdoc/>
        public void Solve(IkChain chain, IReadOnlyDictionary<string, bool> ikStates)
        {
            if (chain == null) return;

            // IKがオフならスキップ
            if (ikStates != null && ikStates.TryGetValue(chain.IkBoneName, out bool on) && !on)
                return;

            try
            {
                SolveChain(chain);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} チェーン '{chain.IkBoneName}' の解決中にエラー（スキップ）: {ex.Message}");
            }
        }

        private void SolveChain(IkChain chain)
        {
            // ターゲット（IKボーン）のTransform取得
            if (!_boneMap.TryGetValue(chain.IkBoneName, out Transform ikBoneTransform))
            {
                Debug.LogWarning($"{LogPrefix} IKボーン '{chain.IkBoneName}' がboneMapに見つかりません");
                return;
            }

            // エフェクター（足首）のTransform取得
            if (!_boneMap.TryGetValue(chain.TargetBoneName, out Transform effectorTransform))
            {
                Debug.LogWarning($"{LogPrefix} エフェクターボーン '{chain.TargetBoneName}' がboneMapに見つかりません");
                return;
            }

            // リンクのTransform取得
            var links = new Transform[chain.Links.Length];
            for (int i = 0; i < chain.Links.Length; i++)
            {
                var linkData = chain.Links[i];
                if (!_boneMap.TryGetValue(linkData.BoneName, out Transform linkTransform))
                {
                    Debug.LogWarning($"{LogPrefix} リンクボーン '{linkData.BoneName}' がboneMapに見つかりません");
                    return;
                }
                links[i] = linkTransform;
            }

            float limitAngleDeg = chain.LimitAngleRadians * Mathf.Rad2Deg;

            int iterUsed = 0;

            // CCD反復
            for (int iter = 0; iter < chain.LoopCount; iter++)
            {
                iterUsed = iter + 1;

                // エフェクター（足首）のワールド位置
                Vector3 effectorWorld = effectorTransform.position;
                // ターゲット（IKボーン）のワールド位置
                Vector3 targetWorld = ikBoneTransform.position;

                // 収束チェック（ループ先頭）
                if (Vector3.Distance(effectorWorld, targetWorld) < ConvergenceThreshold)
                    break;

                // 各リンクを処理（エフェクターに近い側から）
                for (int li = 0; li < links.Length; li++)
                {
                    Transform link = links[li];
                    var linkData = chain.Links[li];

                    // リンクのローカル空間に変換
                    Vector3 toEffector = link.InverseTransformPoint(effectorTransform.position).normalized;
                    Vector3 toTarget = link.InverseTransformPoint(ikBoneTransform.position).normalized;

                    // NaNチェック（ゼロベクトルへのnormalized等）
                    if (float.IsNaN(toEffector.x) || float.IsNaN(toTarget.x))
                        continue;

                    Quaternion stepRotation = linkData.HasAngleLimit && linkData.IsXAxisOnlyLimit
                        ? CreateXAxisLimitedStepRotation(toEffector, toTarget, limitAngleDeg)
                        : CreateStepRotation(toEffector, toTarget, limitAngleDeg);

                    if (stepRotation == Quaternion.identity)
                        continue;

                    // リンクのlocalRotationに回転を掛ける
                    link.localRotation = link.localRotation * stepRotation;

                    // 角度制限があれば適用
                    if (linkData.HasAngleLimit)
                    {
                        ApplyAngleLimit(link, linkData);
                    }
                }

                // 収束チェック（ループ末尾）
                effectorWorld = effectorTransform.position;
                targetWorld = ikBoneTransform.position;
                if (Vector3.Distance(effectorWorld, targetWorld) < ConvergenceThreshold)
                    break;
            }

            // [IK] 5.6: IKの結果ログ（ターゲット、反復回数、最終距離）
            float finalDist = Vector3.Distance(effectorTransform.position, ikBoneTransform.position);
            Debug.Log($"{LogPrefix} Solved '{chain.IkBoneName}': effector='{chain.TargetBoneName}', iters={iterUsed}/{chain.LoopCount}, finalDist={finalDist:F5}");
        }

        private static void ApplyAngleLimit(Transform link, IkLinkData linkData)
        {
            if (linkData.IsXAxisOnlyLimit)
            {
                float angle = GetSignedAxisAngle(link.localRotation, Vector3.right);
                angle = Mathf.Clamp(NormalizeAngle(angle), linkData.LowerLimit.x, linkData.UpperLimit.x);
                link.localRotation = Quaternion.AngleAxis(angle, Vector3.right);
                return;
            }

            link.localRotation.ToAngleAxis(out float rawAngle, out Vector3 rawAxis);
            float signedAngle = NormalizeAngle(rawAngle);
            Vector3 angleVector = rawAxis.normalized * signedAngle;
            angleVector.x = Mathf.Clamp(NormalizeAngle(angleVector.x), linkData.LowerLimit.x, linkData.UpperLimit.x);
            angleVector.y = Mathf.Clamp(NormalizeAngle(angleVector.y), linkData.LowerLimit.y, linkData.UpperLimit.y);
            angleVector.z = Mathf.Clamp(NormalizeAngle(angleVector.z), linkData.LowerLimit.z, linkData.UpperLimit.z);

            float magnitude = angleVector.magnitude;
            link.localRotation = magnitude <= Mathf.Epsilon
                ? Quaternion.identity
                : Quaternion.AngleAxis(magnitude, angleVector / magnitude);
        }

        private static Quaternion CreateStepRotation(Vector3 toEffector, Vector3 toTarget, float limitAngleDeg)
        {
            float dot = Mathf.Clamp(Vector3.Dot(toEffector, toTarget), -1f, 1f);
            float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
            angle = Mathf.Min(angle, limitAngleDeg);

            Vector3 axis = Vector3.Cross(toEffector, toTarget);
            if (axis.sqrMagnitude < 1e-12f)
                return Quaternion.identity;

            return Quaternion.AngleAxis(angle, axis);
        }

        private static Quaternion CreateXAxisLimitedStepRotation(Vector3 toEffector, Vector3 toTarget, float limitAngleDeg)
        {
            Vector3 effectorOnPlane = Vector3.ProjectOnPlane(toEffector, Vector3.right);
            Vector3 targetOnPlane = Vector3.ProjectOnPlane(toTarget, Vector3.right);
            if (effectorOnPlane.sqrMagnitude < 1e-12f || targetOnPlane.sqrMagnitude < 1e-12f)
                return Quaternion.identity;

            float angle = Vector3.SignedAngle(effectorOnPlane, targetOnPlane, Vector3.right);
            angle = Mathf.Clamp(angle, -limitAngleDeg, limitAngleDeg);
            return Quaternion.AngleAxis(angle, Vector3.right);
        }

        private static float GetSignedAxisAngle(Quaternion rotation, Vector3 axis)
        {
            Vector3 rotationVector = new Vector3(rotation.x, rotation.y, rotation.z);
            Vector3 projected = Vector3.Project(rotationVector, axis);
            Quaternion twist = new Quaternion(projected.x, projected.y, projected.z, rotation.w);
            twist = Normalize(twist);
            twist.ToAngleAxis(out float angle, out Vector3 twistAxis);
            if (Vector3.Dot(twistAxis, axis) < 0f)
            {
                angle = -angle;
            }

            return NormalizeAngle(angle);
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

        private static float NormalizeAngle(float angle)
        {
            angle = Mathf.Repeat(angle + 180f, 360f) - 180f;
            return angle == -180f ? 180f : angle;
        }
    }
}

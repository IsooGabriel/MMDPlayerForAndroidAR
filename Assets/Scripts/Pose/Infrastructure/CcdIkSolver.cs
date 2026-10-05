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
                // IKがオフならスキップ
                if (ikStates != null && ikStates.TryGetValue(chain.IkBoneName, out bool on) && !on)
                    continue;

                try
                {
                    SolveChain(chain);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"{LogPrefix} チェーン '{chain.IkBoneName}' の解決中にエラー（スキップ）: {ex.Message}");
                }
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

                    float dot = Mathf.Clamp(Vector3.Dot(toEffector, toTarget), -1f, 1f);
                    float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;

                    // 1ステップ最大回転角でクランプ
                    angle = Mathf.Min(angle, limitAngleDeg);

                    Vector3 axis = Vector3.Cross(toEffector, toTarget);
                    if (axis.sqrMagnitude < 1e-12f)
                        continue;

                    // リンクのlocalRotationに回転を掛ける
                    link.localRotation = link.localRotation * Quaternion.AngleAxis(angle, axis);

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
            // MMD座標系ラジアン→度変換
            Vector3 minDeg = linkData.LowerLimit * Mathf.Rad2Deg;
            Vector3 maxDeg = linkData.UpperLimit * Mathf.Rad2Deg;

            // ひざ等の下限が-179.9°以下の場合は伸び切り対策としてマージンを持たせる
            if (minDeg.x < -179.9f) minDeg.x = -179.9f;

            Vector3 euler = link.localEulerAngles;

            // Unity localEulerAngles は 0〜360 なので -180〜180 に正規化
            euler.x = NormalizeAngle(euler.x);
            euler.y = NormalizeAngle(euler.y);
            euler.z = NormalizeAngle(euler.z);

            euler.x = Mathf.Clamp(euler.x, minDeg.x, maxDeg.x);
            euler.y = Mathf.Clamp(euler.y, minDeg.y, maxDeg.y);
            euler.z = Mathf.Clamp(euler.z, minDeg.z, maxDeg.z);

            link.localEulerAngles = euler;
        }

        private static float NormalizeAngle(float angle) => angle > 180f ? angle - 360f : angle;
    }
}

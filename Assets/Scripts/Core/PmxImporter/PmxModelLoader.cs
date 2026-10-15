using MMDPlayerForVR.PmxImporter.Builders;
using MMDPlayerForVR.Pose.Application;
using MMDPlayerForVR.Pose.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace MMDPlayerForVR.PmxImporter
{
    /// <summary>
    /// 層A: PMXモデルのロードとポーズ適用を担う純粋なC#クラス。
    /// MonoBehaviour・PlayerLogService・VContainer に依存しない。
    /// 生成と呼び出しはアプリ層（PmxRuntimeLoader など）が行う。
    /// </summary>
    public class PmxModelLoader
    {
        private readonly PmxImporterPipeline _pipeline;
        private readonly ApplyInitialPoseUseCase _poseUseCase;
        private readonly IStreamingAssetsReader _streamingAssetsReader;

        public bool SkipLegIk { get; set; }

        public PmxModelLoader(
            PmxImporterPipeline pipeline,
            ApplyInitialPoseUseCase poseUseCase,
            IStreamingAssetsReader streamingAssetsReader)
        {
            _pipeline = pipeline;
            _poseUseCase = poseUseCase;
            _streamingAssetsReader = streamingAssetsReader;
        }

        /// <summary>
        /// PMXファイルをロードし、ポーズを適用したGameObjectを返す。
        /// </summary>
        public async Task<GameObject> LoadModelAsync(string pmxPath, string vmdPath)
        {
            if (!await _streamingAssetsReader.ExistsAsync(pmxPath))
            {
                Debug.LogWarning($"[PmxModelLoader] PMX file might not exist at path (or HEAD request failed): {pmxPath}");
            }

            Debug.Log($"[PmxModelLoader] Starting import of {pmxPath} ...");
            var stopWatch = System.Diagnostics.Stopwatch.StartNew();

            GameObject model = await _pipeline.ImportAsync(pmxPath);

            stopWatch.Stop();
            if (model == null)
            {
                Debug.LogError("[PmxModelLoader] Import failed. Please check the console logs for details.");
                return null;
            }

            Debug.Log($"[PmxModelLoader] Import completed successfully in {stopWatch.ElapsedMilliseconds} ms.");

            await ApplyInitialPose(model, vmdPath);

            return model;
        }

        private async Task ApplyInitialPose(GameObject model, string vmdPath)
        {
            if (_poseUseCase == null)
            {
                Debug.LogError("[Pose] ApplyInitialPoseUseCase is not injected.");
                return;
            }

            Transform armatureRoot = model.transform.Find("Armature");
            if (armatureRoot == null)
            {
                Debug.LogWarning("[Pose] Armature root not found, cannot apply pose.");
                return;
            }

            var boneMap = new Dictionary<string, Transform>();
            foreach (var t in armatureRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t == armatureRoot) continue;
                if (!boneMap.ContainsKey(t.name))
                {
                    boneMap.Add(t.name, t);
                }
            }

            var physicsSyncs = model.GetComponentsInChildren<PmxPhysicsSync>(true).ToList();
            var applier = new TransformPoseApplier(boneMap, physicsSyncs);

            // IK setup
            IkContextAdapter ikContext = null;
            CcdIkSolver ikSolver = null;
            PoseEvaluationContextAdapter evaluationContext = null;
            PoseTransformEvaluator transformEvaluator = null;

            var pmxDoc = _pipeline.LastImportedDocument;
            if (pmxDoc != null && pmxDoc.Bones != null)
            {
                // [IK] 5.6: 足まわりのボーン情報ログ（インデックス、親、付与親、変形階層、IKフラグ）
                string[] legBoneNames =
                {
                    "\u5de6\u8db3", "\u53f3\u8db3",                   // 左足, 右足
                    "\u5de6\u3072\u3056", "\u53f3\u3072\u3056",       // 左ひざ, 右ひざ
                    "\u5de6\u8db3\u9996", "\u53f3\u8db3\u9996",       // 左足首, 右足首
                    "\u5de6\u8db3\u5148EX", "\u53f3\u8db3\u5148EX",   // 左足先EX, 右足先EX
                    "\u5de6\u3064\u307e\u5148", "\u53f3\u3064\u307e\u5148", // 左つま先, 右つま先
                    "\u5de6\u8db3IK\u89aa", "\u53f3\u8db3IK\u89aa",   // 左足IK親, 右足IK親
                    "\u5de6\u8db3\uff29\uff2b", "\u53f3\u8db3\uff29\uff2b",     // 左足ＩＫ, 右足ＩＫ
                    "\u5de6\u3064\u307e\u5148\uff29\uff2b", "\u53f3\u3064\u307e\u5148\uff29\uff2b" // 左つま先ＩＫ, 右つま先ＩＫ
                };
                for (int i = 0; i < pmxDoc.Bones.Length; i++)
                {
                    var bone = pmxDoc.Bones[i];
                    if (System.Array.IndexOf(legBoneNames, bone.Name) >= 0)
                    {
                        bool hasAppend = (bone.Flags & MMDPlayerForVR.PmxImporter.Core.PmxBoneFlags.RotationAppend) != 0
                                      || (bone.Flags & MMDPlayerForVR.PmxImporter.Core.PmxBoneFlags.MovementAppend) != 0;
                        string appendInfo = hasAppend
                            ? $", appendParent={bone.AppendParentBoneIndex}, appendRatio={bone.AppendRatio}"
                            : "";
                        Debug.Log($"[IK] Bone[{i}] '{bone.Name}': parent={bone.ParentBoneIndex}, deformLayer={bone.DeformLayer}, hasIK={bone.Ik != null}, flags={bone.Flags}{appendInfo}");
                    }
                }

                ikContext = new IkContextAdapter(pmxDoc.Bones);
                Debug.Log($"[IK] IK chain count: {ikContext.IkChains.Count}");
                foreach (var chain in ikContext.IkChains)
                {
                    Debug.Log($"[IK] Chain '{chain.IkBoneName}' -> effector '{chain.TargetBoneName}', loops={chain.LoopCount}, links={chain.Links.Length}, deformLayer={chain.DeformLayer}");
                }

                ikSolver = new CcdIkSolver(boneMap);
                evaluationContext = new PoseEvaluationContextAdapter(pmxDoc.Bones, ikContext.IkChains);

                if (SkipLegIk)
                {
                    Debug.Log("[IK] skipLegIk=true: \u8db3IK\u7cfb\uff08\u8db3IK\u89aa\u30fb\u8db3\uff29\uff2b\u30fb\u3064\u307e\u5148\uff29\uff2b\uff09\u306eIK\u30c1\u30a7\u30fc\u30f3\u3092\u30b9\u30ad\u30c3\u30d7\uff08FK\u306e\u307f\u78ba\u8a8d\u30e2\u30fc\u30c9\uff09");
                }
            }
            else
            {
                Debug.LogWarning("[IK] PMX document unavailable. Proceeding without IK.");
            }

            // [IK] 5.6: バインドポーズ時のボーン間距離を記録（ボーン長変化検出用）
            var bindBoneLengths = new Dictionary<string, float>();
            foreach (var kv in boneMap)
            {
                Transform t = kv.Value;
                if (t.parent != null && boneMap.ContainsKey(t.parent.name))
                {
                    float dist = Vector3.Distance(t.position, t.parent.position);
                    if (!bindBoneLengths.ContainsKey(kv.Key))
                        bindBoneLengths[kv.Key] = dist;
                }
            }

            // skipLegIk=trueのときは足IK系のみオフにするデコレーターを挟む
            IIkSolver effectiveSolver = null;
            if (ikSolver != null)
            {
                effectiveSolver = SkipLegIk
                    ? (IIkSolver)new SkipLegIkSolverDecorator(ikSolver)
                    : ikSolver;
            }

            if (evaluationContext != null)
            {
                var appendSolver = new AppendTransformSolver(boneMap);
                transformEvaluator = new PoseTransformEvaluator(appendSolver, effectiveSolver, evaluationContext);
            }

            // 検証用VMDのフレーム0を適用する
            await _poseUseCase.ExecuteAsync(applier, ikContext, vmdPath, effectiveSolver, evaluationContext, transformEvaluator);

            // [IK] 5.6: IK解決後のボーン長変化ログ（メッシュ伸び検出）
            LogBoneLengthChanges(boneMap, bindBoneLengths);
        }

        /// <summary>
        /// [IK] 5.6: バインドポーズからボーン長が1%以上変化したボーンをログに出す（メッシュ伸び検出）。
        /// </summary>
        private static void LogBoneLengthChanges(
            Dictionary<string, Transform> boneMap,
            Dictionary<string, float> bindLengths)
        {
            const float ChangeThresholdRatio = 0.01f;
            bool anyChange = false;
            foreach (var kv in bindLengths)
            {
                if (!boneMap.TryGetValue(kv.Key, out var t)) continue;
                if (t.parent == null) continue;
                float current = Vector3.Distance(t.position, t.parent.position);
                float bind = kv.Value;
                if (bind < 1e-6f) continue;
                float ratio = Mathf.Abs(current - bind) / bind;
                if (ratio >= ChangeThresholdRatio)
                {
                    Debug.LogWarning($"[IK] \u30dc\u30fc\u30f3\u9577\u5909\u5316\u691c\u51fa: '{kv.Key}' bind={bind:F4} current={current:F4} \u5909\u5316\u7387={ratio * 100f:F1}%");
                    anyChange = true;
                }
            }
            if (!anyChange)
            {
                Debug.Log("[IK] \u30dc\u30fc\u30f3\u9577\u5909\u5316\u306a\u3057\uff08\u4f38\u3073\u691c\u51fa: \u5168\u30dc\u30fc\u30f31%\u672a\u6e80\uff09");
            }
        }
    }

    /// <summary>
    /// [IK] 5.6切り分け用: 足IK系のチェーンのみをスキップするデコレーター。
    /// IKソルバーの実装を変更せずにデバッグ用の無効化を実現する。
    /// </summary>
    internal sealed class SkipLegIkSolverDecorator : IIkSolver
    {
        private static readonly HashSet<string> LegIkNames = new HashSet<string>
        {
            // 左足IK親, 右足IK親
            "\u5de6\u8db3IK\u89aa", "\u53f3\u8db3IK\u89aa",
            // 左足ＩＫ, 右足ＩＫ
            "\u5de6\u8db3\uff29\uff2b", "\u53f3\u8db3\uff29\uff2b",
            // 左つま先ＩＫ, 右つま先ＩＫ
            "\u5de6\u3064\u307e\u5148\uff29\uff2b", "\u53f3\u3064\u307e\u5148\uff29\uff2b"
        };

        private readonly IIkSolver _inner;

        public SkipLegIkSolverDecorator(IIkSolver inner)
        {
            _inner = inner;
        }

        /// <inheritdoc/>
        public void Solve(
            IReadOnlyList<MMDPlayerForVR.Pose.Domain.IkChain> chains,
            IReadOnlyDictionary<string, bool> ikStates)
        {
            // 足IK系をオフにするオーバーライド辞書を作成し、内部ソルバーに渡す
            var overrideStates = new Dictionary<string, bool>();
            if (ikStates != null)
            {
                foreach (var kv in ikStates)
                    overrideStates[kv.Key] = kv.Value;
            }
            if (chains != null)
            {
                foreach (var chain in chains)
                {
                    if (LegIkNames.Contains(chain.IkBoneName))
                        overrideStates[chain.IkBoneName] = false;
                }
            }
            _inner.Solve(chains, overrideStates);
        }

        /// <inheritdoc/>
        public void Solve(
            MMDPlayerForVR.Pose.Domain.IkChain chain,
            IReadOnlyDictionary<string, bool> ikStates)
        {
            if (chain == null) return;
            if (LegIkNames.Contains(chain.IkBoneName)) return;
            _inner.Solve(chain, ikStates);
        }
    }
}

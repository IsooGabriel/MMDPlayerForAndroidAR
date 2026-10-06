using MMDPlayerForVR.Pose.Domain;
using MMDPlayerForVR.Services;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MMDPlayerForVR.Pose.Application
{
    public interface IPoseSource
    {
        /// <summary>
        /// 指定した時刻（フレーム）のポーズを取得する。
        /// MVP実装ではframe引数を無視し、常に各ボーンの最初のキーフレームを返す。
        /// </summary>
        PoseState Sample(float frame);
    }

    public interface IPoseApplier
    {
        void Apply(PoseState pose);
        /// <summary>IK解決後に物理剛体をボーン位置にスナップさせる</summary>
        void TeleportBodiesForPhysics();
    }

    public interface IVmdParser
    {
        IReadOnlyList<VmdBoneFrame> ParseBoneFrames(byte[] data);
        IReadOnlyDictionary<string, bool> ParseIkStates(byte[] data);
    }

    public interface IStreamingAssetsReader
    {
        Task<byte[]> ReadAllBytesAsync(string relativePath);
        Task<bool> ExistsAsync(string path);
    }

    /// <summary>IKを解くポート。FK適用後に呼ばれる</summary>
    public interface IIkSolver
    {
        /// <summary>IKチェーンリストに従い、Transform上でIKを解く</summary>
        void Solve(IReadOnlyList<IkChain> chains, IReadOnlyDictionary<string, bool> ikStates);

        /// <summary>単一のIKチェーンをTransform上で解く。</summary>
        void Solve(IkChain chain, IReadOnlyDictionary<string, bool> ikStates);
    }

    public interface IIkContext
    {
        IReadOnlyList<IkChain> IkChains { get; }
    }

    public interface IAppendTransformSolver
    {
        /// <summary>単一ボーンの付与変形をTransform上で適用する。</summary>
        void Apply(AppendTransform appendTransform);
    }

    public interface IPoseEvaluationContext
    {
        /// <summary>変形階層、ボーンインデックス順に整列済みの評価ステップ。</summary>
        IReadOnlyList<BoneEvaluationStep> EvaluationSteps { get; }
    }

    public interface IPoseTransformEvaluator
    {
        /// <summary>付与変形とIKを整列済み評価ステップに従って処理する。</summary>
        void Evaluate(IPoseEvaluationContext context, IReadOnlyDictionary<string, bool> ikStates);
    }

    public class ApplyInitialPoseUseCase
    {
        private readonly IStreamingAssetsReader _reader;
        private readonly IVmdParser _parser;
        private readonly IIkSolver _ikSolver;  // nullの場合はIKなし
        private readonly PlayerLogService _playerLogService;

        public ApplyInitialPoseUseCase(IStreamingAssetsReader reader, IVmdParser parser, PlayerLogService playerLogService, IIkSolver ikSolver = null)
        {
            _reader = reader;
            _parser = parser;
            _playerLogService = playerLogService;
            _ikSolver = ikSolver;
        }

        public async Task ExecuteAsync(
            IPoseApplier applier,
            IIkContext ikContext,
            string vmdRelativePath,
            IIkSolver ikSolverOverride = null,
            IPoseEvaluationContext evaluationContext = null,
            IPoseTransformEvaluator transformEvaluator = null)
        {
            // 1. VMD読み込み
            byte[] data;
            try
            {
                data = await _reader.ReadAllBytesAsync(vmdRelativePath);
            }
            catch (System.Exception ex)
            {
                _playerLogService.LogError($"[Pose] VMDファイルの読み込みに失敗しました: {vmdRelativePath}\n{ex}");
                return;
            }

            if (data == null || data.Length == 0)
            {
                _playerLogService.LogError($"[Pose] VMDファイルが空または見つかりません: {vmdRelativePath}");
                return;
            }

            // 2. ボーンフレームパース
            IReadOnlyList<VmdBoneFrame> frames;
            try
            {
                frames = _parser.ParseBoneFrames(data);
            }
            catch (System.Exception ex)
            {
                _playerLogService.LogError($"[Pose] VMDファイルのパースに失敗しました: {vmdRelativePath}\n{ex}");
                return;
            }

            _playerLogService.Log($"[Pose]frames:{frames.Count}");

            // 3. IK ON/OFF区画パース
            IReadOnlyDictionary<string, bool> ikStates;
            try
            {
                ikStates = _parser.ParseIkStates(data);
            }
            catch (System.Exception ex)
            {
                _playerLogService.LogWarning($"[IK] IK状態のパースに失敗しました（無視します）: {ex.Message}");
                ikStates = new Dictionary<string, bool>();
            }

            // 4. PoseState生成（VmdPoseSource経由）
            IPoseSource poseSource = new VmdPoseSource(frames, ikStates);
            PoseState pose = poseSource.Sample(0f);

            // 5. FK適用
            applier.Apply(pose);
            _playerLogService.Log($"[Pose]Applied {Path.GetFileName(vmdRelativePath)}");

            // 6. 付与変形とIK解決（FK適用後・物理スナップ前）
            IIkSolver effectiveSolver = ikSolverOverride ?? _ikSolver;
            if (transformEvaluator != null && evaluationContext != null)
            {
                try
                {
                    transformEvaluator.Evaluate(evaluationContext, pose.ikStates);
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[Pose] 付与/IK評価中にエラーが発生しました: {ex}");
                }
            }
            else if (effectiveSolver != null && ikContext != null)
            {
                try
                {
                    effectiveSolver.Solve(ikContext.IkChains, pose.ikStates);
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[IK] IK解決中にエラーが発生しました: {ex}");
                }
            }

            // 7. 物理スナップ
            applier.TeleportBodiesForPhysics();
        }
    }
}

using UnityEngine;
using System.Collections.Generic;

namespace MMDPlayerForVR.Pose.Domain
{
    /// <summary>
    /// VMDのボーンフレーム（1フレーム分のデータ）
    /// </summary>
    public struct VmdBoneFrame
    {
        public string name;
        public uint frame;
        public Vector3 pos;
        public Quaternion rot;
    }

    /// <summary>
    /// ボーンのポーズ（位置と回転）
    /// </summary>
    public struct BonePose
    {
        public Vector3 pos;
        public Quaternion rot;
    }

    /// <summary>
    /// モデル全体のポーズ状態。ボーン名からポーズへのマッピングを保持する。
    /// </summary>
    public class PoseState
    {
        public readonly Dictionary<string, BonePose> bones = new Dictionary<string, BonePose>();

        /// <summary>IK名 → オン/オフ。存在しない場合はオン(true)として扱う</summary>
        public readonly Dictionary<string, bool> ikStates = new Dictionary<string, bool>();

        /// <summary>
        /// VMDのフレームリストから、各ボーンの最初のキーフレーム（最小フレーム番号）を抽出してPoseStateを生成する。
        /// </summary>
        public static PoseState FromFirstFrames(IReadOnlyList<VmdBoneFrame> frames)
        {
            var best = new Dictionary<string, VmdBoneFrame>();
            foreach (var f in frames)
            {
                if (!best.TryGetValue(f.name, out var cur) || f.frame < cur.frame)
                {
                    best[f.name] = f;
                }
            }

            var pose = new PoseState();
            foreach (var kv in best)
            {
                pose.bones[kv.Key] = new BonePose { pos = kv.Value.pos, rot = kv.Value.rot };
            }
            return pose;
        }
    }
}

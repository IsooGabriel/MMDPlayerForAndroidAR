using System.Collections.Generic;
using MMDPlayerForVR.Pose.Domain;

namespace MMDPlayerForVR.Pose.Application
{
    public class VmdPoseSource : IPoseSource
    {
        private readonly PoseState _firstFramePose;

        public VmdPoseSource(IReadOnlyList<VmdBoneFrame> frames, IReadOnlyDictionary<string, bool> ikStates)
        {
            _firstFramePose = PoseState.FromFirstFrames(frames);
            foreach (var kv in ikStates)
                _firstFramePose.ikStates[kv.Key] = kv.Value;
        }

        /// <summary>
        /// 指定した時刻（フレーム）のポーズを取得する。
        /// MVP実装ではframe引数を無視し、常に各ボーンの最初のキーフレームのポーズを返す。
        /// 将来的にはこのメソッド内でフレーム間の補間処理などを実装する。
        /// </summary>
        public PoseState Sample(float frame)
        {
            return _firstFramePose;
        }
    }
}

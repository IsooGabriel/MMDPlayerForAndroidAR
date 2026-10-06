using MMDPlayerForVR.PmxImporter.Builders;
using MMDPlayerForVR.Pose.Application;
using MMDPlayerForVR.Pose.Domain;
using MMDPlayerForVR.Services;
using System.Collections.Generic;
using UnityEngine;

namespace MMDPlayerForVR.Pose.Infrastructure
{
    public class TransformPoseApplier : IPoseApplier
    {
        private readonly Dictionary<string, Transform> _bones;
        private readonly Dictionary<string, (Vector3 pos, Quaternion rot)> _rest = new Dictionary<string, (Vector3 pos, Quaternion rot)>();
        private readonly List<PmxPhysicsSync> _physicsSyncs;
        private readonly PlayerLogService _playerLogService;

        public TransformPoseApplier(Dictionary<string, Transform> boneMap, List<PmxPhysicsSync> physicsSyncs, PlayerLogService playerLogService)
        {
            _bones = boneMap;
            _physicsSyncs = physicsSyncs;
            _playerLogService = playerLogService;

            foreach (var kv in _bones)
            {
                _rest[kv.Key] = (kv.Value.localPosition, kv.Value.localRotation);
            }
        }

        public void Apply(PoseState pose)
        {
            _playerLogService.Log($"bones:{_bones.Count}");
            foreach (var kv in _bones)
            {
                var (rp, rr) = _rest[kv.Key];
                if (pose.bones.TryGetValue(kv.Key, out var bp))
                {
                    kv.Value.localPosition = rp + MmdCoordinateConverter.ToUnityPosition(bp.pos);
                    kv.Value.localRotation = rr * MmdCoordinateConverter.ToUnityRotation(bp.rot);
                }
                else
                {
                    kv.Value.localPosition = rp;
                    kv.Value.localRotation = rr;
                }
            }

            foreach (var name in pose.bones.Keys)
            {
                if (!_bones.ContainsKey(name))
                {
                    _playerLogService.LogWarning($"[Pose] 対応するボーンなし: {name}");
                }
            }
        }

        /// <summary>
        /// 物理同期オブジェクト（剛体）を現在のボーン位置にスナップさせる。
        /// IK解決後・物理シミュレーション前に呼ぶこと。
        /// </summary>
        public void TeleportBodiesForPhysics()
        {
            if (_physicsSyncs != null)
            {
                foreach (var sync in _physicsSyncs)
                {
                    sync.TeleportToBone();
                }
            }
        }
    }
}

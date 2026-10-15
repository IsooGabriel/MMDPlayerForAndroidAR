using UnityEngine;

namespace MMDPlayerForVR.Pose.Infrastructure
{
    public static class MmdCoordinateConverter
    {
        // 調査結果：
        // MMDとUnityは共に左手座標系(Y-Up, Z-Forward)であり、PmxBoneBuilderやPmxMeshBuilderでは
        // Z反転を行わずにそのまま読み込んでいる。
        // また、スケール変換はPmxImporterPipelineにてルートオブジェクト(rootObj.transform.localScale = 0.1)
        // で一括適用されているため、ボーンのローカル座標レベルでのスケール乗算は不要。

        public static Vector3 ToUnityPosition(Vector3 vmdPos)
        {
            return vmdPos;
        }

        public static Quaternion ToUnityRotation(Quaternion vmdRot)
        {
            return vmdRot;
        }
    }
}

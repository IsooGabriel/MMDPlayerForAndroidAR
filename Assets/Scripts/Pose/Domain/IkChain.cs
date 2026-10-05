using UnityEngine;

namespace MMDPlayerForVR.Pose.Domain
{
    public struct IkLinkData
    {
        public int BoneIndex;       // ボーン配列のインデックス
        public string BoneName;
        public bool HasAngleLimit;
        public Vector3 LowerLimit;  // 度、Unity座標系
        public Vector3 UpperLimit;  // 度、Unity座標系
        public bool IsXAxisOnlyLimit;
    }

    public class IkChain
    {
        public int IkBoneIndex;       // IKボーン自体のインデックス
        public string IkBoneName;
        public int TargetBoneIndex;   // エフェクター（足首）
        public string TargetBoneName;
        public int LoopCount;
        public float LimitAngleRadians;
        public IkLinkData[] Links;  // Links[0]がエフェクターに近い側
        public int DeformLayer;     // 変形階層（解決順序に使用）
    }
}

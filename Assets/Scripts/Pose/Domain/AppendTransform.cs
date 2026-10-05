using UnityEngine;

namespace MMDPlayerForVR.Pose.Domain
{
    /// <summary>PMXボーンの付与変形設定。</summary>
    public class AppendTransform
    {
        /// <summary>付与を受けるボーンのインデックス。</summary>
        public int BoneIndex;

        /// <summary>付与を受けるボーン名。</summary>
        public string BoneName;

        /// <summary>付与親ボーンのインデックス。</summary>
        public int ParentBoneIndex;

        /// <summary>付与親ボーン名。</summary>
        public string ParentBoneName;

        /// <summary>付与率。負の値も許容する。</summary>
        public float Ratio;

        /// <summary>回転付与を行うか。</summary>
        public bool AppliesRotation;

        /// <summary>移動付与を行うか。</summary>
        public bool AppliesTranslation;

        /// <summary>ローカル付与フラグが立っているか。</summary>
        public bool IsLocal;

        /// <summary>変形階層。</summary>
        public int DeformLayer;
    }

    /// <summary>変形階層とボーンインデックスで整列済みの評価ステップ。</summary>
    public class BoneEvaluationStep
    {
        /// <summary>ボーンのインデックス。</summary>
        public int BoneIndex;

        /// <summary>ボーン名。</summary>
        public string BoneName;

        /// <summary>変形階層。</summary>
        public int DeformLayer;

        /// <summary>このボーンに設定された付与変形。無い場合はnull。</summary>
        public AppendTransform AppendTransform;

        /// <summary>このボーンをIKボーンとするIKチェーン。無い場合はnull。</summary>
        public IkChain IkChain;
    }
}

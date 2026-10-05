using UnityEngine;

namespace MMDPlayerForVR.PmxImporter.Builders
{
    /// <summary>
    /// PMXの物理演算モードに応じて、ボーンと剛体（Proxy）のTransformを同期するコンポーネント。
    /// </summary>
    public class PmxPhysicsSync : MonoBehaviour
    {
        public Transform BoneTransform;
        public Rigidbody ProxyRigidbody;
        public int PhysicsMode;

        private Vector3 _boneToProxyPosOffset;
        private Quaternion _boneToProxyRotOffset;
        
        private Vector3 _proxyToBonePosOffset;
        private Quaternion _proxyToBoneRotOffset;

        public void Initialize(Transform bone, Rigidbody proxy, int mode)
        {
            BoneTransform = bone;
            ProxyRigidbody = proxy;
            PhysicsMode = mode;
            
            // Bone -> Proxy へのオフセット
            _boneToProxyPosOffset = Quaternion.Inverse(bone.rotation) * (proxy.position - bone.position);
            _boneToProxyRotOffset = Quaternion.Inverse(bone.rotation) * proxy.rotation;
            
            // Proxy -> Bone へのオフセット
            _proxyToBonePosOffset = Quaternion.Inverse(proxy.rotation) * (bone.position - proxy.position);
            _proxyToBoneRotOffset = Quaternion.Inverse(proxy.rotation) * bone.rotation;
        }

        void FixedUpdate()
        {
            if (PhysicsMode == 0) // 0: ボーン追従 (Kinematic)
            {
                // ボーンの動きを剛体（Proxy）に反映させる
                Vector3 targetPos = BoneTransform.position + BoneTransform.rotation * _boneToProxyPosOffset;
                Quaternion targetRot = BoneTransform.rotation * _boneToProxyRotOffset;
                
                ProxyRigidbody.MovePosition(targetPos);
                ProxyRigidbody.MoveRotation(targetRot);
            }
        }

        void LateUpdate()
        {
            if (PhysicsMode != 0) // 1: 物理演算, 2: 物理+ボーン合わせ (Dynamic)
            {
                // 剛体（Proxy）の動きをボーンに反映させる
                Vector3 targetPos = ProxyRigidbody.position + ProxyRigidbody.rotation * _proxyToBonePosOffset;
                Quaternion targetRot = ProxyRigidbody.rotation * _proxyToBoneRotOffset;
                
                BoneTransform.position = targetPos;
                BoneTransform.rotation = targetRot;
            }
        }
        public void TeleportToBone()
        {
            if (ProxyRigidbody != null && BoneTransform != null)
            {
                Vector3 targetPos = BoneTransform.position + BoneTransform.rotation * _boneToProxyPosOffset;
                Quaternion targetRot = BoneTransform.rotation * _boneToProxyRotOffset;
                
                ProxyRigidbody.position = targetPos;
                ProxyRigidbody.rotation = targetRot;
            }
        }
    }
}

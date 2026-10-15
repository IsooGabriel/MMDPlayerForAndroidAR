using UnityEngine;
using UnityEngine.Events;

namespace MMDPlayerForVR.PmxImporter.Builders
{
    /// <summary>
    /// 剛体に対してハンドトラッキング（または他の指定レイヤー）の接触を検知し、アクションを呼び出すためのコンポーネント。
    /// Dynamic剛体はCollision、Kinematic剛体はTriggerを使用して検知する。
    /// </summary>
    public class PmxContactDetector : MonoBehaviour
    {
        [Tooltip("接触を検知する対象のレイヤーマスク（通常は手にあたるレイヤーを指定します）")]
        public LayerMask TargetLayerMask = -1; // デフォルトはEverything

        public UnityEvent<Collider> OnContactEnter = new UnityEvent<Collider>();
        public UnityEvent<Collider> OnContactExit = new UnityEvent<Collider>();

        // Dynamic剛体用（非トリガーコライダー同士の衝突）
        private void OnCollisionEnter(Collision collision)
        {
            if (IsTargetLayer(collision.gameObject.layer))
            {
                OnContactEnter.Invoke(collision.collider);
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            if (IsTargetLayer(collision.gameObject.layer))
            {
                OnContactExit.Invoke(collision.collider);
            }
        }

        // Kinematic剛体用（検知用に追加されたトリガーコライダーとの接触）
        private void OnTriggerEnter(Collider other)
        {
            if (IsTargetLayer(other.gameObject.layer))
            {
                OnContactEnter.Invoke(other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsTargetLayer(other.gameObject.layer))
            {
                OnContactExit.Invoke(other);
            }
        }

        private bool IsTargetLayer(int layer)
        {
            return (TargetLayerMask.value & (1 << layer)) != 0;
        }
    }
}

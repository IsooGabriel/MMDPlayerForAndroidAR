using System.Collections.Generic;
using UnityEngine;
using MMDPlayerForVR.PmxImporter.Core;

namespace MMDPlayerForVR.PmxImporter.Builders
{
    public class PmxPhysicsBuilder : IPmxPhysicsBuilder
    {
        private const float MIN_COLLIDER_SIZE = 0.0001f;
        private const float MIN_RIGIDBODY_MASS = 0.0001f;
        private const float JOINT_LOCK_THRESHOLD = 1e-5f;

        public void Build(PmxDocument doc, Transform armatureRoot, Transform[] boneTransforms)
        {
            Transform modelRoot = armatureRoot.parent;
            float scaleCoef = modelRoot != null ? modelRoot.lossyScale.x : 0.1f;

            // スケールの影響を受けない独立した物理階層 (PhysicsRoot) を作成
            GameObject physicsRootGo = new GameObject("PhysicsRoot");
            physicsRootGo.transform.SetParent(modelRoot, false);
            // 親のスケール（例: 0.1）を打ち消してワールドスケールを1.0にする
            physicsRootGo.transform.localScale = Vector3.one / scaleCoef;

            Dictionary<int, Rigidbody> bodyMap = new Dictionary<int, Rigidbody>();
            List<(Collider col, PmxRigidBody data)> allColliders = new List<(Collider, PmxRigidBody)>();

            // 1. 剛体の構築
            for (int i = 0; i < doc.RigidBodies.Length; i++)
            {
                List<Collider> cols = new List<Collider>();
                Rigidbody rb = BuildRigidBody(doc.RigidBodies[i], boneTransforms, modelRoot, physicsRootGo.transform, scaleCoef, cols);
                if (rb != null)
                {
                    bodyMap[i] = rb;
                    foreach (var col in cols)
                    {
                        allColliders.Add((col, doc.RigidBodies[i]));
                    }
                }
            }

            // 2. コリジョンマトリクスの構築（非衝突設定）
            for (int i = 0; i < allColliders.Count; i++)
            {
                for (int j = i + 1; j < allColliders.Count; j++)
                {
                    var a = allColliders[i];
                    var b = allColliders[j];
                    
                    bool aCanCollideWithB = (a.data.CollisionMask & (1 << b.data.CollisionGroup)) != 0;
                    bool bCanCollideWithA = (b.data.CollisionMask & (1 << a.data.CollisionGroup)) != 0;
                    
                    if (!aCanCollideWithB || !bCanCollideWithA)
                    {
                        Physics.IgnoreCollision(a.col, b.col, true);
                    }
                }
            }

            // 3. ジョイントの構築
            for (int i = 0; i < doc.Joints.Length; i++)
            {
                BuildJoint(doc.Joints[i], bodyMap, modelRoot, scaleCoef);
            }
        }

        private Rigidbody BuildRigidBody(PmxRigidBody rbData, Transform[] boneTransforms, Transform modelRoot, Transform physicsRoot, float scaleCoef, List<Collider> outCols)
        {
            if (rbData.RelatedBoneIndex < 0 || rbData.RelatedBoneIndex >= boneTransforms.Length) return null;

            Transform boneTransform = boneTransforms[rbData.RelatedBoneIndex];
            if (boneTransform == null) return null;

            // 物理ルート直下にプロキシを作成（スケール1.0）
            GameObject rbGo = new GameObject($"[RB] {rbData.Name}");
            rbGo.transform.SetParent(physicsRoot, worldPositionStays: false);
            rbGo.transform.localScale = Vector3.one;

            if (modelRoot != null)
            {
                rbGo.transform.position = modelRoot.TransformPoint(rbData.Position);
                rbGo.transform.rotation = modelRoot.rotation * Quaternion.Euler(rbData.RotationEuler * Mathf.Rad2Deg);
            }
            else
            {
                rbGo.transform.position = rbData.Position * scaleCoef;
                rbGo.transform.rotation = Quaternion.Euler(rbData.RotationEuler * Mathf.Rad2Deg);
            }

            Rigidbody rb = rbGo.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(MIN_RIGIDBODY_MASS, rbData.Mass);
            rb.linearDamping = Mathf.Max(0f, rbData.LinearDamping);
            rb.angularDamping = Mathf.Max(0f, rbData.AngularDamping);
            rb.isKinematic = (rbData.PhysicsMode == 0);
            
            // 剛体のすり抜け防止対策（とくにスカートや髪の毛など、高速で動いたり薄いコライダー用）
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // 通常のコライダー
            Collider mainCol = ConfigureCollider(rbGo, rbData, scaleCoef);
            if (mainCol != null) outCols.Add(mainCol);

            // Kinematic剛体（ボーン追従）の場合、衝突が発生しないため、検知用のTriggerColliderを追加する
            if (rbData.PhysicsMode == 0)
            {
                Collider triggerCol = ConfigureCollider(rbGo, rbData, scaleCoef);
                if (triggerCol != null)
                {
                    triggerCol.isTrigger = true;
                    outCols.Add(triggerCol);
                }
            }

            // 同期スクリプトを追加
            PmxPhysicsSync sync = rbGo.AddComponent<PmxPhysicsSync>();
            sync.Initialize(boneTransform, rb, rbData.PhysicsMode);

            // 接触検知（ハンドトラッキング連携等）スクリプトを追加
            PmxContactDetector contactDetector = rbGo.AddComponent<PmxContactDetector>();
            // TargetLayerMask は後からプレハブや外部のマネージャー経由で設定できるように public で公開しています

            return rb;
        }

        private void BuildJoint(PmxJoint jData, Dictionary<int, Rigidbody> bodyMap, Transform modelRoot, float scaleCoef)
        {
            if (!bodyMap.TryGetValue(jData.RigidBodyIndexA, out Rigidbody connectedBody) ||
                !bodyMap.TryGetValue(jData.RigidBodyIndexB, out Rigidbody targetBody))
            {
                return;
            }

            ConfigurableJoint joint = targetBody.gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = connectedBody;

            Vector3 anchorWorld = modelRoot != null ? modelRoot.TransformPoint(jData.Position) : jData.Position * scaleCoef;
            joint.anchor = targetBody.transform.InverseTransformPoint(anchorWorld);

            ApplyLinearLimits(joint, jData, scaleCoef);
            ApplyAngularLimits(joint, jData);
            ApplySpringDrives(joint, jData);
        }

        private void ApplyLinearLimits(ConfigurableJoint joint, PmxJoint jData, float scaleCoef)
        {
            Vector3 limitMin = jData.PositionLimitMin * scaleCoef;
            Vector3 limitMax = jData.PositionLimitMax * scaleCoef;

            joint.xMotion = ResolveLinearMotion(limitMin.x, limitMax.x);
            joint.yMotion = ResolveLinearMotion(limitMin.y, limitMax.y);
            joint.zMotion = ResolveLinearMotion(limitMin.z, limitMax.z);

            bool anyLimited = joint.xMotion == ConfigurableJointMotion.Limited
                           || joint.yMotion == ConfigurableJointMotion.Limited
                           || joint.zMotion == ConfigurableJointMotion.Limited;

            if (anyLimited)
            {
                float limit = Mathf.Max(
                    Mathf.Max(Mathf.Abs(limitMin.x), Mathf.Abs(limitMax.x)),
                    Mathf.Max(Mathf.Abs(limitMin.y), Mathf.Abs(limitMax.y)),
                    Mathf.Max(Mathf.Abs(limitMin.z), Mathf.Abs(limitMax.z))
                );
                joint.linearLimit = new SoftJointLimit { limit = Mathf.Max(MIN_COLLIDER_SIZE, limit) };
            }
        }

        private void ApplyAngularLimits(ConfigurableJoint joint, PmxJoint jData)
        {
            Vector3 limitMinDeg = jData.RotationLimitMin * Mathf.Rad2Deg;
            Vector3 limitMaxDeg = jData.RotationLimitMax * Mathf.Rad2Deg;

            float xLow  = Mathf.Min(limitMinDeg.x, limitMaxDeg.x);
            float xHigh = Mathf.Max(limitMinDeg.x, limitMaxDeg.x);
            joint.angularXMotion = ResolveAngularMotion(xLow, xHigh);
            joint.lowAngularXLimit  = new SoftJointLimit { limit = xLow };
            joint.highAngularXLimit = new SoftJointLimit { limit = xHigh };

            float yLimit = Mathf.Max(Mathf.Abs(limitMinDeg.y), Mathf.Abs(limitMaxDeg.y));
            joint.angularYMotion = ResolveAngularMotion(-yLimit, yLimit);
            joint.angularYLimit = new SoftJointLimit { limit = yLimit };

            float zLimit = Mathf.Max(Mathf.Abs(limitMinDeg.z), Mathf.Abs(limitMaxDeg.z));
            joint.angularZMotion = ResolveAngularMotion(-zLimit, zLimit);
            joint.angularZLimit = new SoftJointLimit { limit = zLimit };
        }

        private void ApplySpringDrives(ConfigurableJoint joint, PmxJoint jData)
        {
            joint.xDrive = CreateDrive(jData.SpringPosition.x);
            joint.yDrive = CreateDrive(jData.SpringPosition.y);
            joint.zDrive = CreateDrive(jData.SpringPosition.z);

            joint.angularXDrive = CreateDrive(jData.SpringRotation.x);
            joint.angularYZDrive = CreateDrive(Mathf.Max(jData.SpringRotation.y, jData.SpringRotation.z));
        }

        private static ConfigurableJointMotion ResolveLinearMotion(float min, float max)
        {
            bool isLocked = Mathf.Abs(min) < JOINT_LOCK_THRESHOLD && Mathf.Abs(max) < JOINT_LOCK_THRESHOLD;
            return isLocked ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Limited;
        }

        private static ConfigurableJointMotion ResolveAngularMotion(float minDeg, float maxDeg)
        {
            bool isLocked = (maxDeg - minDeg) < JOINT_LOCK_THRESHOLD;
            return isLocked ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Limited;
        }

        private static JointDrive CreateDrive(float spring)
        {
            return new JointDrive
            {
                positionSpring = Mathf.Max(0f, spring),
                positionDamper = 0f,
                maximumForce   = float.MaxValue
            };
        }

        private Collider ConfigureCollider(GameObject rbGo, PmxRigidBody rbData, float scaleCoef)
        {
            switch (rbData.Shape)
            {
                case 0:
                    var sphere = rbGo.AddComponent<SphereCollider>();
                    sphere.radius = Mathf.Max(MIN_COLLIDER_SIZE, rbData.Size.x * scaleCoef);
                    return sphere;

                case 1:
                    var box = rbGo.AddComponent<BoxCollider>();
                    box.size = new Vector3(
                        Mathf.Max(MIN_COLLIDER_SIZE, rbData.Size.x * 2f * scaleCoef),
                        Mathf.Max(MIN_COLLIDER_SIZE, rbData.Size.y * 2f * scaleCoef),
                        Mathf.Max(MIN_COLLIDER_SIZE, rbData.Size.z * 2f * scaleCoef));
                    return box;

                case 2:
                    var capsule = rbGo.AddComponent<CapsuleCollider>();
                    capsule.radius = Mathf.Max(MIN_COLLIDER_SIZE, rbData.Size.x * scaleCoef);
                    capsule.height = Mathf.Max(capsule.radius * 2f, rbData.Size.y * 2f * scaleCoef);
                    return capsule;

                default:
                    Debug.LogWarning($"[PmxPhysicsBuilder] Unknown collider shape type '{rbData.Shape}'.");
                    return null;
            }
        }
    }
}

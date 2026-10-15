using System.Threading.Tasks;
using UnityEngine;
using MMDPlayerForVR.PmxImporter.Core;

namespace MMDPlayerForVR.PmxImporter.Builders
{
    public class PmxBoneBuilder : IPmxBoneBuilder
    {
        public async Task<(Transform root, Transform[] bones, Matrix4x4[] bindposes)> BuildAsync(PmxDocument doc)
        {
            Transform[] boneTransforms = new Transform[doc.Bones.Length];
            GameObject rootObj = new GameObject("Armature");
            Transform rootTransform = rootObj.transform;

            // 1. Create all bone GameObjects first (flat, no parenting yet)
            for (int i = 0; i < doc.Bones.Length; i++)
            {
                var pmxBone = doc.Bones[i];
                GameObject boneObj = new GameObject(pmxBone.Name);
                Transform boneT = boneObj.transform;

                // PMX Position is in model-space (world-equivalent). Set world position before parenting.
                // MMD and Unity are both Left-Handed. No Z inversion needed.
                boneT.position = pmxBone.Position;

                boneTransforms[i] = boneT;
            }

            // 2. Resolve hierarchy (SetParent with worldPositionStays=true so world positions are preserved)
            for (int i = 0; i < doc.Bones.Length; i++)
            {
                var pmxBone = doc.Bones[i];
                Transform current = boneTransforms[i];

                if (pmxBone.ParentBoneIndex >= 0 && pmxBone.ParentBoneIndex < doc.Bones.Length)
                {
                    current.SetParent(boneTransforms[pmxBone.ParentBoneIndex], true);
                }
                else
                {
                    current.SetParent(rootTransform, true);
                }
            }

            // 3. Calculate bindposes AFTER hierarchy is established and world positions are final.
            // bindpose = inverse of the bone's world matrix at bind time.
            // This tells the GPU "how to transform a vertex from mesh-space into this bone's local space".
            // Without this, Unity's SkinnedMeshRenderer computes incorrect Bounds,
            // causing the model to be frustum-culled and disappear unexpectedly.
            Matrix4x4[] bindposes = new Matrix4x4[doc.Bones.Length];
            for (int i = 0; i < doc.Bones.Length; i++)
            {
                // The mesh's vertices are in model-space (identical to rootTransform space),
                // so we multiply by rootTransform's localToWorldMatrix inverse to get back to mesh-local.
                bindposes[i] = boneTransforms[i].worldToLocalMatrix * rootTransform.localToWorldMatrix;
            }

            return await Task.FromResult((rootTransform, boneTransforms, bindposes));
        }
    }
}

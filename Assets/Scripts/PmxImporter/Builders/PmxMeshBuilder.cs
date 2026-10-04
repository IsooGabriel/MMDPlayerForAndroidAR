using MMDPlayerForVR.PmxImporter.Core;
using System.Threading.Tasks;
using UnityEngine;

namespace MMDPlayerForVR.PmxImporter.Builders
{
    public class PmxMeshBuilder : IPmxMeshBuilder
    {
        public async Task<Mesh> BuildAsync(PmxDocument doc)
        {
            // Unity Mesh construction must happen on the main thread.
            // Using Task.Yield() or similar to distribute load could be added later.
            Mesh mesh = new Mesh();
            mesh.name = doc.Name;

            if (doc.Vertices.Length > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            Vector3[] vertices = new Vector3[doc.Vertices.Length];
            Vector3[] normals = new Vector3[doc.Vertices.Length];
            Vector2[] uvs = new Vector2[doc.Vertices.Length];
            BoneWeight[] boneWeights = new BoneWeight[doc.Vertices.Length];

            for (int i = 0; i < doc.Vertices.Length; i++)
            {
                var v = doc.Vertices[i];
                // MMD and Unity are both Left-Handed, Y-Up, Z-Forward. No Z inversion needed!
                vertices[i] = v.Position;
                normals[i] = v.Normal;
                // MMD UV origin is Top-Left, Unity is Bottom-Left. Invert V.
                uvs[i] = new Vector2(v.Uv.x, 1.0f - v.Uv.y);

                boneWeights[i] = CreateBoneWeight(v);
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.boneWeights = boneWeights;

            // Submeshes based on Materials
            mesh.subMeshCount = doc.Materials.Length;
            int faceIndexOffset = 0;

            for (int m = 0; m < doc.Materials.Length; m++)
            {
                var material = doc.Materials[m];
                int surfaceFaceCount = material.SurfaceCount / 3;
                int[] triangles = new int[material.SurfaceCount];

                for (int f = 0; f < surfaceFaceCount; f++)
                {
                    var face = doc.Faces[faceIndexOffset + f];
                    // Z is not inverted, so winding order remains the same as PMX
                    triangles[f * 3 + 0] = face.VertexIndices[0];
                    triangles[f * 3 + 1] = face.VertexIndices[1];
                    triangles[f * 3 + 2] = face.VertexIndices[2];
                }

                mesh.SetTriangles(triangles, m);
                faceIndexOffset += surfaceFaceCount;
            }

            mesh.RecalculateBounds();
            // mesh.RecalculateTangents(); // Optional, if needed for normal maps

            return await Task.FromResult(mesh);
        }

        private BoneWeight CreateBoneWeight(PmxVertex v)
        {
            BoneWeight bw = new BoneWeight();

            // Handle up to 4 bones
            if (v.BoneIndices.Length > 0) { bw.boneIndex0 = v.BoneIndices[0]; bw.weight0 = v.BoneWeights[0]; }
            if (v.BoneIndices.Length > 1) { bw.boneIndex1 = v.BoneIndices[1]; bw.weight1 = v.BoneWeights[1]; }
            if (v.BoneIndices.Length > 2) { bw.boneIndex2 = v.BoneIndices[2]; bw.weight2 = v.BoneWeights[2]; }
            if (v.BoneIndices.Length > 3) { bw.boneIndex3 = v.BoneIndices[3]; bw.weight3 = v.BoneWeights[3]; }

            // Normalize weights (important for BDEF4 which may not sum to 1.0)
            float sum = bw.weight0 + bw.weight1 + bw.weight2 + bw.weight3;
            if (sum > 0.0001f && Mathf.Abs(sum - 1.0f) > 0.0001f)
            {
                bw.weight0 /= sum;
                bw.weight1 /= sum;
                bw.weight2 /= sum;
                bw.weight3 /= sum;
            }

            return bw;
        }
    }
}

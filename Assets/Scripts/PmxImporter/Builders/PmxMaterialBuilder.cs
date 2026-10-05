using MMDPlayerForVR.PmxImporter.Core;
using MMDPlayerForVR.Pose.Application;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEngine.UI.Image;

namespace MMDPlayerForVR.PmxImporter.Builders
{
    public class PmxMaterialBuilder : IPmxMaterialBuilder
    {
        private PmxMaterialTemplates _materialTemplates = null;
        private IStreamingAssetsReader _streamingAssetsReader;
        public PmxMaterialBuilder(PmxMaterialTemplates materialTemplates, IStreamingAssetsReader streamingAssetsReader)
        {
            _materialTemplates = materialTemplates;
            _streamingAssetsReader = streamingAssetsReader;
        }

        public async Task<Material[]> BuildAsync(PmxDocument doc, string basePath)
        {
            Material[] materials = new Material[doc.Materials.Length];
            Texture2D[] textures = new Texture2D[doc.Textures.Length];

            // 1. Load Textures
            for (int i = 0; i < doc.Textures.Length; i++)
            {
                // PMX texture paths often use Windows backslashes
                string texRelative = doc.Textures[i].Replace('\\', '/').TrimStart('/');
                if (texRelative.StartsWith("./"))
                {
                    texRelative = texRelative.Substring(2);
                }

                string texPath;
                if (basePath.Contains("://"))
                {
                    texPath = basePath.EndsWith("/") ? basePath + texRelative : basePath + "/" + texRelative;
                }
                else
                {
                    texPath = Path.Combine(basePath, texRelative);
                }
                textures[i] = await LoadTextureAsync(texPath);
            }

            // 2. Create Materials
            for (int i = 0; i < doc.Materials.Length; i++)
            {
                var pmxMat = doc.Materials[i];

                // Handle transparency and alpha clipping
                // Simple heuristic: if alpha < 1, it's semi-transparent.
                // Otherwise, enable cutout by default since MMD heavily uses it for hair/eyelashes.
                bool isTransparent = pmxMat.Diffuse.a < 0.99f;
                bool isCutout = true;

                // Use Standard/URP Lit as MVP before custom Toon Shader is ready

                Shader shader = null;
                if (isTransparent)
                {
                    shader = _materialTemplates.transparent.shader;
                }
                else if (isCutout)
                {
                    shader = _materialTemplates.cutout.shader;
                }
                else
                {
                    shader = _materialTemplates.opaque.shader;
                }

                Debug.Log(
                    $"[PmxMaterialBuilder] " +
                    $"Material={pmxMat.Name}, " +
                    $"Shader={(shader != null ? shader.name : "NULL")}"
                );

                Material mat = new Material(shader);
                mat.name = pmxMat.Name;

                mat.SetColor(shader.name.Contains("Universal") ? "_BaseColor" : "_Color", pmxMat.Diffuse);

                if (pmxMat.TextureIndex >= 0 && pmxMat.TextureIndex < textures.Length)
                {
                    mat.SetTexture(shader.name.Contains("Universal") ? "_BaseMap" : "_MainTex", textures[pmxMat.TextureIndex]);
                }

                // Handle double-sided rendering flag (bit0)
                if ((pmxMat.DrawFlags & 0x01) != 0)
                {
                    mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                }


                if (isTransparent)
                {
                    // URP Transparent Setup
                    mat.SetFloat("_Surface", 1.0f); // 1 = Transparent
                    mat.SetFloat("_Blend", 0.0f);   // 0 = Alpha
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                }
                else if (isCutout)
                {
                    // URP Cutout (AlphaTest) Setup
                    mat.SetFloat("_AlphaClip", 1.0f);
                    mat.SetFloat("_Cutoff", 0.5f);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                }

                materials[i] = mat;
            }

            return await Task.FromResult(materials);
        }

        private async Task<Texture2D> LoadTextureAsync(string path)
        {
            if (!await _streamingAssetsReader.ExistsAsync(path))
            {
                Debug.LogWarning($"[PmxMaterialBuilder] Texture not found (or HEAD failed): {path}");
                // We'll proceed to try loading anyway just in case HEAD failed but GET works
            }

            try
            {
                byte[] bytes = await _streamingAssetsReader.ReadAllBytesAsync(path);
                if (bytes == null)
                {
                    return CreateFallbackTexture();
                }

                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(bytes))
                {
                    return tex;
                }

                // TGA or BMP might fail with standard LoadImage, requires custom decoders.
                Debug.LogWarning($"[PmxMaterialBuilder] Failed to decode texture natively (TGA/BMP custom decoder required): {path}");
                return CreateFallbackTexture();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PmxMaterialBuilder] Exception loading texture {path}: {ex.Message}");
                return CreateFallbackTexture();
            }
        }

        private Texture2D CreateFallbackTexture()
        {
            Texture2D tex = new Texture2D(2, 2);
            tex.SetPixels(new Color[] { Color.magenta, Color.magenta, Color.magenta, Color.magenta });
            tex.Apply();
            return tex;
        }
    }
}
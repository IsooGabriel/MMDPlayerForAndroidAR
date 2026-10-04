using MMDPlayerForVR.Services;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;
using System.IO;

namespace MMDPlayerForVR.PmxImporter
{
    /// <summary>
    /// Runtime Entry Point for importing PMX models directly in Unity.
    /// Can be attached to a GameObject for easy testing in the Inspector.
    /// </summary>
    public class PmxRuntimeLoader : MonoBehaviour
    {
        [Header("Test Configuration")]
        [Tooltip("Absolute path to the .pmx file to load (e.g. C:/Models/Miku/miku.pmx)")]
        public string pmxFilePath = "";

        [Tooltip("If true, automatically loads the model when the scene starts")]
        public bool loadOnStart = false;

        private PmxImporterPipeline _pipeline;
        private PlayerLogService _logService;

        [Inject]
        public void Construct(PmxImporterPipeline pipeline, PlayerLogService logService)
        {
            _pipeline = pipeline;
            _logService = logService;
        }

        private async void Start()
        {
            if (_logService != null)
            {
                _logService.Log("started");
            }

            if (!loadOnStart)
            {
                return;
            }

#if UNITY_EDITOR
            _logService.Log($"running in editor");

#else
            _logService.Log($"not running in editor");
            if(pmxFilePath.StartsWith("Assets/StreamingAssets"))
            {
                string relativePath = pmxFilePath.Substring("Assets/StreamingAssets".Length).TrimStart('/', '\\');
                string saPath = Application.streamingAssetsPath;
                pmxFilePath = saPath.Contains("://") ? 
                    (saPath.EndsWith("/") ? saPath + relativePath : saPath + "/" + relativePath) : 
                    Path.Combine(saPath, relativePath);
            }
#endif



            if (!string.IsNullOrEmpty(pmxFilePath))
            {
                if (_logService != null)
                {
                    _logService.Log($"{pmxFilePath} is loading...");
                }
                await LoadModelAsync(pmxFilePath);
            }
            else
            {
                if (_logService != null)
                {
                    _logService.LogError($"{pmxFilePath} is null or empty");
                }
            }
        }

        /// <summary>
        /// Call this method from UI or other scripts to trigger the load at runtime.
        /// </summary>
        public async Task<GameObject> LoadModelAsync(string path)
        {
            if (!await Core.AsyncFileLoader.ExistsAsync(path))
            {
                // Fallback to avoid complete failure if HEAD request failed but GET might succeed.
                // But normally we can just warn here. Let's just log and continue to let the actual load fail if it's really missing.
                Debug.LogWarning($"[PmxRuntimeLoader] PMX file might not exist at path (or HEAD request failed): {path}");
            }

            Debug.Log($"[PmxRuntimeLoader] Starting import of {path} ...");
            var stopWatch = System.Diagnostics.Stopwatch.StartNew();

            GameObject model = await _pipeline.ImportAsync(path);

            stopWatch.Stop();
            if (model != null)
            {
                Debug.Log($"[PmxRuntimeLoader] Import completed successfully in {stopWatch.ElapsedMilliseconds} ms.");
                // Attach the loaded model to this GameObject to keep the scene clean
                model.transform.SetParent(this.transform, false);
            }
            else
            {
                Debug.LogError("[PmxRuntimeLoader] Import failed. Please check the console logs for details.");
            }

            return model;
        }
    }
}
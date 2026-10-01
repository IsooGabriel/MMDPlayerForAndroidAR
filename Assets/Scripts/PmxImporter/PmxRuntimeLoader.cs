using MMDPlayerForVR.Services;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

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
#else
            if(pmxFilePath.StartsWith("Assets/StreamingAssets"))
            {
                pmxFilePath = Path.Combine(Application.streamingAssetsPath, pmxFilePath.Substring("Assets/StreamingAssets".Length));
                if (pmxFilePath.StartsWith("/"))
                {
                     pmxFilePath = pmxFilePath.Substring(1);
                }
            }
#endif


            if (string.IsNullOrEmpty(pmxFilePath))
            {
                if (_logService != null)
                {
                    _logService.LogError($"{pmxFilePath} is null or empty");
                    return;
                }
            }

            if (!File.Exists(pmxFilePath))
            {
                string msg = $"PMX file not found at path: {pmxFilePath}";
                Debug.LogError($"[PmxRuntimeLoader] {msg}");
                _logService?.LogError(msg);
                return;
            }
            await LoadModelAsync(pmxFilePath);
        }

        /// <summary>
        /// Call this method from UI or other scripts to trigger the load at runtime.
        /// </summary>
        public async Task<GameObject> LoadModelAsync(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[PmxRuntimeLoader] PMX file not found at path: {path}");
                return null;
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

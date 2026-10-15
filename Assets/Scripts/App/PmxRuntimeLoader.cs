using MMDPlayerForVR.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace MMDPlayerForVR.PmxImporter
{
    /// <summary>
    /// 層C: シーン上でPMXロードを実行するMonoBehaviour。
    /// UI・DI・ファイル選択イベントの管理を担い、
    /// コアのロジックは PmxModelLoader に委譲する。
    /// </summary>
    public class PmxRuntimeLoader : MonoBehaviour
    {
        [Header("Test Configuration")]
        [Tooltip("Absolute path to the .pmx file to load (e.g. C:/Models/Miku/miku.pmx)")]
        public string pmxFilePath = "";
        public string vmdFilePath = "";

        [NonSerialized]
        public string pmxName = "";

        [Tooltip("If true, automatically loads the model when the scene starts")]
        public bool loadOnStart = false;

        [Header("IK Debug")]
        [Tooltip("If true, skip leg IK chains (足IK親・足ＩＫ・つま先ＩＫ) and apply FK only, for debugging")]
        public bool skipLegIk = false;

        public Action<string> OnFinishInitiation;

        private PmxModelLoader _modelLoader;
        private PlayerLogService _logService;
        private FilePickerReceiver _filePickerReceiver;

        public readonly string _tposeName = "T-Pose";

        [Inject]
        public void Construct(
            PmxModelLoader modelLoader,
            PlayerLogService logService,
            FilePickerReceiver filePickerReceiver)
        {
            _modelLoader = modelLoader;
            _logService = logService;
            _filePickerReceiver = filePickerReceiver;
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

            Init(pmxFilePath, vmdFilePath);

#if UNITY_EDITOR
            _logService.Log($"running in editor");

#else
            _logService.Log($"not running in editor");
#endif
            _filePickerReceiver.OnGetModel = InitModel;
            _filePickerReceiver.OnGetMotion = InitMotion;
        }


        public void InitModel(string pmxPath)
        {
            Init(pmxPath, vmdFilePath);
        }
        public void InitMotion(string vmdPath)
        {
            Init(pmxFilePath, vmdPath);
        }
        public async void Init(string pmxPath, string vmdPath)
        {
            pmxFilePath = pmxPath;
            if (vmdPath == _tposeName)
            {
                vmdFilePath = null; // T-Poseを適用する場合はVMDファイルを指定しない
            }
            else
            {
                vmdFilePath = vmdPath;
            }
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            if (!string.IsNullOrEmpty(pmxPath))
            {
                if (_logService != null)
                {
                    _logService.Log($"{pmxPath} is loading...");
                }

                _logService.Log($"PATH = [{pmxPath}]");
                _logService.Log($"File.Exists = {File.Exists(pmxPath)}");
                _logService.Log($"Directory.Exists = {Directory.Exists(Path.GetDirectoryName(pmxPath))}");
                string directory = Path.GetDirectoryName(pmxPath);

                if (Directory.Exists(directory))
                {
                    foreach (string file in Directory.GetFiles(directory))
                    {
                        Debug.Log($"ACTUAL FILE = [{file}]");
                    }
                }

                await LoadModelAsync(pmxPath);
            }
            else
            {
                if (_logService != null)
                {
                    _logService.LogError($"{pmxPath} is null or empty");
                }
            }
        }

        /// <summary>
        /// Call this method from UI or other scripts to trigger the load at runtime.
        /// </summary>
        public async Task<GameObject> LoadModelAsync(string path)
        {
            _modelLoader.SkipLegIk = skipLegIk;
            GameObject model = await _modelLoader.LoadModelAsync(path, vmdFilePath);
            if (model != null)
            {
                model.transform.SetParent(this.transform, false);
                OnFinishInitiation?.Invoke(model.name);
                pmxName = model.name;
            }
            return model;
        }
    }
}

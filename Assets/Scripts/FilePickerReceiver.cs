

using MMDPlayerForVR.Services;
using System;
using UnityEngine;
using VContainer;

namespace MMDPlayerForVR
{
    public class FilePickerReceiver : MonoBehaviour
    {
        public Action<string> OnGetURI;
        public Action<string> OnGetModel;
        public Action<string> OnGetMotion;

        public bool isModelPath = true;

        private PlayerLogService _playerLogService;

        [Inject]
        public void Inject(PlayerLogService playerLogService)
        {
            _playerLogService = playerLogService;
        }

        public void OnLoadModel()
        {
            isModelPath = true;

#if UNITY_ANDROID && !UNITY_EDITOR
            using var bridge = new AndroidJavaClass("com.MMD4AR.filepicker.SafFolderBridge");
            string destRoot = System.IO.Path.Combine(Application.persistentDataPath, "PmxCache");
            bridge.CallStatic("pickFolder", gameObject.name, destRoot);
#else
            _playerLogService.LogWarning("SafFolderPicker is only supported on Android Device.");
#endif
        }

        public void OnLoadModel(string path)
        {
            isModelPath = true;
            OnFileSelected(path);
        }

        public void OnLoadMotion()
        {
            isModelPath = false;

            using var unityPlayer =
                new AndroidJavaClass("com.unity3d.player.UnityPlayer");

            using var activity =
                unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            using var filePicker =
                new AndroidJavaClass("com.example.filepicker.FilePicker");

            filePicker.CallStatic("OpenFilePicker", activity);

        }

        public void OnLoadMotion(string path)
        {
            isModelPath = false;
            OnFileSelected(path);
        }

        public void OnFileSelected(string uri)
        {
            if (isModelPath)
            {
                OnGetModel?.Invoke(uri);
            }
            else
            {
                OnGetMotion?.Invoke(uri);
            }
            OnGetURI?.Invoke(uri);
        }

        // --- SAF Folder Picker Callbacks ---
        public void OnSafFolderProgress(string countStr)
        {
            _playerLogService.Log($"[SafFolder] Copied {countStr} files...");
        }

        public void OnSafFolderCompleted(string destPath)
        {
            _playerLogService.Log($"[SafFolder] Completed! Path: {destPath}");
            if (System.IO.Directory.Exists(destPath))
            {
                string[] pmxFiles = System.IO.Directory.GetFiles(destPath, "*.pmx", System.IO.SearchOption.AllDirectories);
                if (pmxFiles.Length > 0)
                {
                    OnFileSelected(pmxFiles[0]);
                }
                else
                {
                    _playerLogService.LogError("[SafFolder] No .pmx file found in the selected folder.");
                }
            }
        }

        public void OnSafFolderCancelled(string empty)
        {
            _playerLogService.Log("[SafFolder] Cancelled");
        }

        public void OnSafFolderError(string error)
        {
            _playerLogService.LogError($"[SafFolder] Error: {error}");
        }
    }
}

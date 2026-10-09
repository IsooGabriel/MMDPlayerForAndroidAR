
using MMDPlayerForVR.Services;
using System;
using System.Collections.Generic;
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

        [Header("Disable during loading")]
        [SerializeField] private MonoBehaviour[] _behavioursToDisableWhileLoading;

        private PlayerLogService _playerLogService;

        // Android callback → main thread dispatch queue
        private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();

        [Inject]
        public void Inject(PlayerLogService playerLogService)
        {
            _playerLogService = playerLogService;
        }

        private void Update()
        {
            // Drain the queue on the main thread
            while (_mainThreadQueue.Count > 0)
            {
                Action action;
                lock (_mainThreadQueue)
                {
                    if (_mainThreadQueue.Count == 0) break;
                    action = _mainThreadQueue.Dequeue();
                }
                action?.Invoke();
            }
        }

        /// <summary>スレッドセーフ: アクションを次の Update() でメインスレッドで実行する</summary>
        private void RunOnMainThread(Action action)
        {
            lock (_mainThreadQueue)
            {
                _mainThreadQueue.Enqueue(action);
            }
        }

        // ----- ローディング中のカメラ追跡制御 -----

        private void SetTrackingBehavioursEnabled(bool value)
        {
            if (_behavioursToDisableWhileLoading == null) return;
            foreach (var b in _behavioursToDisableWhileLoading)
            {
                if (b != null) b.enabled = value;
            }
        }

        // ----- 外部API -----

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
            // 読み込み開始: カメラ追跡などを無効化
            SetTrackingBehavioursEnabled(false);

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

        /// <summary>
        /// PmxRuntimeLoader.OnFinishInitiation に接続すること。
        /// モデルのロードが完了したらカメラ追跡などを再度有効化する。
        /// </summary>
        public void ReEnableTracking(string _)
        {
            SetTrackingBehavioursEnabled(true);
        }

        // ----- SAF Folder Picker Callbacks (Java スレッドから呼ばれる) -----

        /// <summary>SafFolderBridge からのコピー進捗通知 (Javaスレッド)</summary>
        public void OnSafFolderProgress(string countStr)
        {
            RunOnMainThread(() => _playerLogService.Log($"[SafFolder] Copied {countStr} files..."));
        }

        /// <summary>SafFolderBridge からのコピー完了通知 (Javaスレッド)</summary>
        public void OnSafFolderCompleted(string destPath)
        {
            RunOnMainThread(() =>
            {
                _playerLogService.Log($"[SafFolder] Completed! Path: {destPath}");
                if (!System.IO.Directory.Exists(destPath))
                {
                    _playerLogService.LogError("[SafFolder] Destination directory does not exist.");
                    return;
                }

                string[] pmxFiles = System.IO.Directory.GetFiles(
                    destPath, "*.pmx", System.IO.SearchOption.AllDirectories);

                if (pmxFiles.Length == 1)
                {
                    OnFileSelected(pmxFiles[0]);
                }
                else if (pmxFiles.Length > 1)
                {
#if UNITY_ANDROID && !UNITY_EDITOR
                    ShowPmxSelectionDialog(pmxFiles);
#else
                    _playerLogService.LogWarning("[SafFolder] Multiple PMX files found. Selecting the first one in Editor.");
                    OnFileSelected(pmxFiles[0]);
#endif
                }
                else
                {
                    _playerLogService.LogError("[SafFolder] No .pmx file found in the selected folder.");
                }
            });
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void ShowPmxSelectionDialog(string[] pmxFiles)
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    using (var builder = new AndroidJavaObject("android.app.AlertDialog$Builder", activity))
                    {
                        builder.Call<AndroidJavaObject>("setTitle", "Select PMX File");

                        string[] fileNames = new string[pmxFiles.Length];
                        for (int i = 0; i < pmxFiles.Length; i++)
                        {
                            fileNames[i] = System.IO.Path.GetFileName(pmxFiles[i]);
                        }

                        var callback = new PmxSelectionCallback(this, pmxFiles);
                        builder.Call<AndroidJavaObject>("setItems", fileNames, callback);
                        builder.Call<AndroidJavaObject>("show");
                    }
                }));
            }
        }

        // files フィールドを internal に上げてコールバックからアクセスできるようにする
        internal string[] _lastPmxFiles;

        class PmxSelectionCallback : AndroidJavaProxy
        {
            readonly FilePickerReceiver _receiver;
            readonly string[] _files;
            public PmxSelectionCallback(FilePickerReceiver r, string[] f)
                : base("android.content.DialogInterface$OnClickListener")
            {
                _receiver = r;
                _files = f;
            }
            // Android UIスレッドから呼ばれる → メインスレッドへ dispatch
            public void onClick(AndroidJavaObject dialog, int which)
            {
                string selected = _files[which];
                _receiver.RunOnMainThread(() => _receiver.OnFileSelected(selected));
            }
        }
#endif

        public void OnSafFolderCancelled(string empty)
        {
            RunOnMainThread(() => _playerLogService.Log("[SafFolder] Cancelled"));
        }

        public void OnSafFolderError(string error)
        {
            RunOnMainThread(() => _playerLogService.LogError($"[SafFolder] Error: {error}"));
        }
    }
}

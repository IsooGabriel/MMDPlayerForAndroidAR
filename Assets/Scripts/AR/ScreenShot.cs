
using MMDPlayerForVR.PmxImporter;
using MMDPlayerForVR.Services;
using System;
using System.IO;
using UnityEngine;
using VContainer;

namespace MMDPlayerForVR
{
    public class ScreenShot : MonoBehaviour
    {
        public string _imageFolderPath = "MMD4AR";
        private PmxRuntimeLoader _pmxRuntimeLoader;
        private PlayerLogService _playerLogService;

        [Inject]
        public void Inject(PmxRuntimeLoader pmxRuntimeLoader, PlayerLogService playerLogService)
        {
            _pmxRuntimeLoader = pmxRuntimeLoader;
            _playerLogService = playerLogService;
        }

        public void OnScreenShot()
        {
            try
            {
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                byte[] png = tex.EncodeToPNG();
                Destroy(tex); // 必ず破棄する(しないとメモリリーク)

                string fileName = $"MMD4AR_{Path.GetFileNameWithoutExtension(_pmxRuntimeLoader.pmxFilePath)}" +
                                    $"_{Path.GetFileNameWithoutExtension(_pmxRuntimeLoader.vmdFilePath)}" +
                                    $"_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
                string path = Path.Combine(Application.persistentDataPath, fileName);
                File.WriteAllBytes(path, png);
                _playerLogService.Log($"Saved: {path} ({png.Length} bytes)");
            }
            catch(Exception e)
            {
                _playerLogService.LogError($"[Photo] {e}");
            }
        }
    }
}

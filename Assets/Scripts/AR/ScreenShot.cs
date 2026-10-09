
using MMDPlayerForVR.PmxImporter;
using MMDPlayerForVR.Services;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MMDPlayerForVR
{
    public class ScreenShot : MonoBehaviour
    {
        public GameObject[] exclusivityObjects;
        public GameObject sampleParent;
        public AspectRatioFitter aspectRatioFitter;
        public RawImage sample;
        public float sampleDisplayTime = 0.7f;

        private const string _albumFolder = "Pictures/MMD4AR";

        [SerializeField]
        private PmxRuntimeLoader _pmxRuntimeLoader;
        [SerializeField]
        private PlayerLogService _playerLogService;
        private AndroidJavaObject _shutterSound;
        private float _timer = 0;


        [Inject]
        public void Inject(PmxRuntimeLoader pmxRuntimeLoader, PlayerLogService playerLogService)
        {
            if (pmxRuntimeLoader != null)
            {
                _pmxRuntimeLoader = pmxRuntimeLoader;
            }
            if (playerLogService != null)
            {
                _playerLogService = playerLogService;

            }
        }

        public void OnScreenShot()
        {
            foreach (GameObject obj in exclusivityObjects)
            {
                obj.SetActive(false);
            }

            try
            {
                _playerLogService.Log("[Photo]開始");

                ShutterSoundPlay();
                _playerLogService.Log("[Photo]音");
                var tex = ScreenCapture.CaptureScreenshotAsTexture();

                _playerLogService.Log("[Photo]エンコード");
                DisplayPhotoSample(tex);
                byte[] png = tex.EncodeToPNG();

                _playerLogService.Log("[Photo]名前作る");
                string fileName = $"MMD4AR_{_pmxRuntimeLoader.pmxName}" +
                                    $"_{Path.GetFileNameWithoutExtension(_pmxRuntimeLoader.vmdFilePath)}" +
                                    $"_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";

                string path = Path.Combine(Application.persistentDataPath, fileName);

                _playerLogService.Log("[Photo]tmp保存");
                File.WriteAllBytes(path, png);
                TrySave(png, fileName, out string error);
                if (error != null)
                {
                    _playerLogService.LogError(error);
                }

                Destroy(tex); // 必ず破棄する(しないとメモリリーク)
                _playerLogService.Log($"Saved: {path} ({png.Length} bytes)");
            }
            catch (Exception e)
            {
                _playerLogService.LogError($"[Photo] {e}");
            }
            finally
            {
                foreach (GameObject obj in exclusivityObjects)
                {
                    obj.SetActive(true);
                }
            }
        }

        private void DisplayPhotoSample(Texture2D texture)
        {
            _timer = sampleDisplayTime;
            sampleParent.SetActive(true);
            sample.texture = texture;
            aspectRatioFitter.aspectRatio = texture.width / texture.height;
        }

        public bool TrySave(byte[] png, string fileName, out string error)
        {
            error = null;
            try
            {
                using var versionClass = new AndroidJavaClass("android.os.Build$VERSION");
                if (versionClass.GetStatic<int>("SDK_INT") < 29)
                {
                    error = "Android 10 (API 29) 未満はギャラリー保存に未対応です";
                    return false;
                }

                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
                using var pending = new AndroidJavaObject("java.lang.Integer", 1);
                using var values = new AndroidJavaObject("android.content.ContentValues");
                values.Call("put", "_display_name", fileName);
                values.Call("put", "mime_type", "image/png");
                values.Call("put", "relative_path", _albumFolder);
                values.Call("put", "is_pending", pending);

                using var mediaClass = new AndroidJavaClass("android.provider.MediaStore$Images$Media");
                using var collection = mediaClass.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");

                using var uri = resolver.Call<AndroidJavaObject>("insert", collection, values);
                if (uri == null)
                {
                    error = "MediaStore.insert が null を返しました";
                    return false;
                }

                using (var stream = resolver.Call<AndroidJavaObject>("openOutputStream", uri))
                {
                    stream.Call("write", png);
                    stream.Call("flush");
                    stream.Call("close");
                }

                using var done = new AndroidJavaObject("android.content.ContentValues");
                using var pendingDone = new AndroidJavaObject("java.lang.Integer", 0);
                done.Call("put", "is_pending", pending);
                resolver.Call<int>("update", uri, done, null, null);
                return true;
            }
            catch (Exception e)
            {
                error = e.ToString();
                return false;
            }
        }


        private void Start()
        {
            _shutterSound = new AndroidJavaObject(
                "com.example.shuttersound.ShutterSound"
            );
        }

        public void ShutterSoundPlay()
        {
            _shutterSound.Call("play");
        }

        private void OnDestroy()
        {
            _shutterSound?.Call("release");
            _shutterSound?.Dispose();
        }

        private void Update()
        {
            if (_timer < 0)
            {
                return;
            }

            _timer -= Time.deltaTime;

            if (_timer < 0)
            {
                sample.texture = null;
                sampleParent.SetActive(false);
            }
        }
    }
}

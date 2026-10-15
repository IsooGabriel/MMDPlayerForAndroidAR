using System;
using UnityEngine;

namespace MMDPlayerForVR
{
    /// <summary>
    /// 層B: Android MediaStore APIを使って画像をギャラリーに保存するクラス。
    /// PlayerLogService・VContainer・アプリ層の型に依存しない。
    /// </summary>
    public class AndroidGallerySaver
    {
        private const string AlbumFolder = "Pictures/MMD4AR";

        /// <summary>
        /// PNG バイト列を Android のギャラリー（MediaStore）に保存する。
        /// 失敗した場合は error に理由を返し、false を返す。
        /// </summary>
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
                values.Call("put", "relative_path", AlbumFolder);
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
                done.Call("put", "is_pending", pendingDone);
                resolver.Call<int>("update", uri, done, null, null);
                return true;
            }
            catch (Exception e)
            {
                error = e.ToString();
                return false;
            }
        }
    }

    /// <summary>
    /// 層B: Android ShutterSound JNI ラッパー。
    /// シャッター音の再生・解放を担う。
    /// </summary>
    public class AndroidShutterSound : IDisposable
    {
        private AndroidJavaObject _shutterSound;

        public AndroidShutterSound()
        {
            _shutterSound = new AndroidJavaObject("com.example.shuttersound.ShutterSound");
        }

        public void Play()
        {
            _shutterSound?.Call("play");
        }

        public void Dispose()
        {
            _shutterSound?.Call("release");
            _shutterSound?.Dispose();
            _shutterSound = null;
        }
    }
}

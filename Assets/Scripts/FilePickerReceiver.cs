

using System;
using UnityEngine;

namespace MMDPlayerForVR
{
    public class FilePickerReceiver : MonoBehaviour
    {
        public Action<string> OnGetURI;
        public void OnLoad()
        {
            using var unityPlayer =
                new AndroidJavaClass("com.unity3d.player.UnityPlayer");

            using var activity =
                unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            using var filePicker =
                new AndroidJavaClass("com.example.filepicker.FilePicker");

            filePicker.CallStatic("OpenFilePicker", activity);

        }

        public void OnFileSelected(string uri)
        {
            OnGetURI?.Invoke(uri);
        }
    }
}

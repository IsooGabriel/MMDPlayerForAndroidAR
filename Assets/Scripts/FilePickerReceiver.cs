

using System;
using UnityEngine;

namespace MMDPlayerForVR
{
    public class FilePickerReceiver : MonoBehaviour
    {
        public Action<string> OnGetURI;
        public Action<string> OnGetModel;
        public Action<string> OnGetMotion;

        public bool isModelPath = true;
        public void OnLoadModel()
        {
            isModelPath = true;

            using var unityPlayer =
                new AndroidJavaClass("com.unity3d.player.UnityPlayer");

            using var activity =
                unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            using var filePicker =
                new AndroidJavaClass("com.example.filepicker.FilePicker");

            filePicker.CallStatic("OpenFilePicker", activity);

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
    }
}

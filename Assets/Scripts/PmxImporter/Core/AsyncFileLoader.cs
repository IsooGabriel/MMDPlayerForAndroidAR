using MMDPlayerForVR.Pose.Application;
using MMDPlayerForVR.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MMDPlayerForVR.PmxImporter.Core
{
    public class AsyncFileLoader : IStreamingAssetsReader
    {
        private PlayerLogService _playLogService;
        private readonly string _temporaryCachePath = "/data/user/0/";
        public AsyncFileLoader(PlayerLogService playLogService)
        {
            _playLogService = playLogService;
        }
        public string AndroidPathResolves(string path)
        {
            if (path.StartsWith("Assets/StreamingAssets"))
            {
                string relativePath = path.Substring("Assets/StreamingAssets".Length).TrimStart('/', '\\');
                string saPath = Application.streamingAssetsPath;
                path = saPath.Contains("://") ?
                    (saPath.EndsWith("/") ? saPath + relativePath : saPath + "/" + relativePath) :
                    Path.Combine(saPath, relativePath);
            }
            _playLogService.Log($"log:{path}");
            return path;
        }

        public async Task<byte[]> ReadAllBytesAsync(string path)
        {
#if UNITY_EDITOR
#else
        path = AndroidPathResolves(path);
#endif

            if (path.Contains("://") || path.Contains(":///"))
            {
                using (UnityWebRequest www = UnityWebRequest.Get(path))
                {
                    var operation = www.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    if (www.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"[AsyncFileLoader] Error loading {path}: {www.error}");
                        return null;
                    }

                    return www.downloadHandler.data;
                }
            }
            else
            {
                try
                {
                    return await Task.Run(() => File.ReadAllBytes(path));
                }
                catch (Exception e)
                {
                    _playLogService.LogError($"ReadAllBytesAsync:{e.ToString()}");
                }
                return null;
            }
        }

        public async Task<bool> ExistsAsync(string path)
        {
#if UNITY_EDITOR
#else
        path = AndroidPathResolves(path);
#endif
            if (path.Contains("://") || path.Contains(":///"))
            {
                using (UnityWebRequest www = UnityWebRequest.Head(path))
                {
                    var operation = www.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }
                    return www.result == UnityWebRequest.Result.Success;
                }
            }
            else
            {
                if (path.StartsWith(_temporaryCachePath))
                {
                    return true;
                }
                return File.Exists(path);
            }
        }
    }
}
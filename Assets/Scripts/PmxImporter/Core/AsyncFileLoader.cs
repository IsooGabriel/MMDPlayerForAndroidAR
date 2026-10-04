using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MMDPlayerForVR.PmxImporter.Core
{
    public static class AsyncFileLoader
    {
        public static async Task<byte[]> ReadAllBytesAsync(string path)
        {
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
                if (!File.Exists(path))
                {
                    Debug.LogError($"[AsyncFileLoader] File not found: {path}");
                    return null;
                }

                return await Task.Run(() => File.ReadAllBytes(path));
            }
        }

        public static async Task<bool> ExistsAsync(string path)
        {
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
                return File.Exists(path);
            }
        }
    }
}
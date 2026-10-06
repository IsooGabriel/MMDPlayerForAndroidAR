using System;
using System.IO;
using TMPro;
using UnityEngine;

namespace MMDPlayerForVR.Services
{
    /// <summary>
    /// Service for displaying logs to the player on the UI.
    /// Attach this to a GameObject in the scene and assign a TextMeshProUGUI component.
    /// </summary>
    public class PlayerLogService : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _displayLog;

        private string _logFilePath;

        /// <summary>
        /// Logs a message to the UI.
        /// </summary>
        public void Log(string message)
        {
            if (_displayLog != null)
            {
                _displayLog.text = $"{message}\n{_displayLog.text}";
            }
            SaveLog(message);
            Debug.Log(message);
        }

        /// <summary>
        /// Logs an error message to the UI in red.
        /// </summary>
        public void LogError(string message)
        {
            if (_displayLog != null)
            {
                _displayLog.text = $"<color=red>Error: {message}</color>\n{_displayLog.text}";
            }
            SaveLog(message);
            Debug.LogError(message);
        }

        public void LogWarning(string message)
        {
            if (_displayLog != null)
            {
                _displayLog.text = $"<color=yellow>Warning: {message}</color>\n{_displayLog.text}";
            }
            SaveLog(message);
            Debug.LogWarning(message);
        }

        private void SaveLog(string message)
        {
            string log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(_logFilePath, log);
        }

        private void OnEnable()
        {
            _logFilePath = Path.Combine(
                Application.persistentDataPath,
                "MMD4AR_GabuGabuNoMi.log"
            );
        }
    }
}

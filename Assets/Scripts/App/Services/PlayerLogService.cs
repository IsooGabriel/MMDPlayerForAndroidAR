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
            Debug.Log(message);
        }

        /// <summary>
        /// Logs an error message to the UI in red.
        /// </summary>
        public void LogError(string message)
        {
            Debug.LogError(message);
        }

        public void LogWarning(string message)
        {
            Debug.LogWarning(message);
        }

        private void HandleLogMessage(string message, string _, LogType type)
        {
            string displayMessage;
            switch (type)
            {
                case LogType.Warning:
                    displayMessage = $"<color=yellow>Warning: {message}</color>";
                    break;
                case LogType.Error:
                case LogType.Assert:
                case LogType.Exception:
                    displayMessage = $"<color=red>Error: {message}</color>";
                    break;
                default:
                    displayMessage = message;
                    break;
            }

            if (_displayLog != null)
            {
                _displayLog.text = $"{displayMessage}\n{_displayLog.text}";
            }

            SaveLog(message);
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
            Application.logMessageReceived += HandleLogMessage;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleLogMessage;
        }
    }
}

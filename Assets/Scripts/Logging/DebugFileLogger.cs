using System;
using System.IO;
using UnityEngine;

namespace ARLibraryNav.Logging
{
    /// <summary>
    /// Captures all Unity console messages (Log, Warning, Error, Exception) to a plain text
    /// file on the device for debugging purposes.
    ///
    /// File location (Android):
    ///   /storage/emulated/0/Android/data/&lt;package&gt;/files/ARLibNav_DebugLog_YYYYMMDD_HHmmss.txt
    ///
    /// Retrieve via ADB:
    ///   adb pull /storage/emulated/0/Android/data/&lt;package&gt;/files/ ./logs/
    ///
    /// Attach to: Managers in ARScene.
    /// Disable via Inspector when not debugging to avoid file I/O overhead.
    /// </summary>
    public class DebugFileLogger : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private bool captureEnabled = true;
        [SerializeField] private bool includeStackTraceOnErrors = true;

        private StreamWriter _writer;
        private string       _filePath;

        private void Awake()
        {
            if (!captureEnabled) return;

            string fileName = $"ARLibNav_DebugLog_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
            _filePath = Path.Combine(Application.persistentDataPath, fileName);

            try
            {
                _writer = new StreamWriter(_filePath, false, System.Text.Encoding.UTF8);
                WriteHeader();
                Application.logMessageReceived += OnLogMessage;

                // Log own path so it appears in the file and in the console
                Debug.Log($"[DebugFileLogger] Logging to: {_filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DebugFileLogger] Failed to open log file: {ex.Message}");
                _writer = null;
            }
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLogMessage;
            CloseFile();
        }

        private void OnApplicationPause(bool paused)
        {
            // Flush on pause so logs are preserved if the OS kills the app
            if (paused) _writer?.Flush();
        }

        private void OnApplicationQuit()
        {
            Application.logMessageReceived -= OnLogMessage;
            CloseFile();
        }

        private void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (_writer == null) return;

            try
            {
                string timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
                string tag = type switch
                {
                    LogType.Warning   => "WARN ",
                    LogType.Error     => "ERROR",
                    LogType.Exception => "EXCPT",
                    LogType.Assert    => "ASSRT",
                    _                 => "INFO "
                };

                _writer.WriteLine($"[{timestamp}] [{tag}] {message}");

                if (includeStackTraceOnErrors &&
                    (type == LogType.Error || type == LogType.Exception) &&
                    !string.IsNullOrEmpty(stackTrace))
                {
                    foreach (var line in stackTrace.Split('\n'))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            _writer.WriteLine($"             {line.Trim()}");
                    }
                }

                _writer.Flush();
            }
            catch { /* never throw from a log handler */ }
        }

        private void WriteHeader()
        {
            _writer.WriteLine("=== AR Library Navigator — Debug Log ===");
            _writer.WriteLine($"Started : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            _writer.WriteLine($"Platform: {Application.platform}");
            _writer.WriteLine($"Version : {Application.version}");
            _writer.WriteLine($"Path    : {_filePath}");
            _writer.WriteLine("=========================================");
            _writer.Flush();
        }

        private void CloseFile()
        {
            if (_writer == null) return;
            try
            {
                _writer.WriteLine("=========================================");
                _writer.WriteLine($"Ended : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                _writer.Flush();
                _writer.Close();
                _writer.Dispose();
            }
            catch { }
            finally { _writer = null; }
        }
    }
}

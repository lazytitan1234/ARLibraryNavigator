using System;
using System.IO;
using UnityEngine;

namespace ARLibraryNav.Logging
{
    /// <summary>
    /// Writes a CSV session log to Application.persistentDataPath for later analysis.
    ///
    /// On Android:
    ///   /storage/emulated/0/Android/data/com.YourCompany.ARLibraryNav/files/
    ///
    /// Retrieve logs via:
    ///   adb pull /storage/emulated/0/Android/data/&lt;package&gt;/files/ ./logs/
    ///
    /// CSV columns: timestamp, session_id, event_type, data
    ///
    /// Metrics captured for dissertation evaluation:
    ///   - Task completion time (NAVIGATION_STARTED -> ARRIVAL delta)
    ///   - Fallback rate (FALLBACK events / total queries)
    ///   - Marker detection latency (QUERY_START -> first MARKER_DETECTED)
    ///
    /// Attach to: Managers in ARScene (so it persists for the whole session).
    /// </summary>
    public class SessionLogger : MonoBehaviour
    {
        // Inspector Fields
        [Header("Config")]
        [SerializeField] private bool   writeToFile    = true;
        [SerializeField] private string logFilePrefix  = "ARLibraryNav_Session";

        // Runtime State
        private string       _sessionID;
        private StreamWriter _writer;
        private bool         _isOpen = false;

        // Unity Lifecycle
        private void Awake()
        {
            _sessionID = Guid.NewGuid().ToString("N").Substring(0, 8); // e.g. "a3f2c891"

            if (writeToFile)
                OpenLogFile();

            LogEvent("SESSION_START", $"session={_sessionID}");
        }

        private void OnApplicationQuit()
        {
            EndSession();
        }

        private void OnDestroy()
        {
            // Safeguard: close file if object is destroyed mid-session
            if (_isOpen)
                CloseLogFile();
        }

        // Public API

        /// <summary>
        /// Writes a single CSV row: timestamp, session_id, event_type, data.
        /// All public log methods route through here.
        ///
        /// Example event types: QUERY_START, CLASSIFIED_LABEL, NAVIGATION_STARTED,
        ///                      FALLBACK, MARKER_DETECTED, ARRIVAL,
        ///                      TREASURE_HUNT_START, CLUE_REACHED, SESSION_START, SESSION_END.
        /// </summary>
        public void LogEvent(string eventType, string data = "")
        {
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            string row       = $"{timestamp},{_sessionID},{eventType},{EscapeCSV(data)}";

            Debug.Log($"[SessionLogger] {row}");

            if (_isOpen && _writer != null)
            {
                _writer.WriteLine(row);
                _writer.Flush(); // Flush immediately so data isn't lost if app crashes
            }
        }

        /// <summary>Closes the log file and writes the SESSION_END row. Call before scene change.</summary>
        public void EndSession()
        {
            LogEvent("SESSION_END", $"session={_sessionID}");
            CloseLogFile();
        }

        // Private

        private void OpenLogFile()
        {
            try
            {
                string fileName = $"{logFilePrefix}_{_sessionID}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
                string filePath = Path.Combine(Application.persistentDataPath, fileName);

                _writer = new StreamWriter(filePath, append: false, System.Text.Encoding.UTF8);
                _writer.WriteLine("timestamp,session_id,event_type,data"); // CSV header
                _writer.Flush();
                _isOpen = true;

                Debug.Log($"[SessionLogger] Log file opened: {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SessionLogger] Failed to open log file: {ex.Message}");
                _isOpen = false;
            }
        }

        private void CloseLogFile()
        {
            if (!_isOpen) return;

            try
            {
                _writer?.Flush();
                _writer?.Close();
                _writer?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SessionLogger] Error closing log file: {ex.Message}");
            }
            finally
            {
                _isOpen = false;
                _writer = null;
            }
        }

        /// <summary>
        /// Wraps a CSV field in quotes if it contains commas or quotes.
        /// Escapes internal double-quotes by doubling them (RFC 4180).
        /// </summary>
        private static string EscapeCSV(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }
}

using System;
using UnityEngine;

namespace ARLibraryNav.AR
{
    /// <summary>
    /// Unity C# bridge to the Android ML Kit Text Recognition plugin.
    ///
    /// Attach to any active GameObject in the scene (e.g. Managers).
    /// The GameObject's name is registered with ML Kit so UnitySendMessage can reach it.
    ///
    /// Usage:
    ///   shelfLabelOCR.ReadText(jpegBytes, onSuccess, onFailure);
    ///
    /// In the Unity Editor, returns a hardcoded placeholder string so other
    /// code can be tested without a real device.
    /// </summary>
    public class ShelfLabelOCR : MonoBehaviour
    {
        // Constants
        private const string JavaClass = "com.ARLibraryNav.mlkit.ShelfLabelReader";

        /// <summary>
        /// The name this GameObject must have so UnitySendMessage can find it.
        /// Wired at Awake do not rename the GameObject manually.
        /// </summary>
        private const string CallbackObjectName = "ShelfLabelOCR_Bridge";

        // State
        private Action<string> _pendingSuccess;
        private Action<string> _pendingFailure;
        private bool           _busy;

        // Lifecycle
        private void Awake()
        {
            // UnitySendMessage looks up GameObjects by name this must match CallbackObjectName.
            gameObject.name = CallbackObjectName;
        }

        // Public API

        /// <summary>
        /// Sends a JPEG image to ML Kit for text recognition.
        /// Calls onSuccess(text) or onFailure(error) on the Unity main thread.
        /// Only one request may be in-flight at a time.
        /// </summary>
        public void ReadText(byte[] jpegBytes,
                             Action<string> onSuccess,
                             Action<string> onFailure)
        {
            if (jpegBytes == null || jpegBytes.Length == 0)
            {
                onFailure?.Invoke("No image data provided.");
                return;
            }

            if (_busy)
            {
                onFailure?.Invoke("OCR already in progress — please wait.");
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            _busy           = true;
            _pendingSuccess = onSuccess;
            _pendingFailure = onFailure;

            try
            {
                using var javaClass = new AndroidJavaClass(JavaClass);
                javaClass.CallStatic("readText", jpegBytes, CallbackObjectName);
            }
            catch (Exception ex)
            {
                _busy = false;
                _pendingSuccess = null;
                _pendingFailure = null;
                Debug.LogError($"[ShelfLabelOCR] Failed to call Java plugin: {ex.Message}");
                onFailure?.Invoke($"Plugin call failed: {ex.Message}");
            }
#else
            // Editor / non-Android fallback 
            // Returns realistic placeholder text so Book Search can be tested
            // in the Editor without a device.
            Debug.Log("[ShelfLabelOCR] Editor mode — returning placeholder OCR text.");
            string placeholder =
                "Management / Business\n" +
                "Decision making and information management [658.4032]\n" +
                "Project management [658.404]\n" +
                "Executive leadership [658.4092]";
            onSuccess?.Invoke(placeholder);
#endif
        }

        /// <summary>Called by Java ShelfLabelReader when text recognition succeeds.</summary>
        public void OnMLKitSuccess(string text)
        {
            Debug.Log($"[ShelfLabelOCR] ML Kit success. Extracted {text.Length} chars.");
            _busy = false;
            var cb = _pendingSuccess;
            _pendingSuccess = null;
            _pendingFailure = null;
            cb?.Invoke(text);
        }

        /// <summary>Called by Java ShelfLabelReader when text recognition fails.</summary>
        public void OnMLKitFailure(string error)
        {
            Debug.LogWarning($"[ShelfLabelOCR] ML Kit failure: {error}");
            _busy = false;
            var cb = _pendingFailure;
            _pendingSuccess = null;
            _pendingFailure = null;
            cb?.Invoke(error);
        }
    }
}

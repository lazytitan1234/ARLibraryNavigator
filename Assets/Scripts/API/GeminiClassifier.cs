using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using ARLibraryNav.Data;

namespace ARLibraryNav.API
{
    /// <summary>
    /// Sends natural-language queries to the Google Gemini API and returns a canonical genre label.
    ///
    /// Uses UnityWebRequest coroutines (no external SDK required).
    /// Temperature is set to 0.1 for near-deterministic genre classification output.
    ///
    /// API key is loaded from Resources/gemini_api_key.txt at runtime.
    /// Add that file to .gitignore to avoid committing credentials.
    ///
    /// Attach to: Managers in ARScene.
    /// Inspector: drag LibraryGenreMapper.asset into genreMapper field.
    /// </summary>
    public class GeminiClassifier : MonoBehaviour
    {
        // Inspector Fields
        [Header("Config")]
        [SerializeField] private string modelEndpoint =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

        [SerializeField]
        [Tooltip("Timeout in seconds for each Gemini API request.")]
        private float timeoutSeconds = 25f;

        [Header("Data")]
        [SerializeField] private GenreMapper genreMapper;

        // Private State
        private string _apiKey;

        // Rate limiting is handled by the shared GeminiRateLimiter static class,
        // which covers GeminiClassifier AND LibraryDatabaseSearch so they never
        // fight each other for the same API quota.

        // Lifecycle
        private void Awake()
        {
            LoadApiKey();
        }

        // Public API

        /// <summary>
        /// Classifies a free-text student query into a canonical genre label.
        /// Calls onSuccess(canonicalLabel) on success, onFailure(errorMessage) on error.
        /// Both callbacks are invoked on the main thread (safe for UI updates).
        /// </summary>
        public void ClassifyQuery(string userQuery,
                                  Action<string> onSuccess,
                                  Action<string> onFailure)
        {
            if (string.IsNullOrWhiteSpace(userQuery))
            {
                onFailure?.Invoke("Query is empty.");
                return;
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                onFailure?.Invoke("Gemini API key not found. " +
                                  "Add your key to Assets/Resources/gemini_api_key.txt");
                return;
            }

            StartCoroutine(PostToGemini(userQuery, onSuccess, onFailure));
        }

        /// <summary>
        /// Rephrases a clue string for the Treasure Hunt mode.
        /// Sends a rephrasing prompt and returns the result via onSuccess.
        /// Falls back via onFailure if the API is unavailable.
        /// Uses higher temperature (0.7) and more tokens for creative output.
        /// </summary>
        public void RephraseClue(string rawClueText,
                                 Action<string> onSuccess,
                                 Action<string> onFailure)
        {
            if (string.IsNullOrWhiteSpace(rawClueText))
            {
                onFailure?.Invoke("Clue text is empty.");
                return;
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                onFailure?.Invoke("Gemini API key not found.");
                return;
            }

            string rephrasePrompt =
                $"Rephrase the following library treasure hunt clue as a fun, engaging riddle for students. " +
                $"Keep it concise (1-3 sentences max). Preserve the meaning — the student must still find the same place. " +
                $"Do not reveal the answer directly. Use playful, curious language.\n\n" +
                $"Original clue: \"{rawClueText}\"\n\n" +
                $"Respond with ONLY the rephrased clue, nothing else.";

            StartCoroutine(PostRaw(rephrasePrompt, onSuccess, onFailure,
                maxOutputTokens: 150, temperature: 0.7f));
        }

        /// <summary>
        /// Checks arrival using reference photos for comparison (few shot visual matching).
        /// Sends 1-3 reference images of the target location alongside the live camera frame.
        /// Gemini compares them visually no text description needed.
        /// Falls back to text-description overload if referenceImages is null or empty.
        /// </summary>
        public void CheckArrival(
            Texture2D[]    referenceImages,
            string         fallbackDescription,
            byte[]         liveJpegBytes,
            Action         onArrived,
            Action<string> onNotArrived)
        {
            // Fall back to text-description method if no reference images assigned
            if (referenceImages == null || referenceImages.Length == 0)
            {
                CheckArrival(fallbackDescription, liveJpegBytes, onArrived, onNotArrived);
                return;
            }

            if (liveJpegBytes == null || liveJpegBytes.Length == 0)
            {
                onNotArrived?.Invoke("No live image data.");
                return;
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                onNotArrived?.Invoke("API key not found.");
                return;
            }

            // Encode reference images use RenderTexture blit for compressed (non readable) textures
            var refBase64List = new System.Collections.Generic.List<string>();
            foreach (var tex in referenceImages)
            {
                if (tex == null) continue;
                byte[] refJpeg = EncodeTextureToJpg(tex, 85);
                if (refJpeg != null && refJpeg.Length > 0)
                    refBase64List.Add(System.Convert.ToBase64String(refJpeg));
            }

            if (refBase64List.Count == 0)
            {
                // All reference textures were null — fall back to text
                CheckArrival(fallbackDescription, liveJpegBytes, onArrived, onNotArrived);
                return;
            }

            string liveBase64 = System.Convert.ToBase64String(liveJpegBytes);
            StartCoroutine(PostVisionMultiImage(refBase64List, liveBase64, onArrived, onNotArrived));
        }

        /// <summary>
        /// Original text-description arrival check. Used as fallback when no reference images exist.
        /// Sends a JPEG camera frame to Gemini Vision and asks whether a specific target is visible.
        /// Calls onArrived() if Gemini replies YES; calls onNotArrived(reason) otherwise.
        /// </summary>
        public void CheckArrival(
            string visionTargetDescription,
            byte[] jpegBytes,
            Action         onArrived,
            Action<string> onNotArrived)
        {
            if (jpegBytes == null || jpegBytes.Length == 0)
            {
                onNotArrived?.Invoke("No image data.");
                return;
            }

            if (string.IsNullOrWhiteSpace(visionTargetDescription))
            {
                onNotArrived?.Invoke("No target description.");
                return;
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                onNotArrived?.Invoke("API key not found.");
                return;
            }

            string prompt =
                $"Look at this image carefully.\n" +
                $"Is the following visible: \"{visionTargetDescription}\"?\n\n" +
                $"Reply with ONLY one word: YES or NO.";

            string base64Image = System.Convert.ToBase64String(jpegBytes);

            StartCoroutine(PostVision(prompt, base64Image, raw =>
            {
                string answer = raw.Trim().ToUpperInvariant();
                if (answer.StartsWith("YES"))
                    onArrived?.Invoke();
                else
                    onNotArrived?.Invoke($"Not there yet (Gemini: {raw.Trim()})");
            }, onNotArrived));
        }

        /// <summary>
        /// Sends a JPEG camera frame to Gemini Vision and asks it to identify the user's
        /// current library location based on nodeDescriptions (nodeID → visual description).
        /// Only nodes with non-empty visualDescription should be included in the dictionary.
        /// Calls onSuccess(nodeID) may return "UNKNOWN"; caller handles that case.
        /// </summary>
        public void IdentifyLocation(
            Dictionary<string, string> nodeDescriptions,
            byte[] jpegBytes,
            Action<string> onSuccess,
            Action<string> onFailure)
        {
            if (jpegBytes == null || jpegBytes.Length == 0)
            {
                onFailure?.Invoke("No image data.");
                return;
            }

            if (nodeDescriptions == null || nodeDescriptions.Count == 0)
            {
                onFailure?.Invoke("No node descriptions.");
                return;
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                onFailure?.Invoke("API key not found.");
                return;
            }

            string base64Image = System.Convert.ToBase64String(jpegBytes);
            string prompt      = BuildLocationPrompt(nodeDescriptions);
            StartCoroutine(PostVision(prompt, base64Image, onSuccess, onFailure));
        }

        private string BuildLocationPrompt(Dictionary<string, string> nodeDescriptions)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are a library navigation assistant.");
            sb.AppendLine("Examine the image and identify which library location is shown.");
            sb.AppendLine();
            sb.AppendLine("Possible locations (ID: Visual Description):");
            foreach (var kvp in nodeDescriptions)
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- Reply with ONLY the location ID (e.g. L2_Entrance). No preamble or explanation.");
            sb.AppendLine("- The ID must be the very first thing in your response.");
            sb.AppendLine("- If the image does not clearly match any location, reply: UNKNOWN");
            return sb.ToString();
        }

        // Private

        private void LoadApiKey()
        {
            var keyAsset = Resources.Load<TextAsset>("gemini_api_key");
            if (keyAsset != null)
            {
                _apiKey = keyAsset.text.Trim();
            }
            else
            {
                Debug.LogWarning("[GeminiClassifier] gemini_api_key.txt not found in Resources. " +
                                 "Create Assets/Resources/gemini_api_key.txt with your API key.");
            }
        }

        /// <summary>Sends a classification prompt and normalises the response.</summary>
        private IEnumerator PostToGemini(string userQuery,
                                         Action<string> onSuccess,
                                         Action<string> onFailure)
        {
            string prompt = BuildClassificationPrompt(userQuery);
            yield return PostRaw(prompt, rawResponse =>
            {
                // Normalise raw LLM output → canonical label
                string canonical = genreMapper != null
                    ? genreMapper.Normalize(rawResponse)
                    : rawResponse.Trim();

                if (!string.IsNullOrEmpty(canonical))
                {
                    Debug.Log($"[GeminiClassifier] '{userQuery}' → '{canonical}'");
                    onSuccess?.Invoke(canonical);
                }
                else
                {
                    string msg = $"Gemini returned unrecognised label: '{rawResponse}'";
                    Debug.LogWarning($"[GeminiClassifier] {msg}");
                    onFailure?.Invoke(msg);
                }
            }, onFailure);
        }

        /// <summary>Sends any prompt and returns the raw text response.</summary>
        private IEnumerator PostRaw(string prompt,
                                    Action<string> onSuccess,
                                    Action<string> onFailure,
                                    int maxOutputTokens = 50,
                                    float temperature = 0.1f)
        {
            string url     = $"{modelEndpoint}?key={_apiKey}";
            string body    = BuildRequestBody(prompt, maxOutputTokens, temperature);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);

            // Shared rate limit waits if another Gemini caller fired recently.
            float waitTime = GeminiRateLimiter.SecondsUntilReady;
            if (waitTime > 0f)
            {
                Debug.Log($"[GeminiClassifier] PostRaw: rate limiter waiting {waitTime:F2}s");
                yield return new WaitForSeconds(waitTime);
            }
            GeminiRateLimiter.RecordRequest();

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = Mathf.RoundToInt(timeoutSeconds);

            float postRawStart = Time.realtimeSinceStartup;
            yield return request.SendWebRequest();
            float postRawElapsed = Time.realtimeSinceStartup - postRawStart;

            if (request.result != UnityWebRequest.Result.Success)
            {
                string errBody = request.downloadHandler?.text ?? "(no body)";
                if (errBody.Length > 500) errBody = errBody.Substring(0, 500) + "...";
                string err = $"Network error: {request.error} (HTTP {request.responseCode})";
                Debug.LogWarning($"[GeminiClassifier] PostRaw failed in {postRawElapsed:F1}s — {err}\nBody: {errBody}");
                onFailure?.Invoke(err);
                yield break;
            }

            Debug.Log($"[GeminiClassifier] PostRaw succeeded in {postRawElapsed:F1}s (HTTP {request.responseCode})");
            string jsonResponse = request.downloadHandler.text;
            string extracted    = ParseGeminiResponse(jsonResponse);

            if (string.IsNullOrEmpty(extracted))
            {
                string err = "Failed to parse Gemini response.";
                Debug.LogWarning($"[GeminiClassifier] {err}\nRaw: {jsonResponse}");
                onFailure?.Invoke(err);
                yield break;
            }

            onSuccess?.Invoke(extracted);
        }

        /// <summary>
        /// Sends a multimodal (text + image) request to Gemini Vision for location identification.
        /// Reuses ParseGeminiResponse the response envelope is identical to text only requests.
        /// maxOutputTokens is 20 (down from 50) a node ID is ≤6 tokens; faster and cheaper.
        /// </summary>
        private IEnumerator PostVision(
            string prompt,
            string base64ImageData,
            Action<string> onSuccess,
            Action<string> onFailure)
        {
            string url = $"{modelEndpoint}?key={_apiKey}";

            string escapedPrompt = prompt
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");

            string body =
                "{\"contents\":[{\"parts\":[" +
                "{\"text\":\"" + escapedPrompt + "\"}," +
                "{\"inlineData\":{\"mimeType\":\"image/jpeg\",\"data\":\"" + base64ImageData + "\"}}" +
                "]}]," +
                "\"generationConfig\":{\"temperature\":0.1,\"maxOutputTokens\":100," +
                "\"thinkingConfig\":{\"thinkingBudget\":0}}}";

            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);

            // Shared rate limit covers vision AND book search calls globally.
            float waitTimeV = GeminiRateLimiter.SecondsUntilReady;
            if (waitTimeV > 0f)
            {
                Debug.Log($"[GeminiClassifier] PostVision: rate limiter waiting {waitTimeV:F2}s");
                yield return new WaitForSeconds(waitTimeV);
            }
            GeminiRateLimiter.RecordRequest();

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Mathf.RoundToInt(timeoutSeconds);

            float postVisionStart = Time.realtimeSinceStartup;
            yield return req.SendWebRequest();
            float postVisionElapsed = Time.realtimeSinceStartup - postVisionStart;

            if (req.result != UnityWebRequest.Result.Success)
            {
                string errBody = req.downloadHandler?.text ?? "(no body)";
                if (errBody.Length > 500) errBody = errBody.Substring(0, 500) + "...";
                string err = $"Vision error: {req.error} (HTTP {req.responseCode})";
                Debug.LogWarning($"[GeminiClassifier] PostVision failed in {postVisionElapsed:F1}s — {err}\nBody: {errBody}");
                onFailure?.Invoke(err);
                yield break;
            }

            Debug.Log($"[GeminiClassifier] PostVision succeeded in {postVisionElapsed:F1}s (HTTP {req.responseCode})");

            string extracted = ParseGeminiResponse(req.downloadHandler.text)?.Trim();
            if (string.IsNullOrEmpty(extracted))
            {
                string err = "Failed to parse vision response.";
                Debug.LogWarning($"[GeminiClassifier] {err}\nRaw: {req.downloadHandler.text}");
                onFailure?.Invoke(err);
                yield break;
            }

            Debug.Log($"[GeminiClassifier] Vision result: '{extracted}'");
            onSuccess?.Invoke(extracted); // passes "UNKNOWN" upstream — caller handles it
        }

        /// <summary>
        /// Builds the classification prompt.
        /// The valid label list is generated at runtime from GenreMapper so it always
        /// reflects the current state of LibraryGenreMapper.asset.
        /// </summary>
        private string BuildClassificationPrompt(string userQuery)
        {
            string labelList = genreMapper != null
                ? string.Join(", ", genreMapper.GetAllCanonicalLabels())
                : "Unknown";

            return
                $"You are a library classification assistant. " +
                $"A student has asked about a book or topic.\n\n" +
                $"Classify their query into exactly ONE of the following categories:\n" +
                $"{labelList}\n\n" +
                $"Student query: \"{userQuery}\"\n\n" +
                $"Rules:\n" +
                $"- Respond with ONLY the category name from the list above.\n" +
                $"- Do not include any explanation, punctuation, or extra words.\n" +
                $"- If the query does not match any category, respond with: UNKNOWN";
        }

        /// <summary>
        /// Builds the JSON request body for the Gemini generateContent API.
        /// Low temperature (0.1) keeps output deterministic for classification tasks.
        /// maxOutputTokens (50) prevents runaway verbose responses.
        /// </summary>
        private string BuildRequestBody(string prompt,
                                        int maxOutputTokens = 50,
                                        float temperature = 0.1f)
        {
            // Manual JSON construction avoids requiring a JSON library dependency.
            // The prompt is escaped to handle quotes and special characters.
            string escapedPrompt = prompt
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");

            return
                "{\n" +
                "  \"contents\": [{\n" +
                "    \"parts\": [{\"text\": \"" + escapedPrompt + "\"}]\n" +
                "  }],\n" +
                "  \"generationConfig\": {\n" +
                $"    \"temperature\": {temperature:F1},\n" +
                $"    \"maxOutputTokens\": {maxOutputTokens},\n" +
                "    \"thinkingConfig\": {\"thinkingBudget\": 0}\n" +
                "  }\n" +
                "}";
        }

        /// <summary>
        /// Sends multiple reference images + one live camera image to Gemini Vision.
        /// Asks Gemini whether the live image shows the same location as the reference images.
        /// This is few-shot visual prompting more accurate than text descriptions.
        /// </summary>
        private IEnumerator PostVisionMultiImage(
            System.Collections.Generic.List<string> refBase64List,
            string liveBase64,
            Action onArrived,
            Action<string> onNotArrived)
        {
            string url = $"{modelEndpoint}?key={_apiKey}";

            // Build prompt
            int refCount = refBase64List.Count;
            string promptText =
                $"You are helping verify whether a student has found the correct target in a treasure hunt.\n" +
                $"The first {refCount} image(s) are REFERENCE photos of the target (it could be a location, object, or scene).\n" +
                $"The last image is a LIVE photo taken by the student right now.\n\n" +
                $"Does the live photo show the same target as the reference photos?\n" +
                $"Consider: same object, same furniture, same scene — even if the angle or distance is different.\n" +
                $"Be generous: if the main subject clearly matches, answer YES.\n\n" +
                $"Reply with ONLY one word: YES or NO.";

            // Escape prompt for JSON
            string escapedPrompt = promptText
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");

            // Build parts array: text prompt + reference images + live image
            var partsBuilder = new System.Text.StringBuilder();
            partsBuilder.Append("{\"text\":\"" + escapedPrompt + "\"}");

            foreach (var refB64 in refBase64List)
                partsBuilder.Append(",{\"inlineData\":{\"mimeType\":\"image/jpeg\",\"data\":\"" + refB64 + "\"}}");

            partsBuilder.Append(",{\"inlineData\":{\"mimeType\":\"image/jpeg\",\"data\":\"" + liveBase64 + "\"}}");

            string body =
                "{\"contents\":[{\"parts\":[" + partsBuilder + "]}]," +
                "\"generationConfig\":{\"temperature\":0.1,\"maxOutputTokens\":50," +
                "\"thinkingConfig\":{\"thinkingBudget\":0}}}";

            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(body);

            // Rate limit
            float waitTime = GeminiRateLimiter.SecondsUntilReady;
            if (waitTime > 0f)
            {
                Debug.Log($"[GeminiClassifier] PostVisionMultiImage: rate limiter waiting {waitTime:F2}s");
                yield return new WaitForSeconds(waitTime);
            }
            GeminiRateLimiter.RecordRequest();

            Debug.Log($"[GeminiClassifier] PostVisionMultiImage: sending {refBase64List.Count} ref images + live frame ({bodyRaw.Length / 1024} KB)");

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Mathf.RoundToInt(timeoutSeconds);

            float multiImgStart = Time.realtimeSinceStartup;
            yield return req.SendWebRequest();
            float multiImgElapsed = Time.realtimeSinceStartup - multiImgStart;

            if (req.result != UnityWebRequest.Result.Success)
            {
                string errBody = req.downloadHandler?.text ?? "(no body)";
                if (errBody.Length > 500) errBody = errBody.Substring(0, 500) + "...";
                string err = $"Multi-image vision error: {req.error} (HTTP {req.responseCode})";
                Debug.LogWarning($"[GeminiClassifier] PostVisionMultiImage failed in {multiImgElapsed:F1}s — {err}\nBody: {errBody}");
                onNotArrived?.Invoke(err);
                yield break;
            }

            Debug.Log($"[GeminiClassifier] PostVisionMultiImage succeeded in {multiImgElapsed:F1}s (HTTP {req.responseCode})");

            string rawJson = req.downloadHandler.text;
            string extracted = ParseGeminiResponse(rawJson)?.Trim();
            string rawPreview = rawJson.Length > 300 ? rawJson.Substring(0, 300) + "..." : rawJson;
            Debug.Log($"[GeminiClassifier] Multi-image arrival check result: '{extracted}' | Raw: {rawPreview}");

            if (string.IsNullOrEmpty(extracted))
            {
                string preview = rawJson.Length > 400 ? rawJson.Substring(0, 400) + "..." : rawJson;
                Debug.LogWarning($"[GeminiClassifier] Multi-image parse failed. Raw response:\n{preview}");
                onNotArrived?.Invoke("Failed to parse multi-image vision response.");
                yield break;
            }

            if (extracted.ToUpperInvariant().StartsWith("YES"))
                onArrived?.Invoke();
            else
                onNotArrived?.Invoke($"Not there yet (Gemini: {extracted})");
        }

        /// <summary>
        /// Encodes a Texture2D to JPEG, handling compressed (non readable) textures by
        /// blitting through a RenderTexture first. On Android, Unity compresses imported
        /// textures to ETC2/ASTC which cannot be encoded directly the blit converts them
        /// to an uncompressed readable format before encoding.
        /// </summary>
        /// <summary>
        /// Encodes a Texture2D to JPEG, resizing to maxDimension on the longest side.
        /// Always blits through a RenderTexture to handle compressed/non-readable
        /// Android formats (ETC2/ASTC). The resize reduces payload size dramatically
        /// for high-resolution reference photos sent to Gemini Vision.
        /// </summary>
        private static byte[] EncodeTextureToJpg(Texture2D tex, int quality, int maxDimension = 512)
        {
            // Calculate target size maintaining aspect ratio
            int w = tex.width;
            int h = tex.height;
            if (w > maxDimension || h > maxDimension)
            {
                if (w >= h) { h = Mathf.RoundToInt(h * maxDimension / (float)w); w = maxDimension; }
                else        { w = Mathf.RoundToInt(w * maxDimension / (float)h); h = maxDimension; }
            }

            // Blit into a resized RenderTexture — handles compression AND resize in one pass
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt);

            var readable = new Texture2D(w, h, TextureFormat.RGB24, false);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            readable.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            readable.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            byte[] jpg = readable.EncodeToJPG(quality);
            Destroy(readable);
            return jpg;
        }

        /// <summary>
        /// Extracts the text content from a Gemini generateContent JSON response.
        /// Expected structure:
        ///   { "candidates": [{ "content": { "parts": [{ "text": "..." }] } }] }
        /// </summary>
        private string ParseGeminiResponse(string json)
        {
            try
            {
                // Locate "text": "..." in the JSON — simple string search to avoid JSON library dependency.
                const string textKey = "\"text\":";
                int textIdx = json.IndexOf(textKey, StringComparison.Ordinal);
                if (textIdx < 0) return null;

                int start = json.IndexOf('"', textIdx + textKey.Length);
                if (start < 0) return null;
                start++; // move past the opening quote

                int end = start;
                while (end < json.Length)
                {
                    if (json[end] == '"' && json[end - 1] != '\\') break;
                    end++;
                }

                if (end >= json.Length) return null;

                string raw = json.Substring(start, end - start);

                // Unescape basic JSON escape sequences
                raw = raw
                    .Replace("\\n", "\n")
                    .Replace("\\r", "\r")
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");

                return raw.Trim();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GeminiClassifier] Exception parsing response: {ex.Message}");
                return null;
            }
        }
    }
}

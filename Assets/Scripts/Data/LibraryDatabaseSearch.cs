using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using ARLibraryNav.API;

namespace ARLibraryNav.Data
{
    /// <summary>
    /// Result returned by LibraryDatabaseSearch.Search().
    /// ShelfId maps directly to a NavGraph nodeID for NavigationController.StartNavigation().
    /// </summary>
    public class LibrarySearchResult
    {
        public string ShelfId;
        public string ShelfLabel;
        public int    Floor;
        public string MatchedTopic;
        public string CallNumber;

        public bool Found => !string.IsNullOrEmpty(ShelfId);
    }

    /// <summary>
    /// Searches the MCAST library database using Gemini for semantic understanding.
    ///
    /// Primary path Gemini full-database search:
    ///   Sends the user's query alongside the full database text (~4 K tokens) in a
    ///   single API call.  Gemini returns a JSON object with the best matching shelf,
    ///   topic, and call number.  This handles natural-language queries like
    ///   "how to build a house" -> Architecture [720] correctly.
    ///
    /// Fallback local keyword search (no API):
    ///   If Gemini is unavailable (no key / 429 / timeout), falls back to scoring
    ///   shelf labels then topics with keyword overlap.  Still works for simple
    ///   literal queries like "accounting" or "biology".
    ///
    /// No startup API calls the database text is loaded once from Resources on
    /// Awake and kept in memory.  Only one API call is made per user search.
    ///
    /// Database file: Assets/Resources/library_database.txt
    /// Attach to: Managers in ARScene.
    /// </summary>
    public class LibraryDatabaseSearch : MonoBehaviour
    {
        // Internal types

        private struct ShelfEntry
        {
            public string ShelfId;
            public string ShelfLabel;
            public int    Floor;
            public List<(string Topic, string CallNumber)> Topics;
        }

        // Endpoint

        private const string GENERATE_URL =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-001:generateContent";

        // Inspector

        [SerializeField] private float timeoutSeconds = 15f;

        // State
        private readonly List<ShelfEntry> _shelves = new List<ShelfEntry>();
        private string _apiKey;
        private string _dbText;

        public bool IsReady    { get; private set; }
        public int  ShelfCount => _shelves.Count;

        // Stop words (keyword fallback only)

        private static readonly HashSet<string> StopWords =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "a","an","the","how","to","do","i","me","my","we","find",
                "book","books","about","on","of","in","for","with","want",
                "looking","where","what","get","help","need","is","are",
                "can","some","any","like","tell","show","give"
            };

        // Lifecycle

        private void Awake()
        {
            var keyAsset = Resources.Load<TextAsset>("gemini_api_key");
            _apiKey = keyAsset?.text.Trim();

            var dbAsset = Resources.Load<TextAsset>("library_database");
            _dbText = dbAsset?.text;

            if (string.IsNullOrEmpty(_apiKey))
                Debug.LogWarning("[LibraryDatabaseSearch] gemini_api_key.txt not found in Resources.");
            if (string.IsNullOrEmpty(_dbText))
                Debug.LogWarning("[LibraryDatabaseSearch] library_database.txt not found in Resources.");

            LoadDatabase();
        }

        // Public API

        /// <summary>
        /// Searches the library for the best shelf matching the natural language query.
        /// One Gemini API call per search.  Falls back to local keywords if Gemini fails.
        /// </summary>
        public void Search(string                      query,
                           Action<LibrarySearchResult> onSuccess,
                           Action<string>              onFailure)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                onFailure?.Invoke("Query is empty.");
                return;
            }
            if (!IsReady)
            {
                onFailure?.Invoke("Library database not loaded.");
                return;
            }

            StartCoroutine(DoSearch(query, onSuccess, onFailure));
        }

        // Search pipeline

        private IEnumerator DoSearch(string                      query,
                                     Action<LibrarySearchResult> onSuccess,
                                     Action<string>              onFailure)
        {
            // Primary: Gemini full-database search
            LibrarySearchResult geminiResult = null;

            if (!string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_dbText))
                yield return StartCoroutine(GeminiSearch(query, r => geminiResult = r));

            if (geminiResult != null && geminiResult.Found)
            {
                Debug.Log($"[LibraryDatabaseSearch] Gemini -> {geminiResult.ShelfLabel} / " +
                          $"{geminiResult.MatchedTopic} [{geminiResult.CallNumber}]");
                onSuccess?.Invoke(geminiResult);
                yield break;
            }

            // Fallback: local keyword search
            Debug.Log("[LibraryDatabaseSearch] Gemini unavailable or returned no match — trying local fallback.");

            LibrarySearchResult localResult = LocalFallback(query);

            if (localResult != null && localResult.Found)
            {
                Debug.Log($"[LibraryDatabaseSearch] Local fallback -> {localResult.ShelfLabel} / " +
                          $"{localResult.MatchedTopic} [{localResult.CallNumber}]");
                onSuccess?.Invoke(localResult);
            }
            else
            {
                onFailure?.Invoke($"No shelf found for \"{query}\".");
            }
        }

        // Gemini full-database search

        private IEnumerator GeminiSearch(string query, Action<LibrarySearchResult> onResult)
        {
            string prompt =
                "You are a library assistant for MCAST library.\n\n" +
                "Here is the complete library database:\n\n" +
                _dbText + "\n\n" +
                "A student is searching for: \"" + query + "\"\n\n" +
                "Find the SINGLE best matching shelf and topic for this search.\n" +
                "Return ONLY this JSON object — no markdown, no code fences, no explanation:\n" +
                "{\"shelf_id\":\"SHELF_B3_17\",\"shelf_label\":\"Accounting / General Management Shelf\"," +
                "\"floor\":1,\"matched_topic\":\"Accounting\",\"call_number\":\"657\"}\n\n" +
                "If absolutely nothing in the database is relevant:\n" +
                "{\"shelf_id\":null,\"shelf_label\":\"Not found\",\"floor\":0," +
                "\"matched_topic\":\"\",\"call_number\":\"\"}";

            string body =
                "{\"contents\":[{\"role\":\"user\",\"parts\":[{\"text\":\"" +
                EscapeForJson(prompt) +
                "\"}]}],\"generationConfig\":{\"temperature\":0.1,\"maxOutputTokens\":200}}";

            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
            string url     = $"{GENERATE_URL}?key={_apiKey}";

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Mathf.RoundToInt(timeoutSeconds);

            // Shared rate limit waits if GeminiClassifier fired recently (vision / treasure hunt).
            float wait = GeminiRateLimiter.SecondsUntilReady;
            if (wait > 0f) yield return new WaitForSeconds(wait);
            GeminiRateLimiter.RecordRequest();

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[LibraryDatabaseSearch] Gemini request failed " +
                                 $"({req.responseCode}: {req.error})");
                onResult?.Invoke(null);
                yield break;
            }

            // Extract text content from Gemini response
            string raw = ParseGeminiTextContent(req.downloadHandler.text);
            if (string.IsNullOrEmpty(raw))
            {
                Debug.LogWarning("[LibraryDatabaseSearch] Could not parse Gemini response.");
                onResult?.Invoke(null);
                yield break;
            }

            // Strip markdown code fences if Gemini added them
            raw = StripCodeFences(raw);

            // Parse the JSON result
            LibrarySearchResult result = ParseJsonResult(raw);
            onResult?.Invoke(result);
        }

        // JSON result parsing

        private LibrarySearchResult ParseJsonResult(string json)
        {
            try
            {
                if (json.Contains("\"shelf_id\":null") || json.Contains("\"shelf_id\": null"))
                    return null;

                string shelfId    = ExtractJsonString(json, "shelf_id");
                string shelfLabel = ExtractJsonString(json, "shelf_label");
                int    floor      = ExtractJsonInt(json, "floor");
                string topic      = ExtractJsonString(json, "matched_topic");
                string callNum    = ExtractJsonString(json, "call_number");

                if (string.IsNullOrEmpty(shelfId)) return null;

                return new LibrarySearchResult
                {
                    ShelfId      = shelfId,
                    ShelfLabel   = shelfLabel ?? shelfId,
                    Floor        = floor,
                    MatchedTopic = topic ?? "",
                    CallNumber   = callNum ?? ""
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LibraryDatabaseSearch] JSON parse error: {ex.Message}");
                return null;
            }
        }

        // Local keyword fallback

        /// <summary>
        /// Two-level local fallback when Gemini is unavailable:
        ///   Level 1 — match shelf labels to find the right shelf, then best topic in it.
        ///   Level 2 — if no shelf label matches, search all topics across all shelves.
        /// </summary>
        private LibrarySearchResult LocalFallback(string query)
        {
            string[] keywords = SplitWords(query.ToLowerInvariant())
                .Where(w => !StopWords.Contains(w))
                .ToArray();

            if (keywords.Length == 0) keywords = SplitWords(query.ToLowerInvariant());
            if (keywords.Length == 0) return null;

            // Level 1: score shelf labels
            float       bestShelfScore = 0f;
            ShelfEntry? bestShelf      = null;

            foreach (ShelfEntry shelf in _shelves)
            {
                string labelLower = shelf.ShelfLabel.ToLowerInvariant();
                foreach (string kw in keywords)
                {
                    float score = ScoreText(labelLower, kw, SplitWords(kw));
                    if (score > bestShelfScore)
                    {
                        bestShelfScore = score;
                        bestShelf      = shelf;
                    }
                }
            }

            if (bestShelf.HasValue && bestShelfScore > 0f)
                return BestTopicInShelf(bestShelf.Value, string.Join(" ", keywords));

            // Level 2: score all topics directly
            float               bestTopicScore = 0f;
            LibrarySearchResult bestResult     = null;

            foreach (ShelfEntry shelf in _shelves)
            {
                foreach (var (topic, callNum) in shelf.Topics)
                {
                    string topicLower = topic.ToLowerInvariant();
                    foreach (string kw in keywords)
                    {
                        float score = ScoreText(topicLower, kw, SplitWords(kw));
                        if (score > bestTopicScore)
                        {
                            bestTopicScore = score;
                            bestResult     = MakeResult(shelf, topic, callNum);
                        }
                    }
                }
            }

            return bestResult;
        }

        // Topic selection within a shelf

        private LibrarySearchResult BestTopicInShelf(ShelfEntry shelf, string queryJoined)
        {
            string[] queryWords = SplitWords(queryJoined);

            if (queryWords.Length == 0)
            {
                var first = shelf.Topics[0];
                return MakeResult(shelf, first.Topic, first.CallNumber);
            }

            float  bestScore = -1f;
            string bestTopic = shelf.Topics[0].Topic;
            string bestCall  = shelf.Topics[0].CallNumber;

            foreach (var (topic, callNum) in shelf.Topics)
            {
                float score = ScoreText(topic.ToLowerInvariant(), queryJoined, queryWords);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTopic = topic;
                    bestCall  = callNum;
                }
            }

            return MakeResult(shelf, bestTopic, bestCall);
        }

        private static LibrarySearchResult MakeResult(ShelfEntry shelf, string topic, string callNum) =>
            new LibrarySearchResult
            {
                ShelfId      = shelf.ShelfId,
                ShelfLabel   = shelf.ShelfLabel,
                Floor        = shelf.Floor,
                MatchedTopic = topic,
                CallNumber   = callNum
            };

        // Text scoring

        private static float ScoreText(string textLower, string queryLower, string[] queryWords)
        {
            if (textLower == queryLower)        return 100f;
            if (textLower.Contains(queryLower)) return 80f;
            if (queryLower.Contains(textLower)) return 70f;

            string[] textWords = SplitWords(textLower);
            int matches = queryWords.Count(qw =>
                textWords.Any(tw => tw == qw || tw.Contains(qw) || qw.Contains(tw)));

            if (matches == 0) return 0f;

            int larger = Mathf.Max(queryWords.Length, textWords.Length);
            return (float)matches / larger * 60f;
        }

        // Database loading

        private void LoadDatabase()
        {
            if (string.IsNullOrEmpty(_dbText))
            {
                Debug.LogError("[LibraryDatabaseSearch] library_database.txt not found in Resources.");
                return;
            }

            ParseDatabase(_dbText);
            IsReady = true;
            Debug.Log($"[LibraryDatabaseSearch] Loaded {_shelves.Count} shelves.");
        }

        private void ParseDatabase(string text)
        {
            _shelves.Clear();

            string pendingLabel = null;
            string pendingId    = null;
            int    pendingFloor = 0;

            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (line.StartsWith("SHELF:", StringComparison.OrdinalIgnoreCase))
                {
                    pendingLabel = line.Substring("SHELF:".Length).Trim();
                    pendingId    = null;
                    pendingFloor = 0;
                }
                else if (line.StartsWith("ID:", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string part in line.Split('|'))
                    {
                        string p = part.Trim();
                        if (p.StartsWith("ID:", StringComparison.OrdinalIgnoreCase))
                            pendingId = p.Substring("ID:".Length).Trim();
                        else if (p.StartsWith("Floor:", StringComparison.OrdinalIgnoreCase))
                            int.TryParse(p.Substring("Floor:".Length).Trim(), out pendingFloor);
                    }
                }
                else if (line.StartsWith("Topics:", StringComparison.OrdinalIgnoreCase) && pendingLabel != null)
                {
                    _shelves.Add(new ShelfEntry
                    {
                        ShelfId    = pendingId    ?? $"SHELF_{_shelves.Count}",
                        ShelfLabel = pendingLabel ?? "Unknown",
                        Floor      = pendingFloor,
                        Topics     = ParseTopics(line.Substring("Topics:".Length).Trim())
                    });
                    pendingLabel = null;
                    pendingId    = null;
                    pendingFloor = 0;
                }
            }
        }

        private static List<(string Topic, string CallNumber)> ParseTopics(string topicsPart)
        {
            var result = new List<(string, string)>();
            int i = 0;

            while (i < topicsPart.Length)
            {
                int bs = topicsPart.IndexOf('[', i);
                if (bs < 0) break;
                int be = topicsPart.IndexOf(']', bs);
                if (be < 0) break;

                string topic = topicsPart.Substring(i, bs - i).Trim().TrimEnd(',').Trim();
                string call  = topicsPart.Substring(bs + 1, be - bs - 1).Trim();

                if (!string.IsNullOrEmpty(topic)) result.Add((topic, call));

                i = be + 1;
                while (i < topicsPart.Length && (topicsPart[i] == ',' || topicsPart[i] == ' '))
                    i++;
            }

            return result;
        }

        // Helpers

        private static string[] SplitWords(string s) =>
            s.Split(new char[] { ' ', '-', ',', '/', '(', ')' },
                    StringSplitOptions.RemoveEmptyEntries);

        private static string ParseGeminiTextContent(string json)
        {
            try
            {
                const string textKey = "\"text\":";
                int idx = json.IndexOf(textKey, StringComparison.Ordinal);
                if (idx < 0) return null;

                int start = json.IndexOf('"', idx + textKey.Length);
                if (start < 0) return null;
                start++;

                int end = start;
                while (end < json.Length)
                {
                    if (json[end] == '"' && json[end - 1] != '\\') break;
                    end++;
                }
                if (end >= json.Length) return null;

                return json.Substring(start, end - start)
                    .Replace("\\n", "\n").Replace("\\r", "\r")
                    .Replace("\\\"", "\"").Replace("\\\\", "\\")
                    .Trim();
            }
            catch { return null; }
        }

        private static string StripCodeFences(string text)
        {
            int fenceStart = text.IndexOf("```", StringComparison.Ordinal);
            if (fenceStart < 0) return text;

            int contentStart = text.IndexOf('\n', fenceStart);
            if (contentStart < 0) return text;
            contentStart++;

            int fenceEnd = text.IndexOf("```", contentStart, StringComparison.Ordinal);
            if (fenceEnd < 0) return text.Substring(contentStart).Trim();

            return text.Substring(contentStart, fenceEnd - contentStart).Trim();
        }

        private static string ExtractJsonString(string json, string key)
        {
            string search = $"\"{key}\":\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0)
            {
                // Try with space after colon
                search = $"\"{key}\": \"";
                idx = json.IndexOf(search, StringComparison.Ordinal);
                if (idx < 0) return null;
            }
            int start = idx + search.Length;
            int end   = json.IndexOf('"', start);
            if (end < 0) return null;
            return json.Substring(start, end - start);
        }

        private static int ExtractJsonInt(string json, string key, int defaultVal = 0)
        {
            // Match "key": N  or "key":N
            string search = $"\"{key}\":";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return defaultVal;
            int start = idx + search.Length;
            while (start < json.Length && (json[start] == ' ' || json[start] == '"')) start++;
            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            if (end == start) return defaultVal;
            return int.TryParse(json.Substring(start, end - start), out int val) ? val : defaultVal;
        }

        private static string EscapeForJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"")
             .Replace("\n", "\\n").Replace("\r", "\\r");
    }
}

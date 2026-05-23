using UnityEngine;

namespace ARLibraryNav.API
{
    /// <summary>
    /// Shared rate limit gate for all outgoing Gemini API calls across the app.
    ///
    /// The Gemini free tier allows 15 RPM = one request every 4 seconds.
    /// GeminiClassifier (Vision, TreasureHunt) and LibraryDatabaseSearch (BookSearch)
    /// both hit the same API key and the same quota, so they must share one timestamp.
    ///
    /// Usage (inside any IEnumerator before SendWebRequest):
    ///
    ///   float wait = GeminiRateLimiter.SecondsUntilReady;
    ///   if (wait > 0f) yield return new WaitForSeconds(wait);
    ///   GeminiRateLimiter.RecordRequest();
    ///   // then send the request
    /// </summary>
    public static class GeminiRateLimiter
    {
        /// <summary>
        /// Minimum gap between any two Gemini requests app-wide.
        /// 4.5 s gives a small buffer above the free-tier floor of 4 s (15 RPM).
        /// </summary>
        public const float MinIntervalSeconds = 4.5f;

        // Shared timestamp — static so all callers see the same value.
        private static float _lastRequestTime = -999f;

        /// <summary>Seconds the caller must wait before sending the next request. 0 if ready now.</summary>
        public static float SecondsUntilReady =>
            Mathf.Max(0f, MinIntervalSeconds - (Time.time - _lastRequestTime));

        /// <summary>
        /// Call this immediately before starting the HTTP request (after any wait).
        /// Records the current time so the next caller waits appropriately.
        /// </summary>
        public static void RecordRequest() => _lastRequestTime = Time.time;
    }
}

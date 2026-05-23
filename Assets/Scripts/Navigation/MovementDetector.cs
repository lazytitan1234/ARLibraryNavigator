using UnityEngine;
using UnityEngine.Events;

namespace ARLibraryNav.Navigation
{
    /// <summary>
    /// Reads the device accelerometer each frame and classifies the phone's movement
    /// into one of five directions: Left, Right, Forward, Backward, Still.
    ///
    /// "Forward/Backward" = phone tilting away from / toward the user (Y axis).
    /// "Left/Right"       = phone tilting sideways (X axis).
    /// "Still"            = net acceleration below threshold.
    ///
    /// Attach to: Managers in ARScene.
    /// Subscribe to OnDirectionChanged or poll CurrentDirection.
    /// </summary>
    public class MovementDetector : MonoBehaviour
    {
        public enum Direction { Still, Forward, Backward, Left, Right }

        [Header("Tuning")]
        [Tooltip("Acceleration magnitude (g) below which the device is considered still.")]
        [SerializeField] private float stillThreshold = 0.12f;

        [Tooltip("Smoothing factor for the low-pass filter (0=no smoothing, 1=frozen).")]
        [Range(0f, 1f)]
        [SerializeField] private float smoothing = 0.8f;

        [Header("Events")]
        public UnityEvent<Direction> OnDirectionChanged;

        public Direction CurrentDirection { get; private set; } = Direction.Still;

        private Vector3    _smoothed = Vector3.zero;
        private Direction  _lastFired = Direction.Still;

        private void Update()
        {
            if (!SystemInfo.supportsAccelerometer) return;

            // Low pass filter to remove jitter
            _smoothed = Vector3.Lerp(Input.acceleration, _smoothed, smoothing);

            Direction dir = Classify(_smoothed);
            CurrentDirection = dir;

            if (dir != _lastFired)
            {
                _lastFired = dir;
                OnDirectionChanged?.Invoke(dir);
            }
        }

        private Direction Classify(Vector3 accel)
        {
            // Remove gravity component (gravity ≈ (0, -1, 0) when phone held upright)
            // accel.x = tilt left/right, accel.y = tilt forward/back, accel.z = depth
            float ax = accel.x;
            float ay = accel.y + 1f; // subtract gravity

            float mag = Mathf.Sqrt(ax * ax + ay * ay);
            if (mag < stillThreshold) return Direction.Still;

            // Dominant axis wins
            if (Mathf.Abs(ax) > Mathf.Abs(ay))
                return ax > 0 ? Direction.Right : Direction.Left;
            else
                return ay > 0 ? Direction.Forward : Direction.Backward;
        }
    }
}

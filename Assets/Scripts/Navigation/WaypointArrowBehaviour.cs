using UnityEngine;
using TMPro;

namespace ARLibraryNav.Navigation
{
    public class WaypointArrowBehaviour : MonoBehaviour
    {
        [Header("Optional Label")]
        [SerializeField] private TextMeshProUGUI label;

        /// <summary>
        /// Points the arrow toward toPosition on the XZ plane.
        /// Optionally moves the arrow to the midpoint between fromPosition and toPosition.
        /// </summary>
        public void SetDirection(Vector3 fromPosition, Vector3 toPosition, bool updatePosition = true)
        {
            if (updatePosition)
            {
                // Place arrow at midpoint between the two nodes
                transform.position = Vector3.Lerp(fromPosition, toPosition, 0.5f);
            }

            Vector3 direction = toPosition - fromPosition;
            direction.y = 0f; // constrain to XZ plane

            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }

        /// <summary>Sets the text label shown above the arrow (optional).</summary>
        public void SetLabel(string text)
        {
            if (label != null)
            {
                label.text          = text;
                label.gameObject.SetActive(!string.IsNullOrEmpty(text));
            }
        }

        /// <summary>Shows or hides this arrow GameObject.</summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
}

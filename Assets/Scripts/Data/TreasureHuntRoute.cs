using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARLibraryNav.Data
{
    [CreateAssetMenu(fileName = "TreasureHuntRoute", menuName = "ARLibraryNav/TreasureHuntRoute")]
    public class TreasureHuntRoute : ScriptableObject
    {
        [Serializable]
        public class Clue
        {
            [Tooltip("The riddle or clue text shown to the student (Gemini will rephrase this).")]
            public string rawClueText;

            [Tooltip("NodeID in the LibraryNavGraph for navigation (Book Search mode only — leave blank for Treasure Hunt).")]
            public string targetNodeID;

            [Tooltip("What Gemini Vision should look for. Used when referenceImages are not assigned.")]
            public string visionTargetDescription;

            [Tooltip("Reference photos of the target (2-3 angles recommended). " +
                     "Gemini compares the live photo against these.")]
            public Texture2D[] referenceImages;

            [Tooltip("Optional variants. If any are added, ONE is randomly chosen each run instead of " +
                     "this clue's own fields. Use this for location alternatives (e.g. Room 241 vs 242). " +
                     "Variants should not contain further variants.")]
            public List<Clue> variants = new List<Clue>();
        }

        [Tooltip("Display name shown on the Start screen.")]
        public string routeName = "Library Treasure Hunt";

        [Tooltip("List of clue slots. The order is shuffled each run.")]
        public List<Clue> clues = new List<Clue>();
    }
}

namespace ARLibraryNav.AppState
{
    /// <summary>
    /// Defines the two primary application modes.
    /// Set on the AppStateManager before loading ARScene.
    /// </summary>
    public enum AppMode
    {
        None,
        BookSearch,
        TreasureHunt,
        /// <summary>
        /// Room-testing mode. Loads ARScene with DebugLocalizationPanel
        /// prominently visible so all features can be tested without
        /// visiting the physical library.
        /// </summary>
        Test
    }
}

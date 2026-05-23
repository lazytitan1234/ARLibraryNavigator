package com.DefaultCompany.ARLibraryNavigator;
import androidx.core.content.FileProvider;
// Subclass avoids duplicate-provider conflicts with the Vuforia FileProvider.
public class ARNavFileProvider extends FileProvider {}

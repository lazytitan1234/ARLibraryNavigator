# AR Library Navigator

An Android application developed as part of an undergraduate dissertation at MCAST (Malta College of Arts, Science and Technology). The AR Library Navigator replaces the traditional passive library orientation with two interactive modes: a gamified AI-powered Treasure Hunt and a structured Book Search tool.

## Overview

Traditional library orientations are passive, one-size-fits-all experiences that fail to produce lasting spatial knowledge or navigational confidence in students. This application addresses that gap by combining:

- **Gamification** a treasure hunt format that physically guides students through the library
- **Large Language Model content generation** Google Gemini dynamically rephrases clues into riddle-style text each session
- **AI Vision confirmation** Gemini Vision API confirms student arrival at each location via photo analysis
- **Practical navigation** a Book Search tool that provides floor-by-floor directions to any subject shelf

The application was developed in Unity 2022.3.15f1 LTS for Android and evaluated with twelve undergraduate students at the MCAST library in May 2026.



## Features

### Treasure Hunt Mode
- Presents students with a sequence of AI-generated riddle clues corresponding to real physical library locations
- Students navigate to each location independently and photograph it using the device camera
- Gemini Vision API confirms arrival with a YES/NO response before unlocking the next clue
- Session events are automatically logged to a timestamped CSV file for analysis
- 100% completion rate achieved across all evaluation sessions

### Book Search Mode
- Students select a subject from a dropdown menu populated from a 1,160-entry library database
- The app returns plain-text floor and shelf directions using a Dijkstra pathfinding algorithm
- Covers 39 shelves across two library floors
- No camera or API calls required fully offline



## Tech Stack

| Technology | Version | Purpose |
|---|---|---|
| Unity | 2022.3.15f1 LTS | Development environment |
| Android | API 24+ | Target platform |
| Google Gemini API | gemini-2.0-flash | Clue rephrasing + Vision arrival confirmation |
| Vuforia Engine | 11.4.4 | AR framework (integrated, evaluated during development) |
| TextMeshPro | — | UI text rendering |
| C# | — | Scripting language |
| Python | 3.x | Database export + results analysis |



## Prerequisites

Before running this project you will need:

- **Unity 2022.3.15f1 LTS** download from [Unity Hub](https://unity.com/download)
- **Android Build Support module** install via Unity Hub -> Installs -> Add Modules
- **JDK 11** Eclipse Adoptium JDK 11 recommended ([download here](https://adoptium.net/))
- **Android SDK** installed automatically with Unity's Android module
- **A Google Gemini API key** free tier available at [Google AI Studio](https://aistudio.google.com/app/apikey)
- **An Android device** running Android 7.0 (API 24) or higher with a working camera



## Setup Instructions

### 1. Clone the repository

```bash
git clone https://github.com/lazytitan1234/ARLibraryNavigator.git
cd ARLibraryNavigator
```

### 2. Add the Vuforia Engine package

The Vuforia Engine package (`com.ptc.vuforia.engine-11.4.4.tgz`) exceeds GitHub's 100MB file size limit and is not included in the repository. You must add it manually before opening the project in Unity:

1. Download **Vuforia Engine 11.4.4** from the [Vuforia Developer Portal](https://developer.vuforia.com/downloads/sdk)
2. Select **Unity Extension (tgz)** as the download format
3. Rename the downloaded file to exactly `com.ptc.vuforia.engine-11.4.4.tgz`
4. Place it in the `Packages/` folder of the cloned repository

> If you skip this step, Unity will show a package resolution error when opening the project.

### 3. Open in Unity

1. Open **Unity Hub**
2. Click **Add project from disk**
3. Select the cloned `ARLibraryNavigator` folder
4. Open with **Unity 2022.3.15f1 LTS**
5. Wait for Unity to import all assets and recompile scripts (this may take a few minutes on first open)

### 3. Add your Gemini API key

The API key is read from a text file at runtime:

1. Navigate to `Assets/Resources/`
2. Create a new text file named exactly `gemini_api_key.txt`
3. Paste your Gemini API key as the only content in the file no spaces, no line breaks
4. Save the file

> **Warning:** This file is listed in `.gitignore` and will never be committed. Never share or upload your API key.

### 4. Configure the Android build

1. In Unity, go to **File -> Build Settings**
2. Select **Android** and click **Switch Platform**
3. Click **Player Settings** and verify:
   - Minimum API Level: Android 7.0 (API 24)
   - Scripting Backend: IL2CPP
   - Target Architecture: ARM64
4. Connect your Android device via USB and enable **USB Debugging** in Developer Options
5. Click **Build and Run**



## Running the Application

Once installed on your Android device:

1. **Launch** the AR Library Navigator app
2. From the **Main Menu**, select either:
   - **Treasure Hunt** begins the guided library exploration
   - **Book Search** opens the subject navigation tool
3. For the Treasure Hunt:
   - Read each riddle clue and navigate to the described library location
   - Tap **Scan Me** when you believe you have arrived
   - Take a clear, well-lit photo of the shelf or location
   - Wait for the Gemini Vision confirmation (approximately 5–10 seconds)
   - If rejected, reposition and try again get closer to the shelf label and ensure good lighting
   - Progress through all four clues to complete the hunt
4. For Book Search:
   - Select a subject from the dropdown menu
   - Optionally enter your current shelf number for floor comparison
   - Follow the plain-text directions displayed on screen



## Project Structure

```
ARLibraryNavigator/
├── Assets/
│   ├── Scripts/
│   │   ├── AppState/          # AppStateManager.cs singleton scene manager
│   │   ├── API/               # GeminiService.cs, GeminiRateLimiter.cs
│   │   ├── Navigation/        # NavGraph.cs, Pathfinder.cs, NavigationController.cs
│   │   ├── Data/              # TreasureHuntRoute.asset, clue definitions
│   │   ├── UI/                # All UI panel controllers
│   │   ├── Logging/           # SessionLogger.cs CSV event logging
│   │   └── AR/                # Vuforia integration scripts
│   ├── Resources/
│   │   ├── library_database.txt    # 1,160 entries across 39 shelves
│   │   └── gemini_api_key.txt      # NOT included add your own (see Setup)
│   ├── Scenes/
│   │   ├── MainMenu.unity
│   │   └── ARScene.unity
│   └── TreasureHuntClues/     # Reference images for Gemini Vision confirmation
├── Packages/                  # Unity package manifest
├── ProjectSettings/           # Android build configuration
├── results.ipynb              # Jupyter notebook evaluation data and charts
└── README.md
```



## Session Logging

The app automatically records all session events to a CSV log file stored on the device at:

```
/storage/emulated/0/Android/data/com.MCAST.ARLibraryNavigator/files/
```

Retrieve logs using Android Debug Bridge:

```bash
adb pull /storage/emulated/0/Android/data/com.MCAST.ARLibraryNavigator/files/ ./logs
```

Logged events include:

`SESSION_START` -> `TREASURE_HUNT_START` -> `CLUE_SHOWN` -> `SCAN_ATTEMPT` -> `CLUE_CONFIRMED / SCAN_REJECTED` -> `TREASURE_HUNT_COMPLETE` -> `BOOK_SEARCH` -> `SESSION_END`



## Evaluation Results

The application was evaluated with twelve undergraduate ICT students at MCAST in May 2026.

| Metric | Result |
|---|---|
| Treasure Hunt engagement mean | 4.32 / 5 |
| Book Search usefulness mean | 4.20 / 5 |
| LLM clue comprehension mean | 4.19 / 5 |
| Overall app rating | 4.25 / 5 |
| Navigation confidence (pre-session) | 2.42 / 5 |
| Navigation confidence (post-session) | 3.92 / 5 |
| Confidence improvement | +1.50 points (62%) |
| Treasure Hunt completion rate | 100% |
| Gemini Vision rejection rate | 69.8% |

Full analysis and visualisations are available in `results.ipynb`.



## Known Limitations

- **Gemini Vision rejection rate (69.8%)** caused by variable lighting and camera angle. Conservative rejection is intentional to prevent false confirmations. Reduce by holding the phone steady and getting close to the shelf label.
- **Gemini API dependency** requires an active internet connection. Free tier limited to 15 requests per minute.
- **Android only** iOS not supported.
- **MCAST library specific** the navigation graph, shelf database, and clue locations are configured for the MCAST library. Reconfiguration is required for other institutions.
- **Manual current location input** Book Search requires the user to know their current shelf number for floor comparison directions.



## Future Work

- Automated indoor positioning using BLE beacons or QR codes at shelf end-caps
- Full AR overlay navigation with directional arrows rendered over the camera feed
- Expanded treasure hunt covering all major library sections
- Multi-language support (Maltese and English)
- Staff analytics dashboard for session log visualisation
- Longitudinal study to measure spatial knowledge retention over time



## Dissertation

This project was submitted as a final-year undergraduate dissertation at the MCAST Institute of Information and Communication Technology.

- **Title:** AR Library Navigator A Gamified, AI-Assisted Mobile Application for Library Orientation
- **Institution:** MCAST Institute of ICT
- **Year:** 2026
- **Research paradigm:** Design Science Research
- **Evaluation:** Mixed-methods (Likert survey + automated session logs)



## Author

**Christian** MCAST Institute of Information and Communication Technology, 2026



## Acknowledgements

Thanks to the MCAST library staff for permitting access during development and evaluation, and to the twelve student participants who took part in the evaluation sessions.

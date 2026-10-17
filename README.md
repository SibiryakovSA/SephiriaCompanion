# Sephiria Companion

[Русская версия](README.ru.md)

A second-screen companion for **Sephiria**, built and tested on **AYN Thor**. Other dual-screen Android handhelds may also work with a compatible PC game build running locally.

The project uses **C#** for the game AddOn and local HTTP/event server, **JavaScript, HTML and CSS** for the interface, and **Java** for the Android WebView app.

The interface supports **English and Russian**. Item names and descriptions follow the game's language.

## Features

- HUD with health, mana, currency, experience, dashes, potions and a room minimap.
- Floor map with room travel and an animated current-room highlight.
- Backpack with drag and drop, tablet rotation and visible bonuses/penalties.
- Item descriptions, artifact levels and combo tiers.

## Screenshots

Captured on AYN Thor. The game was set to Russian; the companion interface was set to English.

| HUD | Floor map |
| :---: | :---: |
| ![HUD](docs/screenshots/hud.png) | ![Floor map](docs/screenshots/map.png) |
| **Backpack** | **Combos** |
| ![Backpack](docs/screenshots/backpack.png) | ![Combos](docs/screenshots/combos.png) |

## Install

1. Copy the release archive's **AddOns** folder into the game directory, next to **Sephiria.exe**.
2. Install **SephiriaCompanion-<version>.apk** on the handheld.
3. Start Sephiria, load a save, open the companion and tap **Refresh**.

Requires Android 8.0+ and a compatible PC version of Sephiria with its built-in AddOn loader. The game runtime and Android app must share localhost networking. Restart the game after replacing the AddOn.

## Build from source

On Windows, install **PowerShell 7**, **.NET SDK 8**, **JDK 17**, and the **Android SDK** with **platform android-34** and **Build Tools 34.0.0**. Set `JAVA_HOME` and `ANDROID_SDK_ROOT`, then run:

```powershell
.\scripts\build.ps1 -GameDirectory "PathToSephiria"
```

Alternatively, pass `-JavaHome` and `-AndroidSdk` explicitly. Output: `artifacts/package-<version>/`, an APK and a ZIP.

Game libraries are referenced from your installation and are not distributed. The build creates a local development signing key under `tools/`; keep it for future APK updates. Toolchains, keys and build output are excluded from Git.

---

Developed with AI assistance using **Codex**.

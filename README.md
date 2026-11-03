# Unijoy Keyboard (`bn-BD`)

[![Build & Release Windows Executables](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-0078D6?logo=windows)](https://github.com/)
[![Language](https://img.shields.io/badge/language-C%23%20%2F%20.NET%204.8-239120?logo=csharp)](https://github.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-emerald.svg)](LICENSE)

A fast, lightweight, native Windows System Tray keyboard application for typing Bengali (`bn-BD` Unicode) using the **Unijoy** (`.avrolayout`) layout — with **zero external runtime dependencies**.

---

## ✨ Features

- **Universal System-Wide Bengali Typing (`bn-BD` Unicode)**:
  - Works across all Windows applications (Browsers, File Explorer, Microsoft Word, Notepad, VS Code, messaging apps, etc.).
  - **Universal Language Persistence**: Switching between **`EN`** and **`বাং`** applies universally across the entire operating system — switching windows (`Alt + Tab`, `Win + E`, etc.) never resets or desynchronizes your active language.
  - **Strict Unicode Order**: Type the consonant (ব্যঞ্জনবর্ণ) first, followed by the vowel sign (কার) or Hasanta (`্`) + consonant for conjuncts (যুক্তবর্ণ).
  - **Smart Independent Vowels**: Press `G` (`্`) followed by any vowel kar (`া`, `ি`, `ী`, `ু`, `ূ`, `ৃ`, `ে`, `ৈ`, `ো`, `ৌ`) **or** hold `Right Alt` / `Ctrl + Alt` (`AltGr`) to type independent vowels (`আ`, `ই`, `ঈ`, `উ`, `ঊ`, `ঋ`, `এ`, `ঐ`, `ও`, `ঔ`).
- **Minimal System Tray Interface (`EN` / `বাং`)**:
  - Runs quietly in the Windows System Tray with no intrusive floating top-bar.
  - Displays simple, high-contrast text icons: **`EN`** for English and **`বাং`** for Bengali (automatically adapts to dark and light Windows taskbars).
- **`Ctrl + Space` Global Toggle**:
  - Press **`Ctrl + Space`** anywhere (or single left-click the **`EN` / `বাং`** tray icon) to toggle between English and Bengali.
  - Uses both a Win32 Raw Input sink (`WM_INPUT` / `RIDEV_INPUTSINK`) and low-level keyboard hook (`WH_KEYBOARD_LL`) so `Ctrl + Space` works reliably and stays synchronized with the tray icon.
- **Built-in Windows Installer & Startup Persistence**:
  - Includes **`UnijoyKeyboardSetup.exe`** to install to `%LOCALAPPDATA%\Programs\UnijoyKeyboard`, configure **Start with Windows** (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`), create Start Menu & Desktop shortcuts, and register in Windows **Installed Apps** (`Settings -> Apps -> Installed Apps`).
- **Built-in Layout Viewer & Custom `.avrolayout` Support**:
  - View the Normal/Shift and AltGr visual keyboard diagrams embedded in `Unijoy.avrolayout`, or search the interactive key-mapping reference table from the system tray menu.

---

## 🚀 Installation & Usage

### Option 1: Using the Setup Installer (Recommended)
1. Run **`UnijoyKeyboardSetup.exe`** (or build from source using `.\build.ps1`).
2. Click **Install Now**.
   - Installs to `%LOCALAPPDATA%\Programs\UnijoyKeyboard`
   - Registers **Start with Windows** so it persists across shutdowns and restarts
   - Launches **`UnijoyKeyboard.exe`** in the Windows System Tray

### Option 2: Silent Command-Line Install / Uninstall
```powershell
# Silent install
.\UnijoyKeyboardSetup.exe --install-silent

# Interactive uninstall
.\UnijoyKeyboard.exe --uninstall
```

### Keyboard Shortcuts & Tray Controls
| Action | Shortcut / Control |
| :--- | :--- |
| Toggle **English (`EN`)** $\leftrightarrow$ **Bengali (`বাং`)** | **`Ctrl + Space`** or **Left-Click** the System Tray icon |
| Type Conjuncts (যুক্তবর্ণ) | Consonant + **`G` (`্`)** + Consonant (e.g. `J` + `G` + `J` = `ক্ক`) |
| Type Independent Vowels (স্বরবর্ণ) | **`G` (`্`)** + Vowel Kar (e.g. `G` + `F` = `আ`, `G` + `D` = `ই`) **or** `Right Alt` / `Ctrl+Alt` + Key |
| Type রেফ (`র্`), র-ফলা (`্র`), য-ফলা (`্য`) | Consonant + **`Shift+A` (`র্`)** + Consonant; Consonant + **`Z` (`্র`)**; Consonant + **`Shift+Z` (`্য`)** |
| Open Menu / Layout Viewer / Settings | **Right-Click** the **`EN` / `বাং`** System Tray icon |

---

## 🛠️ Building from Source

No third-party SDKs or NuGet packages are required. The project compiles directly using the `.NET Framework 4.x` C# compiler (`csc.exe`) pre-installed on every Windows 10/11 machine, or via MSBuild / Visual Studio (`UnijoyKeyboard.csproj`).

```powershell
# Build UnijoyKeyboard.exe and UnijoyKeyboardSetup.exe
powershell -ExecutionPolicy Bypass -File .\build.ps1

# Run automated self-tests
.\UnijoyKeyboard.exe --self-test | Out-Host
```

---

## 📁 Project Structure

| File / Path | Description |
| :--- | :--- |
| `Unijoy.avrolayout` | Unijoy keyboard layout definition (embedded into the executable at build time) |
| `src/Program.cs` | Application entry point, single-instance guard, CLI flags, and self-test runner |
| `src/KeyboardHookEngine.cs` | `WH_KEYBOARD_LL` hook, `WM_INPUT` Raw Input sink, `EVENT_SYSTEM_FOREGROUND` hook refresh, and Unicode `SendInput` engine |
| `src/AvroLayoutParser.cs` | Parser for `.avrolayout` XML key mappings and Base64 layout images |
| `src/TrayApplicationContext.cs` | System Tray (`NotifyIcon`) lifecycle, context menu, and mode synchronization |
| `src/TrayIconGenerator.cs` | Dynamic high-DPI renderer for the `EN` and `বাং` simple text tray icons |
| `src/LayoutViewerForm.cs` | Visual layout viewer and searchable key-mapping reference table |
| `src/SettingsForm.cs` | Startup preferences, typing options, and live test typing box |
| `src/InstallerManager.cs` | Windows installation, startup registry (`Run`), shortcuts, and `Uninstall` registration |
| `src/InstallerForm.cs` | Setup GUI dialog (`UnijoyKeyboardSetup.exe`) |
| `build.ps1` | Zero-dependency PowerShell build script |
| `UnijoyKeyboard.csproj` | Standard MSBuild / Visual Studio project file |
| `.github/workflows/build.yml` | GitHub Actions workflow for automated builds, self-tests, and release binaries |

---

## 📄 License & Credits

- Application source code is licensed under the [MIT License](LICENSE).
- The `Unijoy.avrolayout` keyboard layout format and file are designed by [OmicronLab](https://www.omicronlab.com/).

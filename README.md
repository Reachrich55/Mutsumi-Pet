<h1 align="center">Mutsumi Pet</h1>
<p align="center">
  <img src="assets/droste.png" alt="Mutsumi Pet" width="520">
</p>
<p align="center">
  English
  · <a href="README.zh-CN.md">简体中文</a>
</p>

Mutsumi Pet is a WPF desktop pet application. It lightly senses computer usage through Win32 APIs and calls a large language model to generate speech-bubble lines.

> Current version: v1.3

---

## Feature Overview

### 1. Desktop Pet Display

- Drag the pet with the left mouse button
- Use the right-click menu to refresh dialogue, pause interactions, or exit
- Supports dialogue interactions

### 2. Computer Usage Awareness

- `WindowsUsageMonitor` uses Win32 APIs to read the foreground window and idle state
- The app currently senses:
  - Foreground process name
  - Foreground window title
  - Application type
  - Idle seconds
  - Continuous time spent in the current window
  - Recent usage event
  - Windows session lock state

### 3. LLM Input Panel And Commands

- `LlmClient` calls an OpenAI-compatible API to generate speech-bubble lines
- The floating input panel supports normal chat and commands that start with `/`
- Supported commands include `/focus`, `/rest`, `/summary`, `/setting`, and `/help`
- Normal chat includes current runtime memory; memory is cleared after the app exits
- Prompts distinguish between daily interaction, message reminders, normal chat, focus summaries, and daily summaries

---

## Quick Start

Requirements:

- Windows 10/11
- .NET 8 SDK
- An OpenAI-compatible chat-completions API

### Download MutsumiPet.exe

If you only want to use the app locally, downloading the prebuilt Windows package from GitHub Releases is recommended. This does not require cloning the source code or installing the .NET 8 SDK.

1. Open the latest [GitHub Releases](https://github.com/Reachrich55/Mutsumi-Pet/releases/latest) page for this repository
2. Download `MutsumiPet-*-win-x64.zip` from the latest release
3. Extract it to a local directory, for example:

```text
D:\Apps\MutsumiPet
```

4. Find `.env.example` in the extracted directory, copy it, and rename the copy to `.env`
5. Edit `.env` and fill in your LLM configuration:

```text
LLM_API_KEY="YOUR_API_KEY"
LLM_BASE_URL="YOUR_BASE_URL"
LLM_MODEL="YOUR_MODEL_NAME"
LLM_TIMEOUT_SECONDS="60"
```

6. Double-click `MutsumiPet.exe`

`.env` must be placed in the same directory as `MutsumiPet.exe`. Do not upload your `.env` or API key to public repositories, screenshots, or issues.

If the app only shows local fallback lines after startup, the LLM configuration, network connection, or API permissions may be incorrect. Check that the `.env` filename is correct, the API key is available, and `LLM_BASE_URL` points to an OpenAI-compatible `/v1` chat-completions endpoint.

7. You can type `/setting` in the chat window to open settings and enable or disable local tracking, LLM, message reminders, sending window titles, saving window titles, summary reminders, and usage thresholds.

### Directory Overview

| Path | Description |
| --- | --- |
| `App.xaml` / `App.xaml.cs` | WPF application entry point |
| `MainWindow.xaml` | Desktop pet window, character image, and speech bubble layout |
| `ChatWindow.xaml` | Floating input panel and command completion |
| `MainWindow.xaml.cs` | Window events, timers, dragging, and speech-bubble orchestration |
| `Services/LlmClient.cs` | LLM requests, prompt construction, and response parsing |
| `Services/ChatCommandService.cs` | `/` command parsing |
| `Services/ChatConversationService.cs` | Chat window interaction orchestration |
| `Services/ConversationMemoryService.cs` | Current runtime conversation memory |
| `Services/WindowsUsageMonitor.cs` | Foreground window, idle time, and session state monitoring |
| `Services/UsageSessionStore.cs` | Local SQLite usage-session storage |
| `Services/UsageSessionTracker.cs` | App session merging and active/idle segmentation |
| `Services/FocusSessionService.cs` | Focus and break timers |
| `Services/SettingsService.cs` | Local privacy and behavior settings |
| `Services/ChatAppMessageMonitor.cs` | New-message signal detection for communication apps |
| `Services/SpeechQueueService.cs` | Long-text segmentation and display duration calculation |
| `Services/ImageTransparencyService.cs` | Character image white-background transparency processing |
| `Models/` | Models for usage state, triggers, message signals, and related data |
| `mutsumi.png` | Desktop pet character image |
| `.env.example` | LLM configuration template |

When running from source, `.env` is placed in the project root. This file is ignored by `.gitignore` and should not be committed to the repository.

---

## Configuration And Runtime Data

### LLM Configuration

The app reads configuration from `.env` or environment variables. Environment variables take priority over `.env`.

| Key | Description |
| --- | --- |
| `LLM_API_KEY` | LLM API key |
| `LLM_BASE_URL` | OpenAI-compatible API base URL, usually ending with `/v1` |
| `LLM_MODEL` | Model name |
| `LLM_TIMEOUT_SECONDS` | Request timeout in seconds |

### Runtime Artifacts

The following files and directories are treated as local runtime or build artifacts by default and should not be committed to Git:

- `.env`
- `bin/`
- `obj/`
- `artifacts/`
- `.vs/`
- `TestResults/`
- `*.user`
- `*.suo`
- `*.db`
- `*.db-shm`
- `*.db-wal`
- `*.log`

---

## Privacy Boundary

The app may send the following context to the LLM you configure:

- Foreground process name
- Foreground window title, which can be disabled in settings
- Application type
- Idle time
- Continuous time spent in the current window
- Recent usage event
- Session lock state
- New-message signals from communication apps
- Communication app display name
- Message signal time
- User input in the chat window
- Current runtime conversation memory summary
- Local aggregated summaries: active time, idle time, main apps, and switch count

The app does not collect or send:

- Keyboard input content
- Screenshots
- Chat message bodies from communication apps
- Local databases of communication apps
- Network packet captures
- File contents from the user's computer

---

## Packaging And Release

Publishing a self-contained Windows x64 package is recommended:

```powershell
dotnet publish .\MutsumiPet.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  /p:EnableCompressionInSingleFile=true `
  -o .\artifacts\github-release\MutsumiPet-win-x64
```

---

## Credits

Thanks to Y悠嗖O for drawing the lovely Mutsumi.

---

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

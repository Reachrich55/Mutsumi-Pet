# Multi-Persona Framework Implementation Plan

## Context

Add a multi-persona system to Mutsumi Pet (WPF .NET 8 desktop app). Two built-in personas: `mutsumi` (default) and `mortis`. This phase implements the framework, switching, persistence, and per-persona isolated memory — but no new character images, no massive dialogue writing, and no changes to API config, auto-start, usage monitoring, SQLite, or bubble styling.

## Architecture Overview

```
Personas/*.json  ──load──>  PersonaManager  ──Current──>  LlmClient (systemPrompt, temp, maxTokens)
                                 │
                                 ├──>  ChatConversationService (persona-aware memory)
                                 │       └──>  PersonaConversationMemoryService
                                 │              └──>  Dictionary<string, ConversationMemoryService>
                                 │
                                 └──>  MainWindow (Tab key, context menu, thinking text, SwitchIn/SwitchOut)
```

**Concurrency safety**: `PetInteractionService` records `(personaId, requestGeneration)` at request start; on response, checks `PersonaManager.Current.Id == personaId && generation == latestGeneration`. Stale results are discarded.

## New Files to Create

### 1. `Personas/mutsumi.json`
- All 13 persona properties as JSON
- SystemPrompt = current 7-line `BuildSystemPrompt()` content (placeholder version from spec)
- Temperature=0.76, MaxTokens=600, ThinkingText="小睦正在思考..."
- SwitchInText="……我回来了。", SwitchOutText="" (empty for now, switch handled by manager)

### 2. `Personas/mortis.json`
- Id="mortis", DisplayName="墨提斯", AssistantPrefix="Mortis"
- SystemPrompt = placeholder from spec
- Temperature=0.78, MaxTokens=500
- SwitchInText="终于想起我了？", SwitchOutText=""
- Different trait scores (higher initiative, jealousy; lower clinginess)

### 3. `Models/PersonaProfile.cs`
- All fields with `{ get; init; }` (immutable after deserialization)
- Defaults: Temperature=0.7, MaxTokens=600, MaxSentences=3, UseEmoji=true
- Trait scores default to 0.5

### 4. `Services/PersonaManager.cs`
- Constructor takes `initialPersonaId`, loads all JSONs from `Personas/` dir via `System.Text.Json`
- Fallback: if 0 personas loaded, creates hardcoded "mutsumi" default in-memory
- Invalid initialId → falls back to first available
- `SetCurrent(id)` returns bool, fires `CurrentPersonaChanged` event
- `TryCycleNext(out next)` cycles through all loaded personas
- `Exists(id)`, `AllPersonas`, `Count`, `Current`
- Uses `StringComparer.OrdinalIgnoreCase` for ID dictionary

### 5. `Services/PersonaConversationMemoryService.cs`
- Internal `Dictionary<string, ConversationMemoryService>` with lock
- `RecordExchange(personaId, userText, assistantText)`
- `BuildPromptMemory(personaId, assistantPrefix)` → delegates to per-persona `ConversationMemoryService`
- `ExchangeCount(personaId)`, `Clear(personaId)`

## Files to Modify

### 6. `Services/UserSettings.cs` — Add ActivePersonaId
- New property: `public string ActivePersonaId { get; set; } = "mutsumi";`
- In `CloneNormalized()`: copy it, fallback to "mutsumi" if empty

### 7. `Services/ConversationMemoryService.cs` — Parameterize assistant prefix
- `BuildPromptMemory()` → `BuildPromptMemory(string assistantPrefix)` — replaces hardcoded "小睦"
- Add `Clear()` method

### 8. `Services/LlmClient.cs` — Use persona for system prompt, temp, max_tokens
- `GenerateLineAsync()` and `GenerateChatReplyAsync()` gain `PersonaProfile persona` parameter
- `CreateRequestBody()`/`CreateChatRequestBody()`: `temperature = persona.Temperature`, `max_tokens = persona.MaxTokens`
- `CreateMessages()`/`CreateChatMessages()`: accept `string systemPrompt` instead of calling `BuildSystemPrompt()`
- Remove `BuildSystemPrompt()` static method (no longer called)
- `SelectTemperature()` and `SelectMaxTokens()` become dead code (keep or remove — keep to minimize diff)

### 9. `Services/PetInteractionService.cs` — Persona context + concurrency safety
- Constructor gains `PersonaManager` parameter
- Fields: `_personaManager`, `_requestGeneration` (int, accessed via `Interlocked.Increment`), `_lastLlmLines` (Dictionary<string, string?>)
- `GetChatReplyAsync()` gains `string personaId` param; captures current persona + generation counter; on response checks both; discards stale
- `GenerateOrFallbackAsync()` same pattern
- Per-persona `_lastLlmLines` tracking (`GetLastLlmLine(id)`, `SetLastLlmLine(id, line)`)

### 10. `Services/ChatConversationService.cs` — Persona-aware memory
- Constructor gains `PersonaManager`, `PersonaConversationMemoryService` (replaces `ConversationMemoryService`)
- `SubmitAsync()`: gets current persona; calls `_personaMemoryService.BuildPromptMemory(persona.Id, persona.AssistantPrefix)`; passes `persona.Id` to `GetChatReplyAsync`; records exchange to `_personaMemoryService`

### 11. `MainWindow.xaml` — Context menu items
- Add "切换人格" MenuItem with sub-items "睦子米" (IsCheckable, IsChecked=true, Tag="mutsumi") and "墨提斯" (IsCheckable, Tag="mortis")
- Insert between pause and exit items
- Add `PreviewKeyDown="MainWindow_PreviewKeyDown"` to Window element

### 12. `MainWindow.xaml.cs` — Tab key, switch logic, wiring
- New fields: `_personaManager`, `_personaMemoryService`
- Constructor: instantiate PersonaManager with `_settingsService.Current.ActivePersonaId`; wire into PetInteractionService and ChatConversationService; subscribe to `CurrentPersonaChanged`
- `MainWindow_PreviewKeyDown`: Tab cycles only when IsActive && no child window visible && focused element is not TextBoxBase
- `PersonaMenuItem_Click`: reads Tag, calls `_personaManager.SetCurrent`, calls `ApplyPersonaSwitch`
- `ApplyPersonaSwitch(old, new)`: saves ActivePersonaId to SettingsService; clears speech queue; enqueues SwitchOut+SwitchIn text; updates menu checkmarks; calls `_chatWindow?.UpdatePersona(displayName)`
- `ShowThinkingBubble()`: uses `_personaManager.Current.ThinkingText`
- Cleanup event subscription in `Window_Closing`

### 13. `ChatWindow.xaml` — Name placeholder TextBlock
- Add `x:Name="PlaceholderTextBlock"` to the "和小睦说点什么吧" TextBlock

### 14. `ChatWindow.xaml.cs` — UpdatePersona method
- `UpdatePersona(string displayName)`: sets `Title = $"{displayName} Input"` and `PlaceholderTextBlock.Text = $"和{displayName}说点什么吧"`

### 15. `SettingsWindow.xaml.cs` — Preserve ActivePersonaId on save
- In `SaveButton_Click`, add `ActivePersonaId = _original.ActivePersonaId` to the new UserSettings before CloneNormalized()

### 16. `MutsumiPet.csproj` — Content items
- Add Personas/mutsumi.json and Personas/mortis.json as Content with CopyToOutputDirectory=PreserveNewest

## Test Files

### 17. Update `ConversationMemoryServiceTests.cs`
- Pass "小睦" to all `BuildPromptMemory()` calls
- Add test for custom prefix

### 18. Update `UserSettingsTests.cs`
- Add: default ActivePersonaId is "mutsumi"
- Add: CloneNormalized preserves ActivePersonaId
- Add: empty ActivePersonaId falls back to "mutsumi"

### 19. New `PersonaProfileTests.cs`
- Deserialize full mutsumi.json → all properties correct
- Deserialize partial JSON → defaults applied

### 20. New `PersonaManagerTests.cs`
- Valid initialId sets current
- Invalid initialId falls back
- SetCurrent valid/invalid
- SetCurrent fires event
- TryCycleNext cycles all
- Exists/AllPersonas

### 21. New `PersonaConversationMemoryServiceTests.cs`
- Isolated per-persona memory
- ExchangeCount per persona
- Clear removes memory
- Empty persona returns empty string

## Key Design Decisions

1. **PersonaManager is NOT a static singleton** — instantiated in MainWindow, passed to services. This keeps testability and avoids global state.
2. **LlmClient does NOT reference PersonaManager** — it receives PersonaProfile as a parameter. This keeps LlmClient testable without persona loading.
3. **Generation counter in PetInteractionService** — `Interlocked.Increment` ensures even concurrent LLM responses are correctly rejected after persona switch.
4. **Per-persona _lastLlmLine** — prevents one persona's last line from leaking into another persona's "前一条回复" context.
5. **No new settings file** — ActivePersonaId goes into existing settings.json via SettingsService.
6. **Persona JSON files ship as Content** — same pattern as mutsumi.png.

## Verification

```powershell
dotnet build -c Release
dotnet test
dotnet publish MutsumiPet.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o artifacts/personal-release/MutsumiPet
```

Manual checks:
- Right-click → 切换人格 shows both options with checkmark on current
- Tab key cycles mutsumi↔mortis when main window focused
- Tab key does NOT cycle when ChatWindow is open
- SwitchIn text appears on persona change
- Chat memory is isolated (switch to mortis, chat, switch back to mutsumi → old mutsumi memory restored)
- Old LLM responses discarded after quick switch
- ActivePersonaId persists across restart
- App doesn't crash if persona JSON files are deleted

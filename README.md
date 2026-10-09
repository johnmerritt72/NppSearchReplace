# NppSearchReplace

A Notepad++ plugin that docks a modeless search-and-replace panel at the bottom of the window,
in the spirit of EditPad Pro's search panel: multiline search and replacement boxes, Notepad++'s
own regex engine, and optional live highlighting of every match in the active document.

## Requirements

- Notepad++ 8.5.6 or later, 64-bit
- .NET Framework 4.8 (included in Windows 10 May 2019 Update and later)

## Install

1. Build (below) or download `NppSearchReplace.dll` and `NppSearchReplace.Core.dll`.
2. Create `C:\Program Files\Notepad++\plugins\NppSearchReplace\` and copy both DLLs into it
   (this needs an elevated prompt).
3. Restart Notepad++. The panel is under **Plugins > NppSearchReplace > Show Search & Replace Panel** (Ctrl+Alt+F).

## Using the panel

| Toggle | Meaning |
|---|---|
| Regex | Regular expression search, same syntax as Notepad++'s Find dialog (Boost) |
| Dot | `.` also matches newline (regex only) |
| Case | Match case |
| Words | Whole word only (ignored when Regex is on) |
| Loop | Wrap around at the end or start of the document |
| Highlight | Highlight every match in the active document while you type (capped at 20,000) |

| Key (inside either box) | Action |
|---|---|
| Enter | New line in the box |
| Ctrl+Enter | Find Next |
| Ctrl+Shift+Enter | Find Previous |
| Ctrl+R | Replace selection if it matches, then Find Next |
| Ctrl+Shift+R | Replace All (one undo step) |
| Ctrl+Shift+C | Count matches |
| Tab | Switch between the search and replacement boxes |
| Escape | Return focus to the editor |

The small ▾ button beside each box lists the last 20 entries. Toggles, highlight state, splitter
position, panel visibility and history are remembered between sessions in
`%AppData%\Notepad++\plugins\config\NppSearchReplace\`.

## Build

Requires Visual Studio 2022 (MSBuild) and the .NET SDK.

```powershell
# plugin (x64 only)
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" src\NppSearchReplace\NppSearchReplace.csproj /t:Restore
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" src\NppSearchReplace\NppSearchReplace.csproj /p:Configuration=Release /p:Platform=x64

# unit tests for the search logic
dotnet test tests\NppSearchReplace.Core.Tests
```

Output lands in `src\NppSearchReplace\bin\Release-x64\`. The build also tries to copy the DLLs into
`%ProgramFiles%\Notepad++\plugins\NppSearchReplace\`; that step is skipped with a warning when the
folder is not writable, so copy by hand or run the build from an elevated prompt. Granting your
account Modify rights on that one folder makes every later build install automatically.

## Layout

- `src/NppSearchReplace.Core` — search engine, match enumeration, highlighter, history. No Notepad++ dependency; fully unit tested.
- `src/NppSearchReplace` — the plugin: Notepad++ entry points, docked WinForms panel, Scintilla adapter, settings. Based on [NppCSharpPluginPack](https://github.com/molsonkiko/NppCSharpPluginPack).
- `tests/NppSearchReplace.Core.Tests` — MSTest suite with an in-memory editor fake.
- `docs/manual-test.md` — checklist for behaviour that needs a live Notepad++.
- `docs/superpowers/` — the design spec and implementation plan this was built from.

## Not in this version

Multi-file search, search in selection, bookmarks, and EditPad's Free-spacing/Adapt/Line/Invert modes.

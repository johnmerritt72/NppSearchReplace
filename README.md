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
   From the editor, Ctrl+Alt+F always shows the panel and loads the current single-line selection into the
   search box; pressing it while the panel already has focus hides the panel.

## Using the panel

| Toggle | Meaning |
|---|---|
| Regex | Regular expression search, same syntax as Notepad++'s Find dialog (Boost) |
| Dot | `.` also matches newline (dimmed and ignored while Regex is off) |
| Case | Match case |
| Words | Whole word only (dimmed and ignored while Regex is on) |
| Loop | Loop around to the start (or end) of the document when the search reaches the end (or start) |
| Highlight | Highlight every match in the active document while you type (capped at 20,000) |

Buttons: Find First, Find Next, Find Prev, Find Last, Replace, Replace All, Count. Find First and
Find Last jump to the first or last match in the document regardless of the caret or Loop.

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

The search and replacement boxes use the editor's Global default font from Style Configurator.
The small ▾ button at the top-right of each box lists the last 20 entries. Toggles, highlight state, splitter
position, panel visibility and history are remembered between sessions in
`%AppData%\Notepad++\plugins\config\NppSearchReplace\`.

## Build

Requires Visual Studio 2022 (MSBuild) and the .NET SDK.

```powershell
# whole solution (plugin is x64 only; MSBuild.exe is under your Visual Studio install, or on PATH in a Developer PowerShell)
msbuild NppSearchReplace.sln /t:Restore
msbuild NppSearchReplace.sln /p:Configuration=Release /p:Platform=x64

# unit tests for the search logic
dotnet test tests\NppSearchReplace.Core.Tests
```

Opening `NppSearchReplace.sln` in Visual Studio 2022 and choosing the x64 platform works too.

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

## Licence and credits

NppSearchReplace is released under the [MIT License](LICENSE).

The Notepad++ plugin infrastructure in `src/NppSearchReplace` (the `PluginInfrastructure`, `JSON_Tools`
and `Utils` folders, `FormBase` and `NppFormHelper`, and the project file) is derived from
[NppCSharpPluginPack](https://github.com/molsonkiko/NppCSharpPluginPack) by Mark J. Olson, which is in turn
based on [NotepadPlusPlusPluginPack.Net](https://github.com/kbilsted/NotepadPlusPlusPluginPack.Net) by
Kasper B. Graversen. Those portions remain under the Apache License, Version 2.0; the full licence text and
copyright notices are in [THIRD-PARTY-LICENSES.md](THIRD-PARTY-LICENSES.md). Thank you to both authors,
without whom writing a Notepad++ plugin in C# would be a much larger job.

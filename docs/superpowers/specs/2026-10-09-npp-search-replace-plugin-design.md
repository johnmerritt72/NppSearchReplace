# Notepad++ Search & Replace Panel Plugin — Design

Date: 2026-10-09
Status: approved in conversation, awaiting spec review

## Goal

A Notepad++ plugin that reproduces the feel of EditPad Pro's search-and-replace
panel: a modeless panel docked at the bottom of the Notepad++ window, with
multiline search and replacement boxes by default, regex support that matches
Notepad++'s own Find dialog, and an optional live highlight of every match in the
active document.

## Target environment

- Notepad++ 8.5.6 or later, 64-bit (the user runs 8.9.8). 8.5.6 is the first
  version with `NPPM_ALLOCATEINDICATOR`.
- Windows 10/11 with .NET Framework 4.8.
- Built with Visual Studio 2022, x64 configuration only.

## Decisions already made

| Decision | Choice | Why |
|---|---|---|
| Language / toolchain | C#, .NET Framework 4.8, WinForms, based on the NppCSharpPluginPack template | Fastest UI iteration; the user's other projects are .NET; JsonTools proves the template works on this machine |
| Regex engine | Scintilla search (`SCI_SEARCHINTARGET`) with `SCFIND_REGEXP \| SCFIND_POSIX` | Notepad++ swaps in Boost.Regex, so the flavor is identical to the built-in Find dialog; positions are UTF-8 byte offsets, which indicators and selection need |
| Enter key in text boxes | Enter inserts a newline; Ctrl+Enter = Find Next | The boxes are multiline first, like EditPad Pro |
| Highlighting | Live, debounced, whole document, capped | Mirrors EditPad; cap keeps huge files responsive |
| Persistence | INI for toggles, XML for history | Template already provides INI settings; history needs ordered multiline strings |

## Features (v1)

Options, each a toggle button on the top row:

- **Regex** — regular expression search. Off means literal text.
- **Dot** — "." matches newline (Notepad++ flag `SCFIND_REGEXP_DOTMATCHESNEWLINE`, `0x10000000`). Only meaningful when Regex is on; button is disabled otherwise.
- **Case** — match case.
- **Words** — whole word only.
- **Loop** — wrap around at end/start of document.
- **Highlight** — live highlight of all matches in the active document.

Actions, each a button on the second row with a keyboard shortcut that works
while focus is in either text box:

| Action | Shortcut | Behavior |
|---|---|---|
| Find Next | Ctrl+Enter | Search forward from the end of the current selection; select and scroll to the match |
| Find Previous | Ctrl+Shift+Enter | Search backward from the start of the current selection |
| Replace | Ctrl+R | If the current selection matches the pattern, replace it; then Find Next |
| Replace All | Ctrl+Shift+R | Replace every match in the document inside one undo action; report count |
| Count | Ctrl+Shift+C | Count matches without moving the caret; report in status |
| Focus editor | Escape | Return keyboard focus to the Scintilla editor |

Other UI:

- Two monospace multiline text boxes side by side in a `SplitContainer`:
  search on the left, replacement on the right. Word wrap off, both scrollbars.
  Splitter position is persisted.
- A small drop-down button beside each box opens a menu of the last 20 entries
  for that box (most recent first, de-duplicated, each shown as a single line
  truncated to about 60 characters with newlines rendered as a return symbol).
  Choosing one loads it into the box.
- A status label at the bottom showing the last result: "Match found",
  "No more matches", "Wrapped", "N matches", "Replaced N", regex error text in
  red, or "Highlight capped at 20,000 matches".
- Plugin menu entries: "Show Search Panel" (default shortcut Ctrl+Alt+F; shows
  the panel if hidden and focuses the search box, pre-filling it with the
  current editor selection if the selection is non-empty and single-line) and
  "About".
- Tab moves focus between the search box and the replacement box.

Out of scope for v1: multi-file / project / closed-file search, search in
selection, bookmarking or marking lines, EditPad's Free-spacing, Adapt, Line
and Invert modes, dark mode styling beyond what the template gives for free.

## Architecture

```
NppSearchReplace.sln
├── NppSearchReplace/                (class library, x64, net48)
│   ├── PluginInfrastructure/        (copied from NppCSharpPluginPack: gateways, Win32, docking structs)
│   ├── Main.cs                      (plugin entry: menu commands, notifications, lifetime)
│   ├── Forms/
│   │   ├── SearchPanel.cs           (docked WinForms Form; UI only)
│   │   └── SearchPanel.Designer.cs
│   ├── Search/
│   │   ├── SearchOptions.cs         (immutable record of the six toggles -> Scintilla flags)
│   │   ├── SearchEngine.cs          (find next/prev, replace, replace all, count)
│   │   ├── Highlighter.cs           (owns the indicator; apply/clear; tracks touched buffers)
│   │   └── MatchEnumerator.cs       (iterates SCI_SEARCHINTARGET over a range, handles empty matches, cap)
│   ├── Persistence/
│   │   ├── Settings.cs              (template INI settings: toggles, highlight on/off, splitter %)
│   │   └── History.cs               (ordered, de-duplicated list of recent texts; XML round-trip)
│   └── Properties/AssemblyInfo.cs
└── NppSearchReplace.Tests/          (MSTest, net48; pure logic only, fake gateway)
```

### Component responsibilities

**Main** — the only file that knows about plugin registration. Builds the
menu, creates the panel lazily on first show, registers it with
`NPPM_DMMREGASDCKDLG` (`DWS_DF_CONT_BOTTOM`, panel title "Search & Replace"),
forwards `NPPN_BUFFERACTIVATED`, `NPPN_FILECLOSED`, `SCN_MODIFIED` and
`NPPN_SHUTDOWN` to the panel, and loads/saves settings and history.

**SearchPanel** — WinForms `Form` registered as modeless with
`NPPM_MODELESSDIALOG` so Ctrl+C/X work and key routing is sane. Holds a
`SearchOptions` built from the toggle buttons, calls `SearchEngine` for
actions, and asks `Highlighter` to refresh on a 300 ms `Timer` after any
change to the search text or options while Highlight is on. Never sends
Scintilla messages directly.

**SearchOptions** — pure data. `ToScintillaFlags()` produces the int for
`SCI_SETSEARCHFLAGS`: `SCFIND_MATCHCASE`, `SCFIND_WHOLEWORD`,
`SCFIND_REGEXP | SCFIND_POSIX` when Regex, plus
`SCFIND_REGEXP_DOTMATCHESNEWLINE` when Regex and Dot.

**SearchEngine** — takes an `IScintillaGateway`. Each operation:

- `FindNext(text, options)` — target = [selEnd, docEnd]; on miss and Loop,
  target = [0, selEnd]; returns a `SearchResult { Found, Start, End, Wrapped, Error }`.
  A regex compile error is detected by `SCI_SEARCHINTARGET` returning -1 with
  `SCI_GETSTATUS` set; the engine reads the message, clears the status, and
  returns it in `Error`.
- `FindPrevious` — same but backward: target = [selStart, 0] (Scintilla searches
  backward when target start > end), then wrap target = [docEnd, selStart].
- `Replace` — if the current selection equals the current match range (re-run
  the search anchored at selStart and confirm it returns exactly the selection),
  set target to the selection and call `SCI_REPLACETARGETRE` (regex) or
  `SCI_REPLACETARGET` (literal); then `FindNext`.
- `ReplaceAll` — `SCI_BEGINUNDOACTION`, enumerate matches from 0 forward, replace
  each, advancing the search start by the replacement length (and by one extra
  character after a zero-length match), `SCI_ENDUNDOACTION`; returns the count.
- `Count` — enumerate without modifying; returns the count.

**MatchEnumerator** — the one place that knows the empty-match rule and the
cap. Yields `(start, end)` pairs. Stops when the cap (20,000) is hit and
reports `Capped = true`.

**Highlighter** — on first use calls `NPPM_ALLOCATEINDICATOR(1)` and styles the
returned indicator (`INDIC_ROUNDBOX`, alpha 70, outline alpha 140, an orange
that reads on both light and dark themes). `Apply(text, options)` clears the
indicator over the whole document, then fills each range from
`MatchEnumerator`. `Clear()` clears the whole document. Only the active buffer
is ever painted. The highlighter records the buffer IDs it has painted; on
`NPPN_BUFFERACTIVATED` it repaints if Highlight is on, or clears the buffer if
it is in the painted set and Highlight is now off. Repaints on `SCN_MODIFIED`
with insert/delete flags, debounced by the same timer.

**History** — `Push(text)` moves an existing equal entry to the front or
inserts at the front, trims to 20. Ignores empty text. `Load/Save` via
`XmlSerializer` to `<config>/NppSearchReplace/history.xml` with two lists.

**Settings** — the template's attribute-driven INI settings class, with:
Regex, DotMatchesNewline, MatchCase, WholeWord, WrapAround, HighlightMatches
(bools), SplitterPercent (int, default 50), HighlightCap (int, default 20000).

## Data flow

1. User types in the search box: panel timer restarts; on tick, if Highlight
   is on, `Highlighter.Apply(text, options)` runs and the status shows the match count.
2. User presses Ctrl+Enter: panel calls `SearchEngine.FindNext`; the engine
   selects and scrolls (`SCI_SETSEL`, `SCI_SCROLLCARET`); the panel updates the
   status label and calls `History.Push(text)`.
3. Buffer switch: Main forwards `NPPN_BUFFERACTIVATED`; the panel asks
   `Highlighter` to repaint or clear as described above.
4. Shutdown: Main saves settings and history.

## Error handling

- Invalid regex: status label in red with Boost's message; no dialog; highlight
  cleared.
- Empty search text: actions do nothing; status "Enter a search text".
- Cap hit: highlight what was found; status warns.
- All Scintilla calls are wrapped so an unexpected exception shows once in the
  status label and is logged to `<config>/NppSearchReplace/error.log` rather
  than crashing Notepad++.

## Testing

Automated (NppSearchReplace.Tests, runs from VS Test Explorer or `vstest.console`):

- `SearchOptions.ToScintillaFlags()` for every combination of the three
  regex-related toggles and the two literal ones.
- `History`: push ordering, de-dup, trim to 20, empty ignored, XML round-trip.
- `MatchEnumerator` against a fake `IScintillaGateway` that answers
  `SearchInTarget` from a scripted list: forward iteration, zero-length match
  advancement, cap behaviour.
- `SearchEngine.ReplaceAll` against the same fake: counts and offset
  adjustment after variable-length replacements.

Manual checklist (documented in `docs/manual-test.md`):

- Panel docks at bottom, survives restart with same visibility and splitter.
- Enter adds newline; Ctrl+Enter/Ctrl+Shift+Enter navigate; Escape returns to editor.
- Multiline literal search across CRLF lines.
- Regex with groups and `$1` / `\1` in replacement.
- Dot toggle changes whether `a.b` matches across a newline.
- Loop off stops at end with "No more matches"; Loop on reports "Wrapped".
- Highlight updates while typing, clears on toggle off and on panel hide,
  follows buffer switches, and survives editing the document.
- Replace All on a large file completes and undoes in one Ctrl+Z.
- Invalid regex shows the error in red, no crash.

## Build & install

- `msbuild NppSearchReplace.sln /p:Configuration=Release /p:Platform=x64`
- A post-build step copies `NppSearchReplace.dll` to
  `%ProgramFiles%\Notepad++\plugins\NppSearchReplace\` when that folder is
  writable; otherwise the README explains copying by hand.

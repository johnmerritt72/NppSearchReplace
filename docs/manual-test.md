# Manual test checklist

Run after any change to the plugin project. Use a new document containing exactly:

```
apple banana apple
Banana cherry
café apple
```

## Panel and keys
- [ ] Ctrl+Alt+F opens the panel docked at the bottom; the search box has focus; Ctrl+Alt+F again (focus still in the panel) hides it.
- [ ] With the panel open, click into the editor, select a word, press Ctrl+Alt+F: the panel stays open and the word is loaded into the search box. A multi-line selection is not loaded.
- [ ] Settings > Preferences > Dark Mode on: the status text in the panel stays readable (not black on dark).
- [ ] Enter inserts a newline in either box; Ctrl+Enter / Ctrl+Shift+Enter navigate; Tab switches boxes; Escape focuses the editor.
- [ ] Ctrl+C / Ctrl+V / Ctrl+A work inside the boxes.
- [ ] The boxes use the editor's Global default font (Style Configurator); changing it there updates the boxes without a restart.
- [ ] Buttons and toggles are flat with a thin border; a checked toggle shows a lighter grey (dark theme) or slightly darker grey (light theme) background, no blue border.
- [ ] All buttons and toggles sit on one row with a gap between the two groups; narrowing the panel wraps the row instead of clipping it.
- [ ] The history ▾ is a small button at the top-right corner of each box, not a full-height bar.

## Searching
- [ ] `apple`, Wrap on: three Ctrl+Enter presses select each occurrence, the fourth reports `Wrapped (line 1)`.
- [ ] Wrap off at the last match: `No more matches`.
- [ ] Find First selects the first `apple` (line 1) from anywhere; Find Last selects the last `apple` (line 3) from anywhere, both with Wrap off.
- [ ] Case off finds `banana` and `Banana`; Case on finds only `banana`.
- [ ] Words on: `app` finds nothing; `apple` finds three.
- [ ] `café apple` (non-ASCII) is found with the selection exactly covering it; `apple` on line 3 is selected without a one-byte shift.
- [ ] Multiline literal `apple⏎Banana` matches across the line break in both Windows and Unix EOL documents.
- [ ] Regex `(ap)(ple)` with replacement `$2$1`: Replace gives `pleap`; Replace All reports `Replaced N`; one Ctrl+Z undoes all.
- [ ] Regex `(` shows red `Invalid regular expression`, no message box.
- [ ] Dot text is dimmed while Regex is off and Words is dimmed while Regex is on; both still show a tooltip saying so. Regex `a.b` with Dot on matches across a newline.
- [ ] Count reports `3 matches` and leaves the selection alone.
- [ ] Replace with a selection that is not a match does not change text and just finds next.

## Highlighting
- [ ] Highlight on: matches get orange boxes within ~0.3 s of typing; status shows the count.
- [ ] Invalid regex clears the boxes and shows the red error.
- [ ] Switching tabs repaints the active tab; tabs painted earlier are cleared when Highlight is off.
- [ ] Editing the document updates the highlight after typing stops.
- [ ] Highlight off, panel hidden, or panel closed with X: boxes vanish.
- [ ] 100,000-line file: `Highlight capped at 20000 matches`, editor remains responsive.

## Persistence
- [ ] Toggles, Highlight, splitter position, panel visibility and both histories survive a Notepad++ restart.
- [ ] History dropdown lists most-recent-first, multiline entries shown with ↵.
- [ ] Closing the panel with X keeps it closed on the next start.

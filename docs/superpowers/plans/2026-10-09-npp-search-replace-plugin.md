# Notepad++ Search & Replace Panel Plugin — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Notepad++ plugin that docks a modeless, multiline search-and-replace panel at the bottom of the window, with Notepad++-flavoured regex and optional live highlighting of all matches.

**Architecture:** Pure search logic lives in an SDK-style class library (`NppSearchReplace.Core`) that talks to the editor only through a small `IEditorTarget` interface, so it is unit-tested with a fake. The plugin assembly (`NppSearchReplace`, cloned from the NppCSharpPluginPack template) supplies the Scintilla-backed `IEditorTarget`, the docked WinForms panel, settings and history persistence, and the Notepad++ entry points.

**Tech Stack:** C# 7.3, .NET Framework 4.8, WinForms, NppCSharpPluginPack infrastructure (`Kbg.NppPluginNET.PluginInfrastructure`), UnmanagedExports.Repack.Upgrade for the C exports, MSTest for Core tests. Build: Visual Studio 2022 `MSBuild.exe` for the plugin, `dotnet test` for Core.

**Spec:** `docs/superpowers/specs/2026-10-09-npp-search-replace-plugin-design.md`

## Global Constraints

- Target framework for every project: `net48` / `v4.8`. Plugin assembly builds for `x64` only.
- Minimum Notepad++: 8.5.6 (first version with `NPPM_ALLOCATEINDICATOR`). Test machine runs 8.9.8 64-bit.
- Plugin name, assembly name, output folder name, and config folder name are all exactly `NppSearchReplace` (Notepad++ loads `plugins\NppSearchReplace\NppSearchReplace.dll`).
- Regex engine is Scintilla's `SCI_SEARCHINTARGET` with `SCFIND_REGEXP | SCFIND_POSIX`; "dot matches newline" is the Notepad++-only flag `0x10000000`.
- Highlight cap default: 20,000 matches. History length: 20 entries per box. Highlight debounce: 300 ms.
- Key bindings inside the panel: Enter = newline, Ctrl+Enter = Find Next, Ctrl+Shift+Enter = Find Previous, Ctrl+R = Replace, Ctrl+Shift+R = Replace All, Ctrl+Shift+C = Count, Escape = focus editor, Tab = other box. Menu shortcut Ctrl+Alt+F shows the panel.
- Status messages (exact text): `Match found (line N)`, `Wrapped (line N)`, `No more matches`, `N matches`, `Replaced N`, `Enter a search text`, `Invalid regular expression`, `Highlight capped at N matches`, `Document too large (over 2 GB)`. After a successful single Replace the next-match message is prefixed with `Replaced; `. Line numbers are 1-based in the UI.
- Never show a MessageBox from a search action; errors go to the status label and `error.log`.
- Template files under `src/NppSearchReplace/PluginInfrastructure/` are copied verbatim except where a task says otherwise.
- Build tools: `"C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"` and `dotnet` (SDK 10.0.401 installed; .NET Framework 4.8 reference assemblies present).
- Commit after every task with the attribution line `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.

## Review Focus

1. **Document over 2 GB** (`SCI_GETLENGTH` > `int.MaxValue`): every engine entry point returns `TooLarge` and the panel shows `Document too large (over 2 GB)` instead of overflowing. Pinned in Task 5 (`FindNext_DocumentOverIntMax_ReportsTooLarge`).
2. **Newlines typed in the boxes vs. document EOL mode**: a pattern typed with Ctrl+Enter contains `\r\n`; in an LF-only document a literal search must still match, so pattern and replacement newlines are normalised to the document's EOL string. Pinned in Task 5 (`FindNext_NormalisesPatternNewlinesToDocumentEol`) and Task 6 (`ReplaceAll_NormalisesReplacementNewlines`).
3. **Replace when the selection no longer equals the match** (user moved the caret or edited): must not replace, only find next. Pinned in Task 6 (`Replace_SelectionNotAMatch_DoesNotReplaceButFindsNext`).
4. **Invalid regex while Highlight is on**: stale highlights from the previous valid pattern must be cleared and the error reported. Pinned in Task 7 (`Apply_InvalidRegex_ClearsPreviousHighlightAndReportsError`).
5. **Non-ASCII search text in a UTF-8 document** (`é`, `€`, CJK): byte offsets differ from char offsets, so the adapter must pass UTF-8 byte counts and never index document text by char. Cannot be unit tested (needs Scintilla); pinned as manual test step 5 in Task 10 and in `docs/manual-test.md` (Task 13).

---

## File Structure

```
NppSearchReplace.sln
.gitignore
README.md
docs/manual-test.md
src/NppSearchReplace.Core/NppSearchReplace.Core.csproj        SDK-style net48 class library, AnyCPU
src/NppSearchReplace.Core/SearchOptions.cs                     toggles -> Scintilla flags
src/NppSearchReplace.Core/IEditorTarget.cs                     the only editor abstraction Core knows
src/NppSearchReplace.Core/TextSpan.cs                          (Start, End) struct
src/NppSearchReplace.Core/MatchEnumerator.cs                   enumerate matches with empty-match rule + cap
src/NppSearchReplace.Core/SearchEngine.cs                      FindNext/Previous/Replace/ReplaceAll/Count + result types
src/NppSearchReplace.Core/Highlighter.cs                       indicator application and touched-buffer tracking
src/NppSearchReplace.Core/SearchHistory.cs                     recent texts, de-dup, trim, XML round-trip
tests/NppSearchReplace.Core.Tests/NppSearchReplace.Core.Tests.csproj   MSTest, SDK-style net48
tests/NppSearchReplace.Core.Tests/FakeEditorTarget.cs          in-memory IEditorTarget
tests/NppSearchReplace.Core.Tests/SearchOptionsTests.cs
tests/NppSearchReplace.Core.Tests/SearchHistoryTests.cs
tests/NppSearchReplace.Core.Tests/MatchEnumeratorTests.cs
tests/NppSearchReplace.Core.Tests/SearchEngineFindTests.cs
tests/NppSearchReplace.Core.Tests/SearchEngineReplaceTests.cs
tests/NppSearchReplace.Core.Tests/HighlighterTests.cs
src/NppSearchReplace/NppSearchReplace.csproj                   old-style csproj from template, x64 only
src/NppSearchReplace/Main.cs                                   plugin entry: menu, notifications, docking registration
src/NppSearchReplace/PluginInfrastructure/*                    copied from template (one small edit to none)
src/NppSearchReplace/Forms/FormBase.cs(.Designer.cs)           from template, two small edits (virtual hooks)
src/NppSearchReplace/Forms/NppFormHelper.cs                    from template verbatim
src/NppSearchReplace/Forms/MultilineTextBox.cs                 TextBox that keeps Enter/Tab/Escape inside a docked form
src/NppSearchReplace/Forms/SearchPanel.cs                      the docked panel (layout built in code, no .resx)
src/NppSearchReplace/Search/ScintillaEditorTarget.cs           IEditorTarget over ScintillaGateway
src/NppSearchReplace/Utils/Settings.cs                         INI-backed settings (template SettingsBase)
src/NppSearchReplace/Utils/Npp.cs, FormStyle.cs, Translator.cs, ArrayExtensions.cs   from template
src/NppSearchReplace/JSON_Tools/*                              from template (Translator depends on it)
src/NppSearchReplace/Properties/AssemblyInfo.cs
```

Template source for copying is cloned to the scratchpad at
`C:\Users\jmerritt\AppData\Local\Temp\claude\c--Code-NotepadPPSearchReplace\da2f00a3-22bc-4c6e-a2e7-64dd5f107a38\scratchpad\template\NppCSharpPluginPack` (commit 8f8d0e0, 2026-05-21). If that folder is gone, re-clone with `git clone --depth 1 https://github.com/molsonkiko/NppCSharpPluginPack.git` into a fresh scratchpad folder.

---

### Task 1: Solution scaffold with Core library and test project

**Files:**
- Create: `NppSearchReplace.sln`
- Create: `src/NppSearchReplace.Core/NppSearchReplace.Core.csproj`
- Create: `tests/NppSearchReplace.Core.Tests/NppSearchReplace.Core.Tests.csproj`
- Create: `tests/NppSearchReplace.Core.Tests/SmokeTests.cs`
- Modify: `.gitignore`

**Interfaces:**
- Produces: project names `NppSearchReplace.Core` (namespace `NppSearchReplace.Core`) and `NppSearchReplace.Core.Tests` (namespace `NppSearchReplace.Core.Tests`); the command `dotnet test tests/NppSearchReplace.Core.Tests` used by every later task.

- [ ] **Step 1: Create the Core project file**

`src/NppSearchReplace.Core/NppSearchReplace.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>7.3</LangVersion>
    <RootNamespace>NppSearchReplace.Core</RootNamespace>
    <AssemblyName>NppSearchReplace.Core</AssemblyName>
    <Nullable>disable</Nullable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create the test project file**

`tests/NppSearchReplace.Core.Tests/NppSearchReplace.Core.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>7.3</LangVersion>
    <IsPackable>false</IsPackable>
    <RootNamespace>NppSearchReplace.Core.Tests</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="MSTest.TestAdapter" Version="3.6.1" />
    <PackageReference Include="MSTest.TestFramework" Version="3.6.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\NppSearchReplace.Core\NppSearchReplace.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Write a smoke test that proves the harness runs**

`tests/NppSearchReplace.Core.Tests/SmokeTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NppSearchReplace.Core.Tests
{
    [TestClass]
    public class SmokeTests
    {
        [TestMethod]
        public void TestHarness_Runs()
        {
            Assert.AreEqual(4, 2 + 2);
        }
    }
}
```

- [ ] **Step 4: Create the solution and add both projects**

Run from the repo root:

```powershell
dotnet new sln -n NppSearchReplace
dotnet sln NppSearchReplace.sln add src\NppSearchReplace.Core\NppSearchReplace.Core.csproj
dotnet sln NppSearchReplace.sln add tests\NppSearchReplace.Core.Tests\NppSearchReplace.Core.Tests.csproj
```

- [ ] **Step 5: Extend .gitignore**

Replace `.gitignore` content with:

```
bin/
obj/
.vs/
*.user
*.suo
TestResults/
packages/
src/NppSearchReplace/packages/
```

- [ ] **Step 6: Run the smoke test**

Run: `dotnet test tests\NppSearchReplace.Core.Tests`
Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 7: Commit**

```powershell
git add -A
git commit -m "Scaffold solution with Core library and MSTest project

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: SearchOptions and Scintilla flag mapping

**Files:**
- Create: `src/NppSearchReplace.Core/SearchOptions.cs`
- Test: `tests/NppSearchReplace.Core.Tests/SearchOptionsTests.cs`

**Interfaces:**
- Produces: `public sealed class SearchOptions { bool UseRegex; bool DotMatchesNewline; bool MatchCase; bool WholeWord; bool WrapAround; int ToScintillaFlags(); }` plus public constants `ScfindWholeWord = 0x2`, `ScfindMatchCase = 0x4`, `ScfindRegexp = 0x00200000`, `ScfindPosix = 0x00400000`, `ScfindRegexpDotMatchesNewline = 0x10000000`.

- [ ] **Step 1: Write the failing tests**

`tests/NppSearchReplace.Core.Tests/SearchOptionsTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NppSearchReplace.Core.Tests
{
    [TestClass]
    public class SearchOptionsTests
    {
        [TestMethod]
        public void Defaults_AreLiteralCaseInsensitiveWrapping()
        {
            var o = new SearchOptions();
            Assert.IsFalse(o.UseRegex);
            Assert.IsFalse(o.DotMatchesNewline);
            Assert.IsFalse(o.MatchCase);
            Assert.IsFalse(o.WholeWord);
            Assert.IsTrue(o.WrapAround);
            Assert.AreEqual(0, o.ToScintillaFlags());
        }

        [TestMethod]
        public void MatchCase_SetsScfindMatchCase()
        {
            var o = new SearchOptions { MatchCase = true };
            Assert.AreEqual(SearchOptions.ScfindMatchCase, o.ToScintillaFlags());
        }

        [TestMethod]
        public void WholeWord_WithoutRegex_SetsScfindWholeWord()
        {
            var o = new SearchOptions { WholeWord = true };
            Assert.AreEqual(SearchOptions.ScfindWholeWord, o.ToScintillaFlags());
        }

        [TestMethod]
        public void WholeWord_WithRegex_IsIgnored()
        {
            var o = new SearchOptions { WholeWord = true, UseRegex = true };
            Assert.AreEqual(SearchOptions.ScfindRegexp | SearchOptions.ScfindPosix, o.ToScintillaFlags());
        }

        [TestMethod]
        public void Regex_SetsRegexpAndPosix()
        {
            var o = new SearchOptions { UseRegex = true };
            Assert.AreEqual(SearchOptions.ScfindRegexp | SearchOptions.ScfindPosix, o.ToScintillaFlags());
        }

        [TestMethod]
        public void DotMatchesNewline_WithRegex_SetsNotepadPlusPlusFlag()
        {
            var o = new SearchOptions { UseRegex = true, DotMatchesNewline = true };
            int expected = SearchOptions.ScfindRegexp | SearchOptions.ScfindPosix | SearchOptions.ScfindRegexpDotMatchesNewline;
            Assert.AreEqual(expected, o.ToScintillaFlags());
        }

        [TestMethod]
        public void DotMatchesNewline_WithoutRegex_IsIgnored()
        {
            var o = new SearchOptions { DotMatchesNewline = true };
            Assert.AreEqual(0, o.ToScintillaFlags());
        }

        [TestMethod]
        public void AllOn_CombinesEverythingExceptWholeWord()
        {
            var o = new SearchOptions { UseRegex = true, DotMatchesNewline = true, MatchCase = true, WholeWord = true, WrapAround = true };
            int expected = SearchOptions.ScfindMatchCase | SearchOptions.ScfindRegexp | SearchOptions.ScfindPosix | SearchOptions.ScfindRegexpDotMatchesNewline;
            Assert.AreEqual(expected, o.ToScintillaFlags());
        }

        [TestMethod]
        public void Clone_CopiesEveryToggle()
        {
            var o = new SearchOptions { UseRegex = true, DotMatchesNewline = true, MatchCase = true, WholeWord = true, WrapAround = false };
            var c = o.Clone();
            Assert.AreNotSame(o, c);
            Assert.IsTrue(c.UseRegex); Assert.IsTrue(c.DotMatchesNewline); Assert.IsTrue(c.MatchCase); Assert.IsTrue(c.WholeWord); Assert.IsFalse(c.WrapAround);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests\NppSearchReplace.Core.Tests`
Expected: build error `The type or namespace name 'SearchOptions' could not be found`.

- [ ] **Step 3: Implement SearchOptions**

`src/NppSearchReplace.Core/SearchOptions.cs`:

```csharp
namespace NppSearchReplace.Core
{
    /// <summary>
    /// The user's search toggles. WholeWord is ignored while UseRegex is on (Notepad++ does the same),
    /// and DotMatchesNewline only means something while UseRegex is on.
    /// </summary>
    public sealed class SearchOptions
    {
        public const int ScfindWholeWord = 0x2;
        public const int ScfindMatchCase = 0x4;
        public const int ScfindRegexp = 0x00200000;
        public const int ScfindPosix = 0x00400000;
        /// <summary>Notepad++-specific extension to Scintilla's search flags.</summary>
        public const int ScfindRegexpDotMatchesNewline = 0x10000000;

        public bool UseRegex { get; set; }
        public bool DotMatchesNewline { get; set; }
        public bool MatchCase { get; set; }
        public bool WholeWord { get; set; }
        public bool WrapAround { get; set; } = true;

        public int ToScintillaFlags()
        {
            int flags = 0;
            if (MatchCase)
                flags |= ScfindMatchCase;
            if (UseRegex)
            {
                flags |= ScfindRegexp | ScfindPosix;
                if (DotMatchesNewline)
                    flags |= ScfindRegexpDotMatchesNewline;
            }
            else if (WholeWord)
            {
                flags |= ScfindWholeWord;
            }
            return flags;
        }

        public SearchOptions Clone()
        {
            return new SearchOptions
            {
                UseRegex = UseRegex,
                DotMatchesNewline = DotMatchesNewline,
                MatchCase = MatchCase,
                WholeWord = WholeWord,
                WrapAround = WrapAround,
            };
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests\NppSearchReplace.Core.Tests`
Expected: `Passed! - Failed: 0, Passed: 10`

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "Add SearchOptions with Scintilla flag mapping

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: SearchHistory with XML persistence

**Files:**
- Create: `src/NppSearchReplace.Core/SearchHistory.cs`
- Test: `tests/NppSearchReplace.Core.Tests/SearchHistoryTests.cs`

**Interfaces:**
- Produces: `public sealed class SearchHistory { const int MaxEntries = 20; List<string> SearchTexts; List<string> ReplaceTexts; void PushSearch(string); void PushReplace(string); void Save(string path); static SearchHistory Load(string path); static string ToDisplayLine(string text, int maxLength); }`

- [ ] **Step 1: Write the failing tests**

`tests/NppSearchReplace.Core.Tests/SearchHistoryTests.cs`:

```csharp
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NppSearchReplace.Core.Tests
{
    [TestClass]
    public class SearchHistoryTests
    {
        [TestMethod]
        public void PushSearch_InsertsMostRecentFirst()
        {
            var h = new SearchHistory();
            h.PushSearch("one");
            h.PushSearch("two");
            CollectionAssert.AreEqual(new[] { "two", "one" }, h.SearchTexts);
        }

        [TestMethod]
        public void PushSearch_ExistingEntry_MovesToFrontWithoutDuplicate()
        {
            var h = new SearchHistory();
            h.PushSearch("one");
            h.PushSearch("two");
            h.PushSearch("one");
            CollectionAssert.AreEqual(new[] { "one", "two" }, h.SearchTexts);
        }

        [TestMethod]
        public void PushSearch_IsCaseSensitiveAndWhitespaceSensitive()
        {
            var h = new SearchHistory();
            h.PushSearch("One");
            h.PushSearch("one");
            h.PushSearch("one ");
            Assert.AreEqual(3, h.SearchTexts.Count);
        }

        [TestMethod]
        public void PushSearch_EmptyOrNull_IsIgnored()
        {
            var h = new SearchHistory();
            h.PushSearch("");
            h.PushSearch(null);
            Assert.AreEqual(0, h.SearchTexts.Count);
        }

        [TestMethod]
        public void PushSearch_TrimsToMaxEntries()
        {
            var h = new SearchHistory();
            for (int i = 0; i < SearchHistory.MaxEntries + 5; i++)
                h.PushSearch("entry" + i);
            Assert.AreEqual(SearchHistory.MaxEntries, h.SearchTexts.Count);
            Assert.AreEqual("entry" + (SearchHistory.MaxEntries + 4), h.SearchTexts[0]);
        }

        [TestMethod]
        public void PushReplace_IsIndependentOfSearch()
        {
            var h = new SearchHistory();
            h.PushSearch("s");
            h.PushReplace("r");
            CollectionAssert.AreEqual(new[] { "s" }, h.SearchTexts);
            CollectionAssert.AreEqual(new[] { "r" }, h.ReplaceTexts);
        }

        [TestMethod]
        public void SaveAndLoad_RoundTripsMultilineTextAndOrder()
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xml");
            try
            {
                var h = new SearchHistory();
                h.PushSearch("line1\r\nline2");
                h.PushSearch("  leading and trailing  ");
                h.PushReplace("<xml & stuff>");
                h.Save(path);

                var loaded = SearchHistory.Load(path);
                CollectionAssert.AreEqual(new[] { "  leading and trailing  ", "line1\r\nline2" }, loaded.SearchTexts);
                CollectionAssert.AreEqual(new[] { "<xml & stuff>" }, loaded.ReplaceTexts);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Load_MissingFile_ReturnsEmptyHistory()
        {
            var loaded = SearchHistory.Load(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Path.GetRandomFileName()));
            Assert.AreEqual(0, loaded.SearchTexts.Count);
            Assert.AreEqual(0, loaded.ReplaceTexts.Count);
        }

        [TestMethod]
        public void Load_CorruptFile_ReturnsEmptyHistory()
        {
            string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xml");
            try
            {
                File.WriteAllText(path, "this is not xml");
                var loaded = SearchHistory.Load(path);
                Assert.AreEqual(0, loaded.SearchTexts.Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Save_CreatesMissingDirectory()
        {
            string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string path = Path.Combine(dir, "history.xml");
            try
            {
                var h = new SearchHistory();
                h.PushSearch("x");
                h.Save(path);
                Assert.IsTrue(File.Exists(path));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void ToDisplayLine_ReplacesNewlinesAndTruncates()
        {
            Assert.AreEqual("a\u21B5b", SearchHistory.ToDisplayLine("a\r\nb", 60));
            Assert.AreEqual("a\u21B5b", SearchHistory.ToDisplayLine("a\nb", 60));
            Assert.AreEqual("abcde\u2026", SearchHistory.ToDisplayLine("abcdefghij", 5));
            Assert.AreEqual("abcde", SearchHistory.ToDisplayLine("abcde", 5));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests\NppSearchReplace.Core.Tests`
Expected: build error `The type or namespace name 'SearchHistory' could not be found`.

- [ ] **Step 3: Implement SearchHistory**

`src/NppSearchReplace.Core/SearchHistory.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace NppSearchReplace.Core
{
    /// <summary>
    /// Most-recent-first lists of search and replacement texts. Serialised with XmlSerializer, so the
    /// public lists must stay public and settable.
    /// </summary>
    [XmlRoot("SearchHistory")]
    public sealed class SearchHistory
    {
        public const int MaxEntries = 20;

        [XmlArray("SearchTexts"), XmlArrayItem("Text")]
        public List<string> SearchTexts { get; set; } = new List<string>();

        [XmlArray("ReplaceTexts"), XmlArrayItem("Text")]
        public List<string> ReplaceTexts { get; set; } = new List<string>();

        public void PushSearch(string text) => Push(SearchTexts, text);

        public void PushReplace(string text) => Push(ReplaceTexts, text);

        private static void Push(List<string> list, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            list.Remove(text);
            list.Insert(0, text);
            if (list.Count > MaxEntries)
                list.RemoveRange(MaxEntries, list.Count - MaxEntries);
        }

        public void Save(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var serializer = new XmlSerializer(typeof(SearchHistory));
            // Entitize keeps "\r" as &#xD; so CRLF entries survive the XML parser's newline normalisation.
            var settings = new XmlWriterSettings { Indent = true, NewLineHandling = NewLineHandling.Entitize };
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (XmlWriter writer = XmlWriter.Create(stream, settings))
                serializer.Serialize(writer, this);
        }

        public static SearchHistory Load(string path)
        {
            if (!File.Exists(path))
                return new SearchHistory();
            try
            {
                var serializer = new XmlSerializer(typeof(SearchHistory));
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    var loaded = serializer.Deserialize(stream) as SearchHistory;
                    if (loaded == null)
                        return new SearchHistory();
                    loaded.SearchTexts = loaded.SearchTexts ?? new List<string>();
                    loaded.ReplaceTexts = loaded.ReplaceTexts ?? new List<string>();
                    return loaded;
                }
            }
            catch (Exception)
            {
                return new SearchHistory();
            }
        }

        /// <summary>Single-line preview for menus: newlines become U+21B5, long text is cut with U+2026.</summary>
        public static string ToDisplayLine(string text, int maxLength)
        {
            string oneLine = (text ?? "").Replace("\r\n", "\u21B5").Replace("\n", "\u21B5").Replace("\r", "\u21B5");
            if (oneLine.Length <= maxLength)
                return oneLine;
            return oneLine.Substring(0, maxLength) + "\u2026";
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests\NppSearchReplace.Core.Tests`
Expected: `Passed! - Failed: 0, Passed: 21`

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "Add SearchHistory with XML persistence

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: IEditorTarget, FakeEditorTarget, and MatchEnumerator

**Files:**
- Create: `src/NppSearchReplace.Core/IEditorTarget.cs`
- Create: `src/NppSearchReplace.Core/TextSpan.cs`
- Create: `src/NppSearchReplace.Core/MatchEnumerator.cs`
- Create: `tests/NppSearchReplace.Core.Tests/FakeEditorTarget.cs`
- Test: `tests/NppSearchReplace.Core.Tests/MatchEnumeratorTests.cs`

**Interfaces:**
- Produces: `IEditorTarget` (below, exact), `struct TextSpan { int Start; int End; int Length }`, `static class MatchEnumerator { static MatchList Enumerate(IEditorTarget target, string pattern, SearchOptions options, int cap); }`, `class MatchList { List<TextSpan> Matches; bool Capped; bool RegexError; bool TooLarge; }`, and the test helper `FakeEditorTarget`.
- Fake conventions used by later tests: literal search (ordinal, case-insensitive unless `ScfindMatchCase`), pattern `"^"` is an empty match at the search start, any pattern listed in `InvalidPatterns` sets the regex error flag, `LengthOverride` simulates huge documents, `EolString` defaults to `"\n"`.

- [ ] **Step 1: Write IEditorTarget and TextSpan**

`src/NppSearchReplace.Core/IEditorTarget.cs`:

```csharp
using System;

namespace NppSearchReplace.Core
{
    /// <summary>
    /// Everything Core needs from the editor. All positions are Scintilla byte positions.
    /// The plugin implements this over ScintillaGateway; tests implement it in memory.
    /// </summary>
    public interface IEditorTarget
    {
        long Length { get; }
        int SelectionStart { get; }
        int SelectionEnd { get; }
        /// <summary>Start of the last successful search match or replacement.</summary>
        int TargetStart { get; }
        /// <summary>End of the last successful search match or replacement.</summary>
        int TargetEnd { get; }
        /// <summary>"\r\n", "\r" or "\n" depending on the document's EOL mode.</summary>
        string EolString { get; }
        IntPtr CurrentBufferId { get; }

        void SetSearchFlags(int scintillaFlags);
        /// <summary>
        /// Search for pattern in [start, end). If start &gt; end the search runs backwards over [end, start).
        /// Returns the match start, or -1. On success TargetStart/TargetEnd describe the match.
        /// </summary>
        int SearchInRange(int start, int end, string pattern);
        /// <summary>Replace the current target. Returns the byte length of the inserted text and updates TargetEnd.</summary>
        int ReplaceTarget(string replacement, bool useRegexGroups);
        /// <summary>True if the most recent search failed because the regex did not compile. Reading clears the flag.</summary>
        bool TakeRegexError();
        int PositionAfter(int position);
        int LineFromPosition(int position);
        void SetSelection(int anchor, int caret);
        void ScrollCaretIntoView();
        void BeginUndoAction();
        void EndUndoAction();

        /// <summary>Returns an indicator number owned by this plugin, or -1 if none could be allocated.</summary>
        int AllocateIndicator();
        void StyleIndicator(int indicator);
        void FillIndicator(int indicator, int start, int length);
        void ClearIndicator(int indicator, int start, int length);
    }
}
```

`src/NppSearchReplace.Core/TextSpan.cs`:

```csharp
namespace NppSearchReplace.Core
{
    public struct TextSpan
    {
        public int Start;
        public int End;

        public TextSpan(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Length => End - Start;

        public override string ToString() => "[" + Start + "," + End + ")";
    }
}
```

- [ ] **Step 2: Write the fake editor**

`tests/NppSearchReplace.Core.Tests/FakeEditorTarget.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using NppSearchReplace.Core;

namespace NppSearchReplace.Core.Tests
{
    /// <summary>
    /// In-memory IEditorTarget. One char == one byte, so tests use ASCII text.
    /// Literal search only, except the pattern "^" which is an empty match at the search start.
    /// </summary>
    public sealed class FakeEditorTarget : IEditorTarget
    {
        public StringBuilder Text;
        public long? LengthOverride;
        public HashSet<string> InvalidPatterns = new HashSet<string>();
        public List<Tuple<int, int, int>> Fills = new List<Tuple<int, int, int>>();   // indicator, start, length
        public List<Tuple<int, int, int>> Clears = new List<Tuple<int, int, int>>();  // indicator, start, length
        public List<Tuple<int, int>> Selections = new List<Tuple<int, int>>();        // anchor, caret
        public int IndicatorToAllocate = 9;
        public int AllocateCalls;
        public int StyleCalls;
        public int ScrollCalls;
        public int UndoDepth;
        public int UndoActionsCompleted;
        public int Flags;
        private bool _regexError;

        public FakeEditorTarget(string text)
        {
            Text = new StringBuilder(text);
        }

        public long Length => LengthOverride ?? Text.Length;
        public int SelectionStart { get; set; }
        public int SelectionEnd { get; set; }
        public int TargetStart { get; private set; }
        public int TargetEnd { get; private set; }
        public string EolString { get; set; } = "\n";
        public IntPtr CurrentBufferId { get; set; } = new IntPtr(1);

        public void SetSearchFlags(int scintillaFlags) => Flags = scintillaFlags;

        public int SearchInRange(int start, int end, string pattern)
        {
            if (InvalidPatterns.Contains(pattern))
            {
                _regexError = true;
                return -1;
            }
            string s = Text.ToString();
            int idx;
            if (start <= end)
            {
                int hi = Math.Min(end, s.Length);
                if (start > hi)
                    return -1;
                if (pattern == "^")
                    idx = start;
                else
                    idx = s.IndexOf(pattern, start, hi - start, Comparison());
            }
            else
            {
                int lo = end;
                int hi = Math.Min(start, s.Length);
                if (pattern == "^")
                    idx = hi;
                else
                {
                    string slice = s.Substring(lo, hi - lo);
                    int rel = slice.LastIndexOf(pattern, Comparison());
                    idx = rel < 0 ? -1 : lo + rel;
                }
            }
            if (idx < 0)
                return -1;
            TargetStart = idx;
            TargetEnd = idx + (pattern == "^" ? 0 : pattern.Length);
            return idx;
        }

        private StringComparison Comparison()
        {
            return (Flags & SearchOptions.ScfindMatchCase) != 0 ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        }

        public int ReplaceTarget(string replacement, bool useRegexGroups)
        {
            Text.Remove(TargetStart, TargetEnd - TargetStart);
            Text.Insert(TargetStart, replacement);
            TargetEnd = TargetStart + replacement.Length;
            return replacement.Length;
        }

        public bool TakeRegexError()
        {
            bool r = _regexError;
            _regexError = false;
            return r;
        }

        public int PositionAfter(int position) => Math.Min(position + 1, Text.Length);

        public int LineFromPosition(int position)
        {
            int line = 0;
            int limit = Math.Min(position, Text.Length);
            for (int i = 0; i < limit; i++)
                if (Text[i] == '\n') line++;
            return line;
        }

        public void SetSelection(int anchor, int caret)
        {
            SelectionStart = Math.Min(anchor, caret);
            SelectionEnd = Math.Max(anchor, caret);
            Selections.Add(Tuple.Create(anchor, caret));
        }

        public void ScrollCaretIntoView() => ScrollCalls++;
        public void BeginUndoAction() => UndoDepth++;
        public void EndUndoAction() { UndoDepth--; UndoActionsCompleted++; }

        public int AllocateIndicator() { AllocateCalls++; return IndicatorToAllocate; }
        public void StyleIndicator(int indicator) => StyleCalls++;
        public void FillIndicator(int indicator, int start, int length) => Fills.Add(Tuple.Create(indicator, start, length));
        public void ClearIndicator(int indicator, int start, int length) => Clears.Add(Tuple.Create(indicator, start, length));
    }
}
```

- [ ] **Step 3: Write the failing enumerator tests**

`tests/NppSearchReplace.Core.Tests/MatchEnumeratorTests.cs`:

```csharp
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NppSearchReplace.Core.Tests
{
    [TestClass]
    public class MatchEnumeratorTests
    {
        private static readonly SearchOptions Literal = new SearchOptions();

        [TestMethod]
        public void Enumerate_FindsEveryNonOverlappingMatchInOrder()
        {
            var t = new FakeEditorTarget("ab ab ab");
            var r = MatchEnumerator.Enumerate(t, "ab", Literal, 100);
            CollectionAssert.AreEqual(new[] { "[0,2)", "[3,5)", "[6,8)" }, r.Matches.Select(m => m.ToString()).ToArray());
            Assert.IsFalse(r.Capped);
            Assert.IsFalse(r.RegexError);
        }

        [TestMethod]
        public void Enumerate_OverlappingCandidates_ResumeAtMatchEnd()
        {
            var t = new FakeEditorTarget("aaaa");
            var r = MatchEnumerator.Enumerate(t, "aa", Literal, 100);
            Assert.AreEqual(2, r.Matches.Count);
        }

        [TestMethod]
        public void Enumerate_NoMatch_ReturnsEmpty()
        {
            var t = new FakeEditorTarget("hello");
            var r = MatchEnumerator.Enumerate(t, "zzz", Literal, 100);
            Assert.AreEqual(0, r.Matches.Count);
        }

        [TestMethod]
        public void Enumerate_EmptyPattern_ReturnsEmptyWithoutSearching()
        {
            var t = new FakeEditorTarget("hello");
            var r = MatchEnumerator.Enumerate(t, "", Literal, 100);
            Assert.AreEqual(0, r.Matches.Count);
            Assert.AreEqual(0, t.Flags);
        }

        [TestMethod]
        public void Enumerate_EmptyMatches_AdvanceOnePositionAndTerminate()
        {
            var t = new FakeEditorTarget("abc");
            var r = MatchEnumerator.Enumerate(t, "^", Literal, 100);
            // empty match at 0,1,2,3 then PositionAfter(3)==3 stops the loop
            Assert.AreEqual(4, r.Matches.Count);
            Assert.IsTrue(r.Matches.All(m => m.Length == 0));
        }

        [TestMethod]
        public void Enumerate_StopsAtCapAndFlagsIt()
        {
            var t = new FakeEditorTarget(new string('a', 50));
            var r = MatchEnumerator.Enumerate(t, "a", Literal, 10);
            Assert.AreEqual(10, r.Matches.Count);
            Assert.IsTrue(r.Capped);
        }

        [TestMethod]
        public void Enumerate_InvalidRegex_ReportsErrorAndNoMatches()
        {
            var t = new FakeEditorTarget("abc");
            t.InvalidPatterns.Add("(");
            var r = MatchEnumerator.Enumerate(t, "(", new SearchOptions { UseRegex = true }, 100);
            Assert.AreEqual(0, r.Matches.Count);
            Assert.IsTrue(r.RegexError);
            Assert.IsFalse(t.TakeRegexError(), "error flag must be consumed");
        }

        [TestMethod]
        public void Enumerate_SetsSearchFlagsFromOptions()
        {
            var t = new FakeEditorTarget("Ab ab");
            var r = MatchEnumerator.Enumerate(t, "ab", new SearchOptions { MatchCase = true }, 100);
            Assert.AreEqual(SearchOptions.ScfindMatchCase, t.Flags);
            Assert.AreEqual(1, r.Matches.Count);
        }

        [TestMethod]
        public void Enumerate_DocumentOverIntMax_ReportsTooLarge()
        {
            var t = new FakeEditorTarget("abc") { LengthOverride = (long)int.MaxValue + 1 };
            var r = MatchEnumerator.Enumerate(t, "a", Literal, 100);
            Assert.IsTrue(r.TooLarge);
            Assert.AreEqual(0, r.Matches.Count);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test tests\NppSearchReplace.Core.Tests`
Expected: build error `The type or namespace name 'MatchEnumerator' could not be found`.

- [ ] **Step 5: Implement MatchEnumerator**

`src/NppSearchReplace.Core/MatchEnumerator.cs`:

```csharp
using System.Collections.Generic;

namespace NppSearchReplace.Core
{
    public sealed class MatchList
    {
        public List<TextSpan> Matches { get; } = new List<TextSpan>();
        public bool Capped { get; set; }
        public bool RegexError { get; set; }
        public bool TooLarge { get; set; }
    }

    /// <summary>
    /// Walks every match from the start of the document. This is the one place that knows the
    /// empty-match rule (advance one character) and the cap.
    /// </summary>
    public static class MatchEnumerator
    {
        public static MatchList Enumerate(IEditorTarget target, string pattern, SearchOptions options, int cap)
        {
            var result = new MatchList();
            if (string.IsNullOrEmpty(pattern))
                return result;
            if (target.Length > int.MaxValue)
            {
                result.TooLarge = true;
                return result;
            }
            int length = (int)target.Length;
            target.SetSearchFlags(options.ToScintillaFlags());
            int pos = 0;
            while (pos <= length)
            {
                int found = target.SearchInRange(pos, length, pattern);
                if (found < 0)
                {
                    result.RegexError = target.TakeRegexError();
                    break;
                }
                int end = target.TargetEnd;
                result.Matches.Add(new TextSpan(found, end));
                if (result.Matches.Count >= cap)
                {
                    result.Capped = true;
                    break;
                }
                if (end > found)
                {
                    pos = end;
                }
                else
                {
                    int next = target.PositionAfter(end);
                    if (next <= end)
                        break;
                    pos = next;
                }
            }
            return result;
        }
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests\NppSearchReplace.Core.Tests`
Expected: `Passed! - Failed: 0, Passed: 30`

- [ ] **Step 7: Commit**

```powershell
git add -A
git commit -m "Add IEditorTarget, in-memory fake, and MatchEnumerator

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: SearchEngine FindNext and FindPrevious

**Files:**
- Create: `src/NppSearchReplace.Core/SearchEngine.cs`
- Test: `tests/NppSearchReplace.Core.Tests/SearchEngineFindTests.cs`

**Interfaces:**
- Produces: `public sealed class SearchResult { bool Found; int Start; int End; int Line; bool Wrapped; bool RegexError; bool EmptyPattern; bool TooLarge; }`, `public sealed class SearchEngine { SearchEngine(IEditorTarget target); SearchResult FindNext(string pattern, SearchOptions options); SearchResult FindPrevious(string pattern, SearchOptions options); string NormalizeNewlines(string text); }`. Task 6 adds `Replace`, `ReplaceAll`, `Count` to the same class.

- [ ] **Step 1: Write the failing tests**

`tests/NppSearchReplace.Core.Tests/SearchEngineFindTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NppSearchReplace.Core.Tests
{
    [TestClass]
    public class SearchEngineFindTests
    {
        private static SearchOptions Wrap => new SearchOptions { WrapAround = true };
        private static SearchOptions NoWrap => new SearchOptions { WrapAround = false };

        [TestMethod]
        public void FindNext_SearchesFromSelectionEndAndSelectsMatch()
        {
            var t = new FakeEditorTarget("xx ab xx ab") { SelectionStart = 0, SelectionEnd = 0 };
            var r = new SearchEngine(t).FindNext("ab", Wrap);
            Assert.IsTrue(r.Found);
            Assert.AreEqual(3, r.Start);
            Assert.AreEqual(5, r.End);
            Assert.IsFalse(r.Wrapped);
            Assert.AreEqual(3, t.SelectionStart);
            Assert.AreEqual(5, t.SelectionEnd);
            Assert.AreEqual(1, t.ScrollCalls);
        }

        [TestMethod]
        public void FindNext_SecondCall_FindsNextOccurrence()
        {
            var t = new FakeEditorTarget("ab ab");
            var e = new SearchEngine(t);
            e.FindNext("ab", Wrap);
            var r = e.FindNext("ab", Wrap);
            Assert.AreEqual(3, r.Start);
        }

        [TestMethod]
        public void FindNext_ReportsZeroBasedLine()
        {
            var t = new FakeEditorTarget("a\nb\nab");
            var r = new SearchEngine(t).FindNext("ab", Wrap);
            Assert.AreEqual(2, r.Line);
        }

        [TestMethod]
        public void FindNext_AtEndWithWrap_WrapsToStartAndFlagsIt()
        {
            var t = new FakeEditorTarget("ab xx") { SelectionStart = 3, SelectionEnd = 3 };
            var r = new SearchEngine(t).FindNext("ab", Wrap);
            Assert.IsTrue(r.Found);
            Assert.IsTrue(r.Wrapped);
            Assert.AreEqual(0, r.Start);
        }

        [TestMethod]
        public void FindNext_AtEndWithoutWrap_ReportsNotFoundAndLeavesSelection()
        {
            var t = new FakeEditorTarget("ab xx") { SelectionStart = 3, SelectionEnd = 3 };
            var r = new SearchEngine(t).FindNext("ab", NoWrap);
            Assert.IsFalse(r.Found);
            Assert.IsFalse(r.Wrapped);
            Assert.AreEqual(0, t.Selections.Count);
        }

        [TestMethod]
        public void FindNext_EmptyPattern_ReportsEmptyPattern()
        {
            var t = new FakeEditorTarget("ab");
            var r = new SearchEngine(t).FindNext("", Wrap);
            Assert.IsTrue(r.EmptyPattern);
            Assert.IsFalse(r.Found);
        }

        [TestMethod]
        public void FindNext_InvalidRegex_ReportsRegexErrorWithoutWrapping()
        {
            var t = new FakeEditorTarget("ab") { SelectionStart = 1, SelectionEnd = 1 };
            t.InvalidPatterns.Add("(");
            var r = new SearchEngine(t).FindNext("(", new SearchOptions { UseRegex = true, WrapAround = true });
            Assert.IsTrue(r.RegexError);
            Assert.IsFalse(r.Found);
            Assert.IsFalse(r.Wrapped);
        }

        [TestMethod]
        public void FindNext_EmptyMatchAtCurrentSelection_AdvancesInsteadOfSticking()
        {
            var t = new FakeEditorTarget("abc") { SelectionStart = 1, SelectionEnd = 1 };
            var r = new SearchEngine(t).FindNext("^", new SearchOptions { UseRegex = true });
            Assert.IsTrue(r.Found);
            Assert.AreEqual(2, r.Start);
            Assert.AreEqual(2, r.End);
        }

        [TestMethod]
        public void FindNext_DocumentOverIntMax_ReportsTooLarge()
        {
            var t = new FakeEditorTarget("ab") { LengthOverride = (long)int.MaxValue + 1 };
            var r = new SearchEngine(t).FindNext("ab", Wrap);
            Assert.IsTrue(r.TooLarge);
            Assert.IsFalse(r.Found);
        }

        [TestMethod]
        public void FindNext_NormalisesPatternNewlinesToDocumentEol()
        {
            var t = new FakeEditorTarget("one\ntwo") { EolString = "\n" };
            var r = new SearchEngine(t).FindNext("one\r\ntwo", Wrap);
            Assert.IsTrue(r.Found);
            Assert.AreEqual(0, r.Start);
            Assert.AreEqual(7, r.End);
        }

        [TestMethod]
        public void NormalizeNewlines_ConvertsAllThreeFormsToEol()
        {
            var t = new FakeEditorTarget("") { EolString = "\r\n" };
            var e = new SearchEngine(t);
            Assert.AreEqual("a\r\nb\r\nc\r\nd", e.NormalizeNewlines("a\r\nb\nc\rd"));
        }

        [TestMethod]
        public void FindPrevious_SearchesBackwardFromSelectionStart()
        {
            var t = new FakeEditorTarget("ab xx ab") { SelectionStart = 6, SelectionEnd = 8 };
            var r = new SearchEngine(t).FindPrevious("ab", Wrap);
            Assert.IsTrue(r.Found);
            Assert.AreEqual(0, r.Start);
            Assert.IsFalse(r.Wrapped);
        }

        [TestMethod]
        public void FindPrevious_AtStartWithWrap_WrapsToEnd()
        {
            var t = new FakeEditorTarget("xx ab") { SelectionStart = 0, SelectionEnd = 0 };
            var r = new SearchEngine(t).FindPrevious("ab", Wrap);
            Assert.IsTrue(r.Found);
            Assert.IsTrue(r.Wrapped);
            Assert.AreEqual(3, r.Start);
        }

        [TestMethod]
        public void FindPrevious_AtStartWithoutWrap_ReportsNotFound()
        {
            var t = new FakeEditorTarget("xx ab") { SelectionStart = 0, SelectionEnd = 0 };
            var r = new SearchEngine(t).FindPrevious("ab", NoWrap);
            Assert.IsFalse(r.Found);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests\NppSearchReplace.Core.Tests`
Expected: build error `The type or namespace name 'SearchEngine' could not be found`.

- [ ] **Step 3: Implement SearchEngine find operations**

`src/NppSearchReplace.Core/SearchEngine.cs`:

```csharp
namespace NppSearchReplace.Core
{
    public sealed class SearchResult
    {
        public bool Found { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
        /// <summary>Zero-based line of Start.</summary>
        public int Line { get; set; }
        public bool Wrapped { get; set; }
        public bool RegexError { get; set; }
        public bool EmptyPattern { get; set; }
        public bool TooLarge { get; set; }
    }

    /// <summary>
    /// Find/replace over an IEditorTarget. Stateless apart from the target; the caret and selection
    /// in the editor are the only cursor.
    /// </summary>
    public sealed class SearchEngine
    {
        private readonly IEditorTarget _t;

        public SearchEngine(IEditorTarget target)
        {
            _t = target;
        }

        public SearchResult FindNext(string pattern, SearchOptions options) => Find(pattern, options, forward: true);

        public SearchResult FindPrevious(string pattern, SearchOptions options) => Find(pattern, options, forward: false);

        /// <summary>Newlines typed in the panel become the document's own EOL so literal multiline searches match.</summary>
        public string NormalizeNewlines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? "";
            string eol = _t.EolString;
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", eol);
        }

        private SearchResult Find(string pattern, SearchOptions options, bool forward)
        {
            var r = new SearchResult();
            if (string.IsNullOrEmpty(pattern))
            {
                r.EmptyPattern = true;
                return r;
            }
            if (_t.Length > int.MaxValue)
            {
                r.TooLarge = true;
                return r;
            }
            int length = (int)_t.Length;
            string p = NormalizeNewlines(pattern);
            _t.SetSearchFlags(options.ToScintillaFlags());
            int selStart = _t.SelectionStart;
            int selEnd = _t.SelectionEnd;

            int found = forward ? _t.SearchInRange(selEnd, length, p) : _t.SearchInRange(selStart, 0, p);
            if (found < 0 && _t.TakeRegexError())
            {
                r.RegexError = true;
                return r;
            }
            if (forward && found >= 0 && found == selStart && _t.TargetEnd == selEnd && found == _t.TargetEnd)
            {
                // the match is the empty selection we already have; step past it
                int next = _t.PositionAfter(selEnd);
                found = next > selEnd ? _t.SearchInRange(next, length, p) : -1;
            }
            if (found < 0 && options.WrapAround)
            {
                found = forward ? _t.SearchInRange(0, selEnd, p) : _t.SearchInRange(length, selStart, p);
                if (found >= 0)
                    r.Wrapped = true;
            }
            if (found < 0)
                return r;

            r.Found = true;
            r.Start = found;
            r.End = _t.TargetEnd;
            r.Line = _t.LineFromPosition(found);
            _t.SetSelection(r.Start, r.End);
            _t.ScrollCaretIntoView();
            return r;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests\NppSearchReplace.Core.Tests`
Expected: `Passed! - Failed: 0, Passed: 44`

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "Add SearchEngine FindNext/FindPrevious with wrap and EOL normalisation

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: SearchEngine Replace, ReplaceAll, and Count

**Files:**
- Modify: `src/NppSearchReplace.Core/SearchEngine.cs`
- Test: `tests/NppSearchReplace.Core.Tests/SearchEngineReplaceTests.cs`

**Interfaces:**
- Consumes: `SearchEngine`, `SearchResult`, `MatchEnumerator` from Tasks 4 and 5.
- Produces: `public sealed class ReplaceResult { bool Replaced; SearchResult Next; }`, `public sealed class ReplaceAllResult { int Count; bool RegexError; bool EmptyPattern; bool TooLarge; }`, `public sealed class CountResult { int Count; bool Capped; bool RegexError; bool EmptyPattern; bool TooLarge; }`, methods `ReplaceResult Replace(string pattern, string replacement, SearchOptions options)`, `ReplaceAllResult ReplaceAll(string pattern, string replacement, SearchOptions options)`, `CountResult Count(string pattern, SearchOptions options, int cap)`.

- [ ] **Step 1: Write the failing tests**

`tests/NppSearchReplace.Core.Tests/SearchEngineReplaceTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NppSearchReplace.Core.Tests
{
    [TestClass]
    public class SearchEngineReplaceTests
    {
        private static SearchOptions Wrap => new SearchOptions { WrapAround = true };

        [TestMethod]
        public void Replace_SelectionIsAMatch_ReplacesThenFindsNext()
        {
            var t = new FakeEditorTarget("ab ab") { SelectionStart = 0, SelectionEnd = 2 };
            var r = new SearchEngine(t).Replace("ab", "XYZ", Wrap);
            Assert.IsTrue(r.Replaced);
            Assert.AreEqual("XYZ ab", t.Text.ToString());
            Assert.IsTrue(r.Next.Found);
            Assert.AreEqual(4, r.Next.Start);
        }

        [TestMethod]
        public void Replace_SelectionNotAMatch_DoesNotReplaceButFindsNext()
        {
            var t = new FakeEditorTarget("zz ab") { SelectionStart = 0, SelectionEnd = 2 };
            var r = new SearchEngine(t).Replace("ab", "X", Wrap);
            Assert.IsFalse(r.Replaced);
            Assert.AreEqual("zz ab", t.Text.ToString());
            Assert.IsTrue(r.Next.Found);
            Assert.AreEqual(3, r.Next.Start);
        }

        [TestMethod]
        public void Replace_SelectionLongerThanMatch_DoesNotReplace()
        {
            var t = new FakeEditorTarget("abc ab") { SelectionStart = 0, SelectionEnd = 3 };
            var r = new SearchEngine(t).Replace("ab", "X", Wrap);
            Assert.IsFalse(r.Replaced);
            Assert.AreEqual("abc ab", t.Text.ToString());
        }

        [TestMethod]
        public void Replace_EmptySelection_OnlyFindsNext()
        {
            var t = new FakeEditorTarget("ab") { SelectionStart = 0, SelectionEnd = 0 };
            var r = new SearchEngine(t).Replace("ab", "X", Wrap);
            Assert.IsFalse(r.Replaced);
            Assert.IsTrue(r.Next.Found);
        }

        [TestMethod]
        public void Replace_LastMatchWithoutWrap_ReplacesAndReportsNoMore()
        {
            var t = new FakeEditorTarget("ab") { SelectionStart = 0, SelectionEnd = 2 };
            var r = new SearchEngine(t).Replace("ab", "X", new SearchOptions { WrapAround = false });
            Assert.IsTrue(r.Replaced);
            Assert.AreEqual("X", t.Text.ToString());
            Assert.IsFalse(r.Next.Found);
        }

        [TestMethod]
        public void Replace_PassesRegexFlagToReplaceTarget()
        {
            var t = new FakeEditorTarget("ab") { SelectionStart = 0, SelectionEnd = 2 };
            var r = new SearchEngine(t).Replace("ab", "X", new SearchOptions { UseRegex = true });
            Assert.IsTrue(r.Replaced);
            Assert.AreEqual(SearchOptions.ScfindRegexp | SearchOptions.ScfindPosix, t.Flags);
        }

        [TestMethod]
        public void Replace_EmptyPattern_ReportsEmptyPattern()
        {
            var t = new FakeEditorTarget("ab") { SelectionStart = 0, SelectionEnd = 2 };
            var r = new SearchEngine(t).Replace("", "X", Wrap);
            Assert.IsFalse(r.Replaced);
            Assert.IsTrue(r.Next.EmptyPattern);
        }

        [TestMethod]
        public void ReplaceAll_ReplacesEveryMatchInsideOneUndoAction()
        {
            var t = new FakeEditorTarget("ab-ab-ab");
            var r = new SearchEngine(t).ReplaceAll("ab", "LONGER", Wrap);
            Assert.AreEqual(3, r.Count);
            Assert.AreEqual("LONGER-LONGER-LONGER", t.Text.ToString());
            Assert.AreEqual(1, t.UndoActionsCompleted);
            Assert.AreEqual(0, t.UndoDepth);
        }

        [TestMethod]
        public void ReplaceAll_ShorterReplacement_AdjustsOffsets()
        {
            var t = new FakeEditorTarget("abcabcabc");
            var r = new SearchEngine(t).ReplaceAll("abc", "x", Wrap);
            Assert.AreEqual(3, r.Count);
            Assert.AreEqual("xxx", t.Text.ToString());
        }

        [TestMethod]
        public void ReplaceAll_ReplacementContainsPattern_DoesNotLoopForever()
        {
            var t = new FakeEditorTarget("a a");
            var r = new SearchEngine(t).ReplaceAll("a", "aa", Wrap);
            Assert.AreEqual(2, r.Count);
            Assert.AreEqual("aa aa", t.Text.ToString());
        }

        [TestMethod]
        public void ReplaceAll_EmptyMatches_InsertAtEachPositionAndTerminate()
        {
            var t = new FakeEditorTarget("ab");
            var r = new SearchEngine(t).ReplaceAll("^", "-", new SearchOptions { UseRegex = true });
            Assert.AreEqual(3, r.Count);
            Assert.AreEqual("-a-b-", t.Text.ToString());
        }

        [TestMethod]
        public void ReplaceAll_NoMatches_ReturnsZeroAndStillClosesUndo()
        {
            var t = new FakeEditorTarget("zzz");
            var r = new SearchEngine(t).ReplaceAll("ab", "X", Wrap);
            Assert.AreEqual(0, r.Count);
            Assert.AreEqual(1, t.UndoActionsCompleted);
        }

        [TestMethod]
        public void ReplaceAll_InvalidRegex_ReportsErrorAndChangesNothing()
        {
            var t = new FakeEditorTarget("abc");
            t.InvalidPatterns.Add("(");
            var r = new SearchEngine(t).ReplaceAll("(", "X", new SearchOptions { UseRegex = true });
            Assert.IsTrue(r.RegexError);
            Assert.AreEqual(0, r.Count);
            Assert.AreEqual("abc", t.Text.ToString());
            Assert.AreEqual(0, t.UndoDepth);
        }

        [TestMethod]
        public void ReplaceAll_NormalisesReplacementNewlines()
        {
            var t = new FakeEditorTarget("a,b") { EolString = "\n" };
            new SearchEngine(t).ReplaceAll(",", "\r\n", Wrap);
            Assert.AreEqual("a\nb", t.Text.ToString());
        }

        [TestMethod]
        public void ReplaceAll_DocumentOverIntMax_ReportsTooLarge()
        {
            var t = new FakeEditorTarget("ab") { LengthOverride = (long)int.MaxValue + 1 };
            var r = new SearchEngine(t).ReplaceAll("ab", "X", Wrap);
            Assert.IsTrue(r.TooLarge);
            Assert.AreEqual(0, t.UndoActionsCompleted);
        }

        [TestMethod]
        public void Count_ReturnsNumberOfMatchesWithoutTouchingSelection()
        {
            var t = new FakeEditorTarget("ab ab ab") { SelectionStart = 1, SelectionEnd = 1 };
            var r = new SearchEngine(t).Count("ab", Wrap, 1000);
            Assert.AreEqual(3, r.Count);
            Assert.IsFalse(r.Capped);
            Assert.AreEqual(0, t.Selections.Count);
            Assert.AreEqual("ab ab ab", t.Text.ToString());
        }

        [TestMethod]
        public void Count_HonoursCap()
        {
            var t = new FakeEditorTarget(new string('a', 30));
            var r = new SearchEngine(t).Count("a", Wrap, 5);
            Assert.AreEqual(5, r.Count);
            Assert.IsTrue(r.Capped);
        }

        [TestMethod]
        public void Count_EmptyPattern_ReportsEmptyPattern()
        {
            var t = new FakeEditorTarget("ab");
            var r = new SearchEngine(t).Count("", Wrap, 1000);
            Assert.IsTrue(r.EmptyPattern);
            Assert.AreEqual(0, r.Count);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests\NppSearchReplace.Core.Tests`
Expected: build errors for `Replace`, `ReplaceAll`, `Count`, `ReplaceResult`.

- [ ] **Step 3: Add result types and the three operations to SearchEngine**

Add these classes to `src/NppSearchReplace.Core/SearchEngine.cs` next to `SearchResult`:

```csharp
    public sealed class ReplaceResult
    {
        public bool Replaced { get; set; }
        public SearchResult Next { get; set; } = new SearchResult();
    }

    public sealed class ReplaceAllResult
    {
        public int Count { get; set; }
        public bool RegexError { get; set; }
        public bool EmptyPattern { get; set; }
        public bool TooLarge { get; set; }
    }

    public sealed class CountResult
    {
        public int Count { get; set; }
        public bool Capped { get; set; }
        public bool RegexError { get; set; }
        public bool EmptyPattern { get; set; }
        public bool TooLarge { get; set; }
    }
```

Add these methods inside `SearchEngine`, after `FindPrevious`:

```csharp
        /// <summary>
        /// If the current selection is exactly a match of pattern, replace it; then find the next match either way.
        /// </summary>
        public ReplaceResult Replace(string pattern, string replacement, SearchOptions options)
        {
            var rr = new ReplaceResult();
            if (string.IsNullOrEmpty(pattern))
            {
                rr.Next = new SearchResult { EmptyPattern = true };
                return rr;
            }
            if (_t.Length > int.MaxValue)
            {
                rr.Next = new SearchResult { TooLarge = true };
                return rr;
            }
            int selStart = _t.SelectionStart;
            int selEnd = _t.SelectionEnd;
            if (selEnd > selStart)
            {
                string p = NormalizeNewlines(pattern);
                _t.SetSearchFlags(options.ToScintillaFlags());
                int found = _t.SearchInRange(selStart, selEnd, p);
                if (found == selStart && _t.TargetEnd == selEnd)
                {
                    int newLen = _t.ReplaceTarget(NormalizeNewlines(replacement ?? ""), options.UseRegex);
                    _t.SetSelection(selStart + newLen, selStart + newLen);
                    rr.Replaced = true;
                }
                else
                {
                    _t.TakeRegexError(); // clear any error; FindNext reports it properly
                }
            }
            rr.Next = FindNext(pattern, options);
            return rr;
        }

        /// <summary>Replace every match in the document inside a single undo action.</summary>
        public ReplaceAllResult ReplaceAll(string pattern, string replacement, SearchOptions options)
        {
            var r = new ReplaceAllResult();
            if (string.IsNullOrEmpty(pattern))
            {
                r.EmptyPattern = true;
                return r;
            }
            if (_t.Length > int.MaxValue)
            {
                r.TooLarge = true;
                return r;
            }
            string p = NormalizeNewlines(pattern);
            string rep = NormalizeNewlines(replacement ?? "");
            _t.SetSearchFlags(options.ToScintillaFlags());
            _t.BeginUndoAction();
            try
            {
                int pos = 0;
                while (true)
                {
                    int length = (int)_t.Length;
                    if (pos > length)
                        break;
                    int found = _t.SearchInRange(pos, length, p);
                    if (found < 0)
                    {
                        r.RegexError = _t.TakeRegexError();
                        break;
                    }
                    int matchEnd = _t.TargetEnd;
                    int newLen = _t.ReplaceTarget(rep, options.UseRegex);
                    r.Count++;
                    int afterReplacement = found + newLen;
                    if (matchEnd > found)
                    {
                        pos = afterReplacement;
                    }
                    else
                    {
                        int next = _t.PositionAfter(afterReplacement);
                        if (next <= afterReplacement)
                            break;
                        pos = next;
                    }
                }
            }
            finally
            {
                _t.EndUndoAction();
            }
            return r;
        }

        /// <summary>Count matches without moving the caret.</summary>
        public CountResult Count(string pattern, SearchOptions options, int cap)
        {
            var r = new CountResult();
            if (string.IsNullOrEmpty(pattern))
            {
                r.EmptyPattern = true;
                return r;
            }
            var matches = MatchEnumerator.Enumerate(_t, NormalizeNewlines(pattern), options, cap);
            r.Count = matches.Matches.Count;
            r.Capped = matches.Capped;
            r.RegexError = matches.RegexError;
            r.TooLarge = matches.TooLarge;
            return r;
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests\NppSearchReplace.Core.Tests`
Expected: `Passed! - Failed: 0, Passed: 62`

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "Add Replace, ReplaceAll and Count to SearchEngine

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 7: Highlighter

**Files:**
- Create: `src/NppSearchReplace.Core/Highlighter.cs`
- Test: `tests/NppSearchReplace.Core.Tests/HighlighterTests.cs`

**Interfaces:**
- Consumes: `IEditorTarget`, `MatchEnumerator`, `SearchOptions`.
- Produces: `public sealed class HighlightResult { int Count; bool Capped; bool RegexError; bool TooLarge; bool IndicatorUnavailable; }`, `public sealed class Highlighter { Highlighter(IEditorTarget target, int cap); HighlightResult Apply(string pattern, SearchOptions options); void ClearCurrent(); bool IsTouched(IntPtr bufferId); void Forget(IntPtr bufferId); }`.

- [ ] **Step 1: Write the failing tests**

`tests/NppSearchReplace.Core.Tests/HighlighterTests.cs`:

```csharp
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NppSearchReplace.Core.Tests
{
    [TestClass]
    public class HighlighterTests
    {
        private static readonly SearchOptions Literal = new SearchOptions();

        [TestMethod]
        public void Apply_AllocatesIndicatorOnceAndStylesIt()
        {
            var t = new FakeEditorTarget("ab ab") { IndicatorToAllocate = 12 };
            var h = new Highlighter(t, 100);
            h.Apply("ab", Literal);
            h.Apply("ab", Literal);
            Assert.AreEqual(1, t.AllocateCalls);
            Assert.IsTrue(t.StyleCalls >= 1);
            Assert.IsTrue(t.Fills.All(f => f.Item1 == 12));
        }

        [TestMethod]
        public void Apply_ClearsWholeDocumentThenFillsEachMatch()
        {
            var t = new FakeEditorTarget("ab-ab");
            var r = new Highlighter(t, 100).Apply("ab", Literal);
            Assert.AreEqual(2, r.Count);
            Assert.AreEqual(1, t.Clears.Count);
            Assert.AreEqual(Tuple.Create(9, 0, 5), t.Clears[0]);
            CollectionAssert.AreEqual(new[] { Tuple.Create(9, 0, 2), Tuple.Create(9, 3, 2) }, t.Fills);
        }

        [TestMethod]
        public void Apply_EmptyMatches_AreCountedButNotFilled()
        {
            var t = new FakeEditorTarget("ab");
            var r = new Highlighter(t, 100).Apply("^", new SearchOptions { UseRegex = true });
            Assert.AreEqual(3, r.Count);
            Assert.AreEqual(0, t.Fills.Count);
        }

        [TestMethod]
        public void Apply_EmptyPattern_ClearsAndReportsZero()
        {
            var t = new FakeEditorTarget("ab");
            var h = new Highlighter(t, 100);
            h.Apply("ab", Literal);
            t.Fills.Clear(); t.Clears.Clear();
            var r = h.Apply("", Literal);
            Assert.AreEqual(0, r.Count);
            Assert.AreEqual(1, t.Clears.Count);
            Assert.AreEqual(0, t.Fills.Count);
        }

        [TestMethod]
        public void Apply_Capped_FillsUpToCapAndFlags()
        {
            var t = new FakeEditorTarget(new string('a', 40));
            var r = new Highlighter(t, 7).Apply("a", Literal);
            Assert.IsTrue(r.Capped);
            Assert.AreEqual(7, r.Count);
            Assert.AreEqual(7, t.Fills.Count);
        }

        [TestMethod]
        public void Apply_InvalidRegex_ClearsPreviousHighlightAndReportsError()
        {
            var t = new FakeEditorTarget("ab");
            var h = new Highlighter(t, 100);
            h.Apply("ab", Literal);
            t.Fills.Clear(); t.Clears.Clear();
            t.InvalidPatterns.Add("(");
            var r = h.Apply("(", new SearchOptions { UseRegex = true });
            Assert.IsTrue(r.RegexError);
            Assert.AreEqual(1, t.Clears.Count);
            Assert.AreEqual(0, t.Fills.Count);
        }

        [TestMethod]
        public void Apply_NoIndicatorAvailable_ReportsAndDoesNothing()
        {
            var t = new FakeEditorTarget("ab") { IndicatorToAllocate = -1 };
            var r = new Highlighter(t, 100).Apply("ab", Literal);
            Assert.IsTrue(r.IndicatorUnavailable);
            Assert.AreEqual(0, t.Fills.Count);
            Assert.AreEqual(0, t.Clears.Count);
        }

        [TestMethod]
        public void Apply_DocumentOverIntMax_ReportsTooLarge()
        {
            var t = new FakeEditorTarget("ab") { LengthOverride = (long)int.MaxValue + 1 };
            var r = new Highlighter(t, 100).Apply("ab", Literal);
            Assert.IsTrue(r.TooLarge);
            Assert.AreEqual(0, t.Fills.Count);
        }

        [TestMethod]
        public void TouchedBuffers_TrackApplyClearAndForget()
        {
            var t = new FakeEditorTarget("ab") { CurrentBufferId = new IntPtr(42) };
            var h = new Highlighter(t, 100);
            Assert.IsFalse(h.IsTouched(new IntPtr(42)));
            h.Apply("ab", Literal);
            Assert.IsTrue(h.IsTouched(new IntPtr(42)));
            h.ClearCurrent();
            Assert.IsFalse(h.IsTouched(new IntPtr(42)));
            h.Apply("ab", Literal);
            h.Forget(new IntPtr(42));
            Assert.IsFalse(h.IsTouched(new IntPtr(42)));
        }

        [TestMethod]
        public void Apply_WithNoMatches_DoesNotMarkBufferTouched()
        {
            var t = new FakeEditorTarget("zz") { CurrentBufferId = new IntPtr(7) };
            var h = new Highlighter(t, 100);
            h.Apply("ab", Literal);
            Assert.IsFalse(h.IsTouched(new IntPtr(7)));
        }

        [TestMethod]
        public void ClearCurrent_BeforeAnyApply_IsANoOp()
        {
            var t = new FakeEditorTarget("ab");
            new Highlighter(t, 100).ClearCurrent();
            Assert.AreEqual(0, t.Clears.Count);
            Assert.AreEqual(0, t.AllocateCalls);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests\NppSearchReplace.Core.Tests`
Expected: build error `The type or namespace name 'Highlighter' could not be found`.

- [ ] **Step 3: Implement Highlighter**

`src/NppSearchReplace.Core/Highlighter.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace NppSearchReplace.Core
{
    public sealed class HighlightResult
    {
        public int Count { get; set; }
        public bool Capped { get; set; }
        public bool RegexError { get; set; }
        public bool TooLarge { get; set; }
        public bool IndicatorUnavailable { get; set; }
    }

    /// <summary>
    /// Paints every match in the active document with one plugin-owned indicator and remembers
    /// which buffers it has painted so they can be cleaned up later.
    /// </summary>
    public sealed class Highlighter
    {
        private readonly IEditorTarget _t;
        private readonly int _cap;
        private int _indicator = -1;
        private readonly HashSet<IntPtr> _touched = new HashSet<IntPtr>();

        public Highlighter(IEditorTarget target, int cap)
        {
            _t = target;
            _cap = cap;
        }

        public HighlightResult Apply(string pattern, SearchOptions options)
        {
            var r = new HighlightResult();
            if (_indicator < 0)
                _indicator = _t.AllocateIndicator();
            if (_indicator < 0)
            {
                r.IndicatorUnavailable = true;
                return r;
            }
            _t.StyleIndicator(_indicator);
            ClearCurrent();

            var matches = MatchEnumerator.Enumerate(_t, pattern, options, _cap);
            r.RegexError = matches.RegexError;
            r.Capped = matches.Capped;
            r.TooLarge = matches.TooLarge;
            r.Count = matches.Matches.Count;
            if (r.Count == 0)
                return r;

            foreach (TextSpan m in matches.Matches)
            {
                if (m.Length > 0)
                    _t.FillIndicator(_indicator, m.Start, m.Length);
            }
            _touched.Add(_t.CurrentBufferId);
            return r;
        }

        /// <summary>Remove the indicator from the whole active document.</summary>
        public void ClearCurrent()
        {
            if (_indicator < 0)
                return;
            long length = _t.Length;
            if (length > int.MaxValue)
                return;
            _t.ClearIndicator(_indicator, 0, (int)length);
            _touched.Remove(_t.CurrentBufferId);
        }

        public bool IsTouched(IntPtr bufferId) => _touched.Contains(bufferId);

        public void Forget(IntPtr bufferId) => _touched.Remove(bufferId);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests\NppSearchReplace.Core.Tests`
Expected: `Passed! - Failed: 0, Passed: 73`

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "Add Highlighter with indicator painting and touched-buffer tracking

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 8: Plugin project from the template, loading in Notepad++

**Files:**
- Create: `src/NppSearchReplace/` (copied template, trimmed)
- Create: `src/NppSearchReplace/NppSearchReplace.csproj` (full content below)
- Create: `src/NppSearchReplace/Main.cs` (full content below)
- Create: `src/NppSearchReplace/Utils/Settings.cs` (full content below)
- Create: `src/NppSearchReplace/Properties/AssemblyInfo.cs`
- Modify: `src/NppSearchReplace/Utils/Translator.cs` (one block)
- Modify: `NppSearchReplace.sln`

**Interfaces:**
- Consumes: nothing from Core yet beyond the ProjectReference.
- Produces: `Main.PluginName = "NppSearchReplace"`, `Main.PluginConfigDirectory`, `Main.settings` (type `Settings`), `Main.searchPanel` (type `SearchPanel`; this task adds a stub `SearchPanel` class in Step 6 that Task 10 replaces), `Main.OnPanelClosedByUser()`, menu command index `IdShowPanel = 0`, and the MSBuild command used from now on.

- [ ] **Step 1: Copy the template project and delete the demo parts**

Run from the repo root (PowerShell):

```powershell
$src = "C:\Users\jmerritt\AppData\Local\Temp\claude\c--Code-NotepadPPSearchReplace\da2f00a3-22bc-4c6e-a2e7-64dd5f107a38\scratchpad\template\NppCSharpPluginPack\NppCSharpPluginPack"
New-Item -ItemType Directory -Force src\NppSearchReplace | Out-Null
Copy-Item "$src\PluginInfrastructure" src\NppSearchReplace\PluginInfrastructure -Recurse
Copy-Item "$src\JSON_Tools" src\NppSearchReplace\JSON_Tools -Recurse
New-Item -ItemType Directory -Force src\NppSearchReplace\Forms, src\NppSearchReplace\Utils, src\NppSearchReplace\Search, src\NppSearchReplace\Properties | Out-Null
Copy-Item "$src\Forms\FormBase.cs", "$src\Forms\FormBase.Designer.cs", "$src\Forms\NppFormHelper.cs" src\NppSearchReplace\Forms\
Copy-Item "$src\Utils\Npp.cs", "$src\Utils\FormStyle.cs", "$src\Utils\Translator.cs", "$src\Utils\ArrayExtensions.cs" src\NppSearchReplace\Utils\
```

Do not copy: `Tests`, `Dependencies`, `Resources`, `Properties\Resources*`, `Forms\AboutForm*`, `Forms\PopupDialog*`, `Forms\SelectionRememberingForm*`, `Forms\DarkModeTestForm*`, `Utils\NppListener.cs`, `Utils\SelectionManager.cs`, `Utils\NanInf.cs`, `Utils\Settings.cs`, `Main.cs`, `.vs`, `*.zip`.

Delete `src\NppSearchReplace\Forms\FormBase.resx` if it was copied (it was not in the list above; verify with `Get-ChildItem src\NppSearchReplace\Forms`).

- [ ] **Step 2: Rename the `NppDemo` namespace to `NppSearchReplace`**

```powershell
Get-ChildItem src\NppSearchReplace -Recurse -Filter *.cs | ForEach-Object {
  (Get-Content $_.FullName -Raw) -replace 'NppDemo', 'NppSearchReplace' | Set-Content $_.FullName -NoNewline
}
```

Then confirm: `Select-String -Path src\NppSearchReplace\**\*.cs -Pattern 'NppDemo' -List` prints nothing.

- [ ] **Step 3: Fix the one template reference to a deleted form**

In `src/NppSearchReplace/Utils/Translator.cs`, find:

```csharp
            if (Main.selectionRememberingForm != null && !Main.selectionRememberingForm.IsDisposed)
            {
                TranslateForm(Main.selectionRememberingForm);
            }
```

Replace with:

```csharp
            if (Main.searchPanel != null && !Main.searchPanel.IsDisposed)
            {
                TranslateForm(Main.searchPanel);
            }
```

- [ ] **Step 4: Write the project file**

`src/NppSearchReplace/NppSearchReplace.csproj`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" ToolsVersion="12.0">
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">x64</Platform>
    <ProductVersion>9.0.21022</ProductVersion>
    <SchemaVersion>2.0</SchemaVersion>
    <ProjectTypeGuids>{60dc8134-eba5-43b8-bcc9-bb4bc16c2548};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids>
    <OutputType>Library</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>NppSearchReplace</RootNamespace>
    <AssemblyName>NppSearchReplace</AssemblyName>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <LangVersion>7.3</LangVersion>
    <ProjectGuid>{7A4F5C21-9B3E-4D6A-8C2F-1E5B7D9A3C40}</ProjectGuid>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|x64' ">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug-x64</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
    <PlatformTarget>x64</PlatformTarget>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|x64' ">
    <DebugType>none</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\Release-x64</OutputPath>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
    <FileAlignment>512</FileAlignment>
    <PlatformTarget>x64</PlatformTarget>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <PropertyGroup>
    <NppPath64 Condition="'$(NppPath64)' == ''">$(ProgramW6432)\Notepad++</NppPath64>
    <StartAction>Program</StartAction>
    <StartProgram>$(NppPath64)\notepad++.exe</StartProgram>
    <RestorePackagesPath>$(MSBuildProjectDirectory)\packages</RestorePackagesPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="UnmanagedExports.Repack.Upgrade" Version="1.2.1" GeneratePathProperty="true">
      <ExcludeAssets>all</ExcludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\NppSearchReplace.Core\NppSearchReplace.Core.csproj">
      <Name>NppSearchReplace.Core</Name>
    </ProjectReference>
  </ItemGroup>
  <ItemGroup>
    <Compile Include="Forms\FormBase.cs">
      <SubType>Form</SubType>
    </Compile>
    <Compile Include="Forms\FormBase.Designer.cs">
      <DependentUpon>FormBase.cs</DependentUpon>
    </Compile>
    <Compile Include="Forms\NppFormHelper.cs" />
    <Compile Include="Forms\SearchPanel.cs">
      <SubType>Form</SubType>
    </Compile>
    <Compile Include="PluginInfrastructure\CLikeStringArray.cs" />
    <Compile Include="PluginInfrastructure\DllExport\DllExportAttribute.cs" />
    <Compile Include="PluginInfrastructure\Docking_h.cs" />
    <Compile Include="PluginInfrastructure\GatewayDomain.cs" />
    <Compile Include="PluginInfrastructure\MenuCmdID_h.cs" />
    <Compile Include="PluginInfrastructure\Msgs_h.cs" />
    <Compile Include="PluginInfrastructure\NotepadPPGateway.cs" />
    <Compile Include="PluginInfrastructure\NppPluginNETBase.cs" />
    <Compile Include="PluginInfrastructure\NppPluginNETHelper.cs" />
    <Compile Include="PluginInfrastructure\Preference_h.cs" />
    <Compile Include="PluginInfrastructure\ScintillaGateway.cs" />
    <Compile Include="PluginInfrastructure\Scintilla_iface.cs" />
    <Compile Include="PluginInfrastructure\SettingsBase.cs" />
    <Compile Include="PluginInfrastructure\UnmanagedExports.cs" />
    <Compile Include="PluginInfrastructure\Win32.cs" />
    <Compile Include="PluginInfrastructure\resource_h.cs" />
    <Compile Include="Main.cs" />
    <Compile Include="Properties\AssemblyInfo.cs" />
    <Compile Include="Utils\FormStyle.cs" />
    <Compile Include="Utils\ArrayExtensions.cs" />
    <Compile Include="Utils\Npp.cs" />
    <Compile Include="Utils\Settings.cs" />
    <Compile Include="Utils\Translator.cs" />
    <Compile Include="JSON_Tools\JNode.cs" />
    <Compile Include="JSON_Tools\JsonParser.cs" />
  </ItemGroup>
  <ItemGroup>
    <Reference Include="System" />
    <Reference Include="System.Data" />
    <Reference Include="System.Drawing" />
    <Reference Include="System.Windows.Forms" />
    <Reference Include="System.Xml" />
    <Reference Include="System.Net.Http" />
  </ItemGroup>
  <Import Project="$(MSBuildBinPath)\Microsoft.CSharp.targets" />
  <Import Condition="Exists($(PkgUnmanagedExports_Repack_Upgrade))" Project="$(PkgUnmanagedExports_Repack_Upgrade)\build\UnmanagedExports.Repack.Upgrade.targets" />
  <!-- Copy the built plugin into Notepad++. Fails quietly when Program Files is not writable; see README. -->
  <Target Name="CopyToNotepadPlusPlus" AfterTargets="Build">
    <ItemGroup>
      <PluginOutput Include="$(OutputPath)\*.dll" />
      <PluginOutput Include="$(OutputPath)\*.pdb" />
    </ItemGroup>
    <Copy SourceFiles="@(PluginOutput)" DestinationFolder="$(NppPath64)\plugins\$(AssemblyName)" SkipUnchangedFiles="true" ContinueOnError="true" />
  </Target>
</Project>
```

- [ ] **Step 5: Write AssemblyInfo**

`src/NppSearchReplace/Properties/AssemblyInfo.cs`:

```csharp
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("NppSearchReplace")]
[assembly: AssemblyDescription("Docked multiline search and replace panel for Notepad++")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("NppSearchReplace")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: Guid("b2a3c4d5-6e7f-4a8b-9c0d-1e2f3a4b5c6d")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
```

- [ ] **Step 6: Write a stub SearchPanel so Main compiles**

`src/NppSearchReplace/Forms/SearchPanel.cs` (Task 10 replaces this file entirely):

```csharp
namespace NppSearchReplace.Forms
{
    /// <summary>Placeholder; replaced in Task 10.</summary>
    public class SearchPanel : FormBase
    {
        public SearchPanel() : base(isModal: false, isDocking: true)
        {
            Text = "Search & Replace";
        }
    }
}
```

- [ ] **Step 7: Write Settings**

`src/NppSearchReplace/Utils/Settings.cs`:

```csharp
using System.ComponentModel;
using CsvQuery.PluginInfrastructure;
using Kbg.NppPluginNET;

namespace NppSearchReplace.Utils
{
    /// <summary>
    /// Persisted in %AppData%\Notepad++\plugins\config\NppSearchReplace\NppSearchReplace.ini by SettingsBase.
    /// Property names are the INI keys, so keep them stable.
    /// </summary>
    public class Settings : SettingsBase
    {
        public override void OnSettingsChanged()
        {
            base.OnSettingsChanged();
            Main.RestyleEverything();
        }

        #region SEARCH
        [Description("Use regular expressions"), Category("Search"), DefaultValue(false)]
        public bool use_regex { get; set; }

        [Description("'.' matches newline (regex only)"), Category("Search"), DefaultValue(false)]
        public bool dot_matches_newline { get; set; }

        [Description("Match case"), Category("Search"), DefaultValue(false)]
        public bool match_case { get; set; }

        [Description("Whole word only (ignored when regex is on)"), Category("Search"), DefaultValue(false)]
        public bool whole_word { get; set; }

        [Description("Wrap around at the end or start of the document"), Category("Search"), DefaultValue(true)]
        public bool wrap_around { get; set; }

        [Description("Highlight all matches in the active document while typing"), Category("Search"), DefaultValue(false)]
        public bool highlight_matches { get; set; }

        [Description("Maximum number of matches to highlight"), Category("Search"), DefaultValue(20000)]
        public int highlight_cap { get; set; }
        #endregion

        #region PANEL
        [Description("Width of the search box as a percentage of the panel (10-90)"), Category("Panel"), DefaultValue(50)]
        public int splitter_percent { get; set; }

        [Description("Re-open the panel at startup if it was open when Notepad++ closed"), Category("Panel"), DefaultValue(false)]
        public bool panel_visible { get; set; }
        #endregion

        #region STYLING
        [Description("Use the same colors as the editor window for this plugin's forms?"), Category("Styling"), DefaultValue(true)]
        public bool use_npp_styling { get; set; }
        #endregion
    }
}
```

- [ ] **Step 8: Write Main**

`src/NppSearchReplace/Main.cs`:

```csharp
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Kbg.NppPluginNET.PluginInfrastructure;
using NppSearchReplace.Forms;
using NppSearchReplace.Utils;

namespace Kbg.NppPluginNET
{
    class Main
    {
        internal const string PluginName = "NppSearchReplace";
        public static readonly string PluginConfigDirectory = Path.Combine(Npp.notepad.GetConfigDirectory(), PluginName);
        public static Settings settings = new Settings();
        public static SearchPanel searchPanel = null;
        public static bool isShuttingDown = false;
        internal const int IdShowPanel = 0;

        static internal void CommandMenuInit()
        {
            // dependent assemblies (NppSearchReplace.Core.dll) live next to the plugin, not next to notepad++.exe
            AppDomain.CurrentDomain.AssemblyResolve += LoadDependency;
            Translator.ResetTranslations(true);

            PluginBase.SetCommand(IdShowPanel, "Show Search && Replace Panel", ToggleSearchPanel, new ShortcutKey(true, true, false, Keys.F));
            PluginBase.SetCommand(1, "---", null);
            PluginBase.SetCommand(2, "&Settings", OpenSettings);
            PluginBase.SetCommand(3, "&About", ShowAbout);
        }

        private static Assembly LoadDependency(object sender, ResolveEventArgs args)
        {
            string assemblyFile = Path.Combine(Npp.pluginDllDirectory, new AssemblyName(args.Name).Name) + ".dll";
            if (File.Exists(assemblyFile))
                return Assembly.LoadFrom(assemblyFile);
            return null;
        }

        static internal void SetToolBarIcons()
        {
            // no toolbar icons in v1
        }

        public static void OnNotification(ScNotification notification)
        {
            uint code = notification.Header.Code;
            switch (code)
            {
            case (uint)NppMsg.NPPN_READY:
                if (settings.panel_visible)
                    ShowSearchPanel();
                break;
            case (uint)NppMsg.NPPN_WORDSTYLESUPDATED:
                RestyleEverything();
                break;
            case (uint)NppMsg.NPPN_NATIVELANGCHANGED:
                Translator.ResetTranslations(false);
                break;
            }
        }

        static internal void PluginCleanUp()
        {
            isShuttingDown = true;
            settings.SaveToIniFile();
            if (searchPanel != null && !searchPanel.IsDisposed)
            {
                searchPanel.Close();
                searchPanel.Dispose();
            }
        }

        public static void RestyleEverything()
        {
            if (searchPanel != null && !searchPanel.IsDisposed)
                FormStyle.ApplyStyle(searchPanel, settings.use_npp_styling);
        }

        static void OpenSettings()
        {
            settings.ShowDialog();
        }

        static void ShowAbout()
        {
            MessageBox.Show(
                $"NppSearchReplace v{Npp.AssemblyVersionString()}\r\n" +
                "Docked multiline search and replace panel for Notepad++.\r\n" +
                $"Notepad++ {Npp.nppVersionStr}",
                "About NppSearchReplace", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void ToggleSearchPanel()
        {
            bool visible = searchPanel != null && !searchPanel.IsDisposed && searchPanel.Visible;
            if (visible)
                HideSearchPanel();
            else
                ShowSearchPanel();
        }

        public static void ShowSearchPanel()
        {
            if (searchPanel == null || searchPanel.IsDisposed)
            {
                searchPanel = new SearchPanel();
                RegisterDockingPanel(searchPanel);
            }
            Npp.notepad.ShowDockingForm(searchPanel);
            settings.panel_visible = true;
            SetPanelMenuCheck(true);
        }

        public static void HideSearchPanel()
        {
            if (searchPanel != null && !searchPanel.IsDisposed)
                Npp.notepad.HideDockingForm(searchPanel);
            settings.panel_visible = false;
            SetPanelMenuCheck(false);
        }

        /// <summary>Called by the panel when the user closes it with the docking X button.</summary>
        public static void OnPanelClosedByUser()
        {
            settings.panel_visible = false;
            SetPanelMenuCheck(false);
        }

        private static void SetPanelMenuCheck(bool isChecked)
        {
            Win32.SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_SETMENUITEMCHECK,
                PluginBase._funcItems.Items[IdShowPanel]._cmdID, isChecked ? 1 : 0);
        }

        private static void RegisterDockingPanel(Form form)
        {
            var data = new NppTbData
            {
                hClient = form.Handle,
                pszName = "Search & Replace",
                dlgID = IdShowPanel,
                uMask = NppTbMsg.DWS_DF_CONT_BOTTOM,
                pszModuleName = PluginName, // same convention as the template; Notepad++ uses it to remember the panel's dock position
            };
            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(data));
            Marshal.StructureToPtr(data, ptr, false);
            Win32.SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_DMMREGASDCKDLG, 0, ptr);
            // Notepad++ keeps a pointer to this struct, so it is intentionally never freed.
        }
    }
}
```

- [ ] **Step 9: Add the plugin project to the solution**

```powershell
dotnet sln NppSearchReplace.sln add src\NppSearchReplace\NppSearchReplace.csproj
```

- [ ] **Step 10: Restore and build the plugin**

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" src\NppSearchReplace\NppSearchReplace.csproj /t:Restore /v:minimal
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" src\NppSearchReplace\NppSearchReplace.csproj /p:Configuration=Release /p:Platform=x64 /v:minimal
```

Expected: `Build succeeded.` and `src\NppSearchReplace\bin\Release-x64\NppSearchReplace.dll` plus `NppSearchReplace.Core.dll` exist. A warning about the copy to Program Files failing is acceptable.

If the build fails with errors about `DllExport` or `ildasm`, run the restore again (the package must be restored before the targets import) and rebuild.

- [ ] **Step 11: Install into Notepad++ and verify it loads**

If the post-build copy failed (Program Files is read-only for normal users), run once from an elevated PowerShell:

```powershell
New-Item -ItemType Directory -Force "C:\Program Files\Notepad++\plugins\NppSearchReplace" | Out-Null
Copy-Item src\NppSearchReplace\bin\Release-x64\NppSearchReplace*.dll "C:\Program Files\Notepad++\plugins\NppSearchReplace\" -Force
```

Then start Notepad++ (close any running instance first) and check: menu **Plugins > NppSearchReplace** lists "Show Search & Replace Panel", "Settings", "About". Click "Show Search & Replace Panel": an empty panel titled "Search & Replace" docks at the bottom. Ctrl+Alt+F toggles it. "About" shows the version box. Confirm `%AppData%\Notepad++\plugins\config\NppSearchReplace\NppSearchReplace.ini` exists after closing Notepad++.

- [ ] **Step 12: Run Core tests still pass, then commit**

Run: `dotnet test tests\NppSearchReplace.Core.Tests`
Expected: `Passed! - Failed: 0, Passed: 73`

```powershell
git add -A
git commit -m "Add plugin project from NppCSharpPluginPack template with docking stub

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 9: FormBase hooks and MultilineTextBox

**Files:**
- Modify: `src/NppSearchReplace/Forms/FormBase.cs`
- Create: `src/NppSearchReplace/Forms/MultilineTextBox.cs`
- Modify: `src/NppSearchReplace/NppSearchReplace.csproj` (add Compile item)

**Interfaces:**
- Produces: `FormBase.AddKeyUpDownPressHandlers(Control ctrl)` becomes `protected virtual`; new `protected virtual void OnDockingPanelClosed()` called on `DMN_CLOSE`; `public class MultilineTextBox : TextBox` that receives Enter, Tab and Escape as ordinary `KeyDown` events even inside a Notepad++ modeless docked dialog.

- [ ] **Step 1: Make FormBase's key wiring overridable**

In `src/NppSearchReplace/Forms/FormBase.cs` change

```csharp
        private void AddKeyUpDownPressHandlers(Control ctrl = null)
```

to

```csharp
        protected virtual void AddKeyUpDownPressHandlers(Control ctrl = null)
```

- [ ] **Step 2: Add the docking-close hook**

In the same file, inside `WndProc`, change

```csharp
                        case DockMgrMsg.DMN_CLOSE:  // we are being closed
                            break;
```

to

```csharp
                        case DockMgrMsg.DMN_CLOSE:  // we are being closed
                            OnDockingPanelClosed();
                            break;
```

and add this method to the class, after `RemoveControlParent`:

```csharp
        /// <summary>Called when the user closes a docked panel with its X button.</summary>
        protected virtual void OnDockingPanelClosed()
        {
        }
```

- [ ] **Step 3: Write MultilineTextBox**

`src/NppSearchReplace/Forms/MultilineTextBox.cs`:

```csharp
using System;
using System.Windows.Forms;

namespace NppSearchReplace.Forms
{
    /// <summary>
    /// Notepad++ runs IsDialogMessage over registered modeless dialogs, which turns Enter into the default
    /// button and Tab into focus movement before the text box ever sees them. Answering WM_GETDLGCODE with
    /// DLGC_WANTALLKEYS makes every key reach this control as a normal KeyDown, so the panel decides what
    /// Enter, Tab and Escape do.
    /// </summary>
    public class MultilineTextBox : TextBox
    {
        private const int WM_GETDLGCODE = 0x0087;
        private const int DLGC_WANTARROWS = 0x0001;
        private const int DLGC_WANTTAB = 0x0002;
        private const int DLGC_WANTALLKEYS = 0x0004;
        private const int DLGC_HASSETSEL = 0x0008;
        private const int DLGC_WANTCHARS = 0x0080;

        public MultilineTextBox()
        {
            Multiline = true;
            AcceptsReturn = true;
            AcceptsTab = true;
            WordWrap = false;
            ScrollBars = ScrollBars.Both;
            HideSelection = false;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_GETDLGCODE)
            {
                m.Result = new IntPtr(DLGC_WANTALLKEYS | DLGC_WANTARROWS | DLGC_WANTTAB | DLGC_WANTCHARS | DLGC_HASSETSEL);
                return;
            }
            base.WndProc(ref m);
        }

        /// <summary>Keep WinForms from treating Tab/Enter/Escape as dialog keys; KeyDown handles them.</summary>
        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Tab:
                case Keys.Enter:
                case Keys.Escape:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        /// <summary>Insert text at the caret, replacing any selection, and keep the caret after it.</summary>
        public void InsertAtCaret(string text)
        {
            int start = SelectionStart;
            SelectedText = text;
            SelectionStart = start + text.Length;
            SelectionLength = 0;
            ScrollToCaret();
        }
    }
}
```

- [ ] **Step 4: Register the file in the project**

In `src/NppSearchReplace/NppSearchReplace.csproj`, after the `Forms\NppFormHelper.cs` Compile item add:

```xml
    <Compile Include="Forms\MultilineTextBox.cs">
      <SubType>Component</SubType>
    </Compile>
```

- [ ] **Step 5: Build**

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" src\NppSearchReplace\NppSearchReplace.csproj /p:Configuration=Release /p:Platform=x64 /v:minimal
```

Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "Add FormBase hooks and MultilineTextBox that keeps dialog keys

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 10: SearchPanel UI with find/replace actions and the Scintilla adapter

**Files:**
- Create: `src/NppSearchReplace/Search/ScintillaEditorTarget.cs`
- Replace: `src/NppSearchReplace/Forms/SearchPanel.cs`
- Modify: `src/NppSearchReplace/NppSearchReplace.csproj` (add Compile item for the adapter)
- Modify: `src/NppSearchReplace/Main.cs` (prefill selection on show)

**Interfaces:**
- Consumes: `SearchEngine`, `SearchOptions`, `SearchResult`, `ReplaceResult`, `ReplaceAllResult`, `CountResult`, `IEditorTarget` from Core; `MultilineTextBox`, `FormBase` hooks from Task 9; `Main.settings`, `Main.OnPanelClosedByUser()`.
- Produces: `public sealed class ScintillaEditorTarget : IEditorTarget`; `SearchPanel` with `public void FocusSearchBox(string prefill)`, `public SearchOptions CurrentOptions`, and the status label. Task 11 adds highlighting hooks (`OnBufferActivated`, `OnFileClosed`, `OnDocumentModified`) and Task 12 adds history.

- [ ] **Step 1: Write the Scintilla adapter**

`src/NppSearchReplace/Search/ScintillaEditorTarget.cs`:

```csharp
using System;
using System.Text;
using Kbg.NppPluginNET.PluginInfrastructure;
using NppSearchReplace.Core;
using NppSearchReplace.Utils;

namespace NppSearchReplace.Search
{
    /// <summary>
    /// IEditorTarget over whichever Scintilla view is active right now. A fresh gateway is created per call
    /// because the active view can change between calls (two-view mode).
    /// </summary>
    public sealed class ScintillaEditorTarget : IEditorTarget
    {
        private int _indicator = -1;

        private static ScintillaGateway Ed => new ScintillaGateway(PluginBase.GetCurrentScintilla());

        public long Length => Ed.GetLength();
        public int SelectionStart => Ed.GetSelectionStart();
        public int SelectionEnd => Ed.GetSelectionEnd();
        public int TargetStart => Ed.GetTargetStart();
        public int TargetEnd => Ed.GetTargetEnd();

        public string EolString
        {
            get
            {
                switch (Ed.GetEOLMode())
                {
                    case EndOfLine.CR: return "\r";
                    case EndOfLine.LF: return "\n";
                    default: return "\r\n";
                }
            }
        }

        public IntPtr CurrentBufferId =>
            Win32.SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_GETCURRENTBUFFERID, 0, 0);

        public void SetSearchFlags(int scintillaFlags) => Ed.SetSearchFlags((FindOption)scintillaFlags);

        public int SearchInRange(int start, int end, string pattern)
        {
            ScintillaGateway ed = Ed;
            ed.SetTargetRange(start, end);
            Encoding enc = ed.GetCodePage();
            return ed.SearchInTarget(enc.GetByteCount(pattern), pattern, enc);
        }

        public int ReplaceTarget(string replacement, bool useRegexGroups)
        {
            ScintillaGateway ed = Ed;
            Encoding enc = ed.GetCodePage();
            int len = enc.GetByteCount(replacement);
            return useRegexGroups ? ed.ReplaceTargetRE(len, replacement, enc) : ed.ReplaceTarget(len, replacement, enc);
        }

        public bool TakeRegexError()
        {
            ScintillaGateway ed = Ed;
            Status status = ed.GetStatus();
            if (status != Status.OK)
                ed.SetStatus(Status.OK);
            return status == Status.WARN_REGEX;
        }

        public int PositionAfter(int position) => Ed.PositionAfter(position);

        public int LineFromPosition(int position) => Ed.LineFromPosition(position);

        public void SetSelection(int anchor, int caret) => Ed.SetSel(anchor, caret);

        public void ScrollCaretIntoView()
        {
            ScintillaGateway ed = Ed;
            ed.EnsureVisible(ed.LineFromPosition(ed.GetCurrentPos()));
            ed.ScrollCaret();
        }

        public void BeginUndoAction() => Ed.BeginUndoAction();

        public void EndUndoAction() => Ed.EndUndoAction();

        public int AllocateIndicator()
        {
            if (_indicator < 0 && Npp.notepad.AllocateIndicators(1, out int[] ids) && ids != null && ids.Length > 0)
                _indicator = ids[0];
            return _indicator;
        }

        public void StyleIndicator(int indicator)
        {
            ScintillaGateway ed = Ed;
            ed.IndicSetStyle(indicator, IndicatorStyle.ROUNDBOX);
            ed.IndicSetFore(indicator, new Colour(0xFF, 0xA5, 0x00)); // orange, readable on light and dark themes
            ed.IndicSetAlpha(indicator, (Alpha)70);
            ed.IndicSetOutlineAlpha(indicator, (Alpha)140);
            ed.IndicSetUnder(indicator, true);
        }

        public void FillIndicator(int indicator, int start, int length)
        {
            ScintillaGateway ed = Ed;
            ed.SetIndicatorCurrent(indicator);
            ed.IndicatorFillRange(start, length);
        }

        public void ClearIndicator(int indicator, int start, int length)
        {
            ScintillaGateway ed = Ed;
            ed.SetIndicatorCurrent(indicator);
            ed.IndicatorClearRange(start, length);
        }
    }
}
```

- [ ] **Step 2: Write the SearchPanel (replacing the stub)**

`src/NppSearchReplace/Forms/SearchPanel.cs`:

```csharp
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Kbg.NppPluginNET;
using NppSearchReplace.Core;
using NppSearchReplace.Search;
using NppSearchReplace.Utils;

namespace NppSearchReplace.Forms
{
    /// <summary>
    /// The docked panel. Layout is built in code (no designer/resx). All editor work goes through
    /// SearchEngine; this class only translates clicks and keys into engine calls and status text.
    /// </summary>
    public class SearchPanel : FormBase
    {
        private const int StatusErrorColor = unchecked((int)0xFFC03030);

        private readonly IEditorTarget _editor = new ScintillaEditorTarget();
        private readonly SearchEngine _engine;

        private CheckBox _regexToggle, _dotToggle, _caseToggle, _wordsToggle, _loopToggle, _highlightToggle;
        private Button _findNextButton, _findPrevButton, _replaceButton, _replaceAllButton, _countButton;
        private SplitContainer _split;
        private MultilineTextBox _searchBox, _replaceBox;
        private Label _statusLabel;
        private Color _statusDefaultColor;

        public SearchPanel() : base(isModal: false, isDocking: true)
        {
            _engine = new SearchEngine(_editor);
            Text = "Search & Replace";
            BuildLayout();
            LoadOptionsFromSettings();
        }

        public SearchOptions CurrentOptions => new SearchOptions
        {
            UseRegex = _regexToggle.Checked,
            DotMatchesNewline = _dotToggle.Checked,
            MatchCase = _caseToggle.Checked,
            WholeWord = _wordsToggle.Checked,
            WrapAround = _loopToggle.Checked,
        };

        public string SearchText => _searchBox.Text;
        public string ReplaceText => _replaceBox.Text;
        public bool HighlightEnabled => _highlightToggle.Checked;

        /// <summary>Put the caret in the search box, optionally replacing its text first.</summary>
        public void FocusSearchBox(string prefill)
        {
            if (!string.IsNullOrEmpty(prefill))
                _searchBox.Text = prefill;
            _searchBox.SelectAll();
            _searchBox.Focus();
        }

        #region layout

        private void BuildLayout()
        {
            SuspendLayout();
            MinimumSize = new Size(400, 120);
            ClientSize = new Size(900, 180);
            Font = SystemFonts.MessageBoxFont;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(4, 2, 4, 2) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var optionsRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            _regexToggle = MakeToggle("Regex", "Regular expression search (Notepad++ / Boost syntax)");
            _dotToggle = MakeToggle("Dot", "'.' also matches newline (regex only)");
            _caseToggle = MakeToggle("Case", "Match case");
            _wordsToggle = MakeToggle("Words", "Whole word only (ignored when Regex is on)");
            _loopToggle = MakeToggle("Loop", "Wrap around at the end or start of the document");
            _highlightToggle = MakeToggle("Highlight", "Highlight every match in the document while typing");
            optionsRow.Controls.AddRange(new Control[] { _regexToggle, _dotToggle, _caseToggle, _wordsToggle, _loopToggle, _highlightToggle });

            var actionsRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            _findNextButton = MakeButton("Find Next", "Ctrl+Enter", (s, e) => DoFindNext());
            _findPrevButton = MakeButton("Find Prev", "Ctrl+Shift+Enter", (s, e) => DoFindPrevious());
            _replaceButton = MakeButton("Replace", "Ctrl+R", (s, e) => DoReplace());
            _replaceAllButton = MakeButton("Replace All", "Ctrl+Shift+R", (s, e) => DoReplaceAll());
            _countButton = MakeButton("Count", "Ctrl+Shift+C", (s, e) => DoCount());
            actionsRow.Controls.AddRange(new Control[] { _findNextButton, _findPrevButton, _replaceButton, _replaceAllButton, _countButton });

            _split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterWidth = 6, Margin = new Padding(0, 2, 0, 2) };
            _searchBox = MakeBox();
            _replaceBox = MakeBox();
            _split.Panel1.Controls.Add(_searchBox);
            _split.Panel2.Controls.Add(_replaceBox);

            _statusLabel = new Label { Dock = DockStyle.Fill, AutoSize = true, Text = "Enter a search text", Margin = new Padding(2, 0, 0, 0) };
            _statusDefaultColor = _statusLabel.ForeColor;

            root.Controls.Add(optionsRow, 0, 0);
            root.Controls.Add(actionsRow, 0, 1);
            root.Controls.Add(_split, 0, 2);
            root.Controls.Add(_statusLabel, 0, 3);
            Controls.Add(root);

            _regexToggle.CheckedChanged += (s, e) => { _dotToggle.Enabled = _regexToggle.Checked; _wordsToggle.Enabled = !_regexToggle.Checked; };
            _dotToggle.Enabled = false;
            ResumeLayout(true);
        }

        private static CheckBox MakeToggle(string text, string tip)
        {
            var cb = new CheckBox
            {
                Text = text,
                Appearance = Appearance.Button,
                AutoSize = true,
                MinimumSize = new Size(54, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 0, 3, 0),
                TabStop = false,
            };
            new ToolTip().SetToolTip(cb, tip);
            return cb;
        }

        private static Button MakeButton(string text, string shortcut, EventHandler onClick)
        {
            var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(78, 24), Margin = new Padding(0, 0, 3, 0), TabStop = false };
            new ToolTip().SetToolTip(b, shortcut);
            b.Click += onClick;
            return b;
        }

        private MultilineTextBox MakeBox()
        {
            var box = new MultilineTextBox { Dock = DockStyle.Fill, Font = new Font(FontFamily.GenericMonospace, 10f) };
            box.KeyDown += Box_KeyDown;
            return box;
        }

        /// <summary>
        /// FormBase wires generic Enter/Escape handlers to every control. The two text boxes handle their own
        /// keys in Box_KeyDown, so skip them to avoid a second newline on Enter.
        /// </summary>
        protected override void AddKeyUpDownPressHandlers(Control ctrl = null)
        {
            if (ctrl == null)
                ctrl = this;
            if (ctrl is MultilineTextBox)
                return;
            ctrl.KeyUp += (sender, e) => NppFormHelper.GenericKeyUpHandler(this, sender, e, IsModal);
            ctrl.KeyDown += NppFormHelper.GenericKeyDownHandler;
            if (ctrl.HasChildren)
                foreach (Control child in ctrl.Controls)
                    AddKeyUpDownPressHandlers(child);
        }

        protected override void OnDockingPanelClosed()
        {
            Main.OnPanelClosedByUser();
        }

        #endregion

        #region keys

        private void Box_KeyDown(object sender, KeyEventArgs e)
        {
            var box = (MultilineTextBox)sender;
            bool handled = true;
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    if (e.Control && e.Shift) DoFindPrevious();
                    else if (e.Control) DoFindNext();
                    else box.InsertAtCaret("\r\n");
                    break;
                case Keys.Tab:
                    (box == _searchBox ? _replaceBox : _searchBox).Focus();
                    break;
                case Keys.Escape:
                    Npp.editor.GrabFocus();
                    break;
                case Keys.R when e.Control && e.Shift:
                    DoReplaceAll();
                    break;
                case Keys.R when e.Control:
                    DoReplace();
                    break;
                case Keys.C when e.Control && e.Shift:
                    DoCount();
                    break;
                case Keys.A when e.Control:
                    box.SelectAll();
                    break;
                default:
                    handled = false;
                    break;
            }
            if (handled)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        #endregion

        #region actions

        private void DoFindNext() => Guarded(() => ReportFind(_engine.FindNext(SearchText, CurrentOptions)));

        private void DoFindPrevious() => Guarded(() => ReportFind(_engine.FindPrevious(SearchText, CurrentOptions)));

        private void DoReplace() => Guarded(() =>
        {
            ReplaceResult r = _engine.Replace(SearchText, ReplaceText, CurrentOptions);
            ReportFind(r.Next, r.Replaced ? "Replaced; " : "");
        });

        private void DoReplaceAll() => Guarded(() =>
        {
            ReplaceAllResult r = _engine.ReplaceAll(SearchText, ReplaceText, CurrentOptions);
            if (r.EmptyPattern) SetStatus("Enter a search text", false);
            else if (r.TooLarge) SetStatus("Document too large (over 2 GB)", true);
            else if (r.RegexError) SetStatus("Invalid regular expression", true);
            else SetStatus("Replaced " + r.Count, false);
        });

        private void DoCount() => Guarded(() =>
        {
            CountResult r = _engine.Count(SearchText, CurrentOptions, 1000000);
            if (r.EmptyPattern) SetStatus("Enter a search text", false);
            else if (r.TooLarge) SetStatus("Document too large (over 2 GB)", true);
            else if (r.RegexError) SetStatus("Invalid regular expression", true);
            else SetStatus(r.Count + " matches" + (r.Capped ? " (or more)" : ""), false);
        });

        private void ReportFind(SearchResult r, string prefix = "")
        {
            if (r.EmptyPattern) SetStatus("Enter a search text", false);
            else if (r.TooLarge) SetStatus("Document too large (over 2 GB)", true);
            else if (r.RegexError) SetStatus("Invalid regular expression", true);
            else if (!r.Found) SetStatus(prefix + "No more matches", false);
            else if (r.Wrapped) SetStatus(prefix + "Wrapped (line " + (r.Line + 1) + ")", false);
            else SetStatus(prefix + "Match found (line " + (r.Line + 1) + ")", false);
        }

        private void SetStatus(string text, bool isError)
        {
            _statusLabel.Text = text;
            _statusLabel.ForeColor = isError ? Color.FromArgb(StatusErrorColor) : _statusDefaultColor;
        }

        /// <summary>Never let an exception escape into Notepad++; show it once and log it.</summary>
        private void Guarded(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                SetStatus("Error: " + ex.Message, true);
                try
                {
                    Directory.CreateDirectory(Main.PluginConfigDirectory);
                    File.AppendAllText(Path.Combine(Main.PluginConfigDirectory, "error.log"),
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + ex + Environment.NewLine);
                }
                catch (Exception)
                {
                    // nothing more to do
                }
            }
        }

        #endregion

        #region settings

        private void LoadOptionsFromSettings()
        {
            Settings s = Main.settings;
            _regexToggle.Checked = s.use_regex;
            _dotToggle.Checked = s.dot_matches_newline;
            _caseToggle.Checked = s.match_case;
            _wordsToggle.Checked = s.whole_word;
            _loopToggle.Checked = s.wrap_around;
            _highlightToggle.Checked = s.highlight_matches;
            _dotToggle.Enabled = s.use_regex;
            _wordsToggle.Enabled = !s.use_regex;
        }

        public void SaveOptionsToSettings()
        {
            Settings s = Main.settings;
            s.use_regex = _regexToggle.Checked;
            s.dot_matches_newline = _dotToggle.Checked;
            s.match_case = _caseToggle.Checked;
            s.whole_word = _wordsToggle.Checked;
            s.wrap_around = _loopToggle.Checked;
            s.highlight_matches = _highlightToggle.Checked;
        }

        #endregion
    }
}
```

- [ ] **Step 3: Register the adapter file in the project**

In `src/NppSearchReplace/NppSearchReplace.csproj`, after `<Compile Include="Main.cs" />` add:

```xml
    <Compile Include="Search\ScintillaEditorTarget.cs" />
```

- [ ] **Step 4: Prefill from the editor selection and save options on shutdown**

In `src/NppSearchReplace/Main.cs`:

Replace the body of `ShowSearchPanel` with:

```csharp
            if (searchPanel == null || searchPanel.IsDisposed)
            {
                searchPanel = new SearchPanel();
                RegisterDockingPanel(searchPanel);
            }
            Npp.notepad.ShowDockingForm(searchPanel);
            settings.panel_visible = true;
            SetPanelMenuCheck(true);
            string selection = Npp.editor.GetSelText();
            bool singleLine = selection.IndexOf('\n') < 0 && selection.IndexOf('\r') < 0;
            searchPanel.FocusSearchBox(singleLine ? selection : null);
```

In `PluginCleanUp`, before `settings.SaveToIniFile();` insert:

```csharp
            if (searchPanel != null && !searchPanel.IsDisposed)
                searchPanel.SaveOptionsToSettings();
```

Also, `NPPN_READY` must not steal focus at startup. Change the `NPPN_READY` case to:

```csharp
            case (uint)NppMsg.NPPN_READY:
                if (settings.panel_visible)
                {
                    ShowSearchPanel();
                    Npp.editor.GrabFocus();
                }
                break;
```

- [ ] **Step 5: Build and install**

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" src\NppSearchReplace\NppSearchReplace.csproj /p:Configuration=Release /p:Platform=x64 /v:minimal
```

Expected: `Build succeeded.` Copy the DLLs into `C:\Program Files\Notepad++\plugins\NppSearchReplace\` as in Task 8 Step 11 if the post-build copy failed (Notepad++ must be closed while copying).

- [ ] **Step 6: Manual verification in Notepad++**

Open a new document and paste exactly:

```
apple banana apple
Banana cherry
café apple
```

1. Ctrl+Alt+F shows the panel at the bottom with the two boxes and the toggle/button rows. The search box has focus.
2. Type `apple`, press **Ctrl+Enter** three times: selections land on each `apple` in order, status reads `Match found (line 1)`, `Match found (line 1)`, `Match found (line 3)`; a fourth press reads `Wrapped (line 1)`. Turn **Loop** off and repeat from the last match: `No more matches`.
3. In the search box press **Enter**: a new line is inserted (no find runs). Press **Escape**: focus goes to the editor. **Tab** moves between the two boxes.
4. Type `banana` with **Case** off: Ctrl+Enter finds both `banana` and `Banana`. Case on: only the lowercase one.
5. Non-ASCII: search `café apple` literally: it is found and the selection covers exactly `café apple` (no off-by-one from the two-byte `é`). Then search `apple` again from before it; the selection on line 3 must land on `apple`, not one byte early.
6. Regex: enable **Regex**, search `(ap)(ple)`, replacement `$2$1`, Ctrl+Enter then **Ctrl+R**: the selected `apple` becomes `pleap` and the next `apple` is selected. Status for **Ctrl+Shift+R** reads `Replaced N`. Ctrl+Z undoes the whole Replace All in one step.
7. Regex `(` : status shows `Invalid regular expression` in red; no message box.
8. Multiline literal: with Regex off type `apple` Enter `Banana` (two lines in the box). Ctrl+Enter selects across the line break regardless of the file's EOL setting (Edit > EOL Conversion to Unix and retry).
9. **Dot** is only enabled while Regex is on; **Words** is only enabled while Regex is off.
10. Count: `apple` reports `3 matches` (after undoing step 6) without moving the selection.

Fix anything that fails before continuing.

- [ ] **Step 7: Commit**

```powershell
git add -A
git commit -m "Add SearchPanel with find/replace actions and Scintilla editor adapter

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 11: Live highlighting

**Files:**
- Modify: `src/NppSearchReplace/Forms/SearchPanel.cs`
- Modify: `src/NppSearchReplace/Main.cs`

**Interfaces:**
- Consumes: `Highlighter`, `HighlightResult` from Core (Task 7); `ScNotification.ModificationType`, `NppMsg.NPPN_BUFFERACTIVATED`, `NppMsg.NPPN_FILECLOSED`, `SciMsg.SCN_MODIFIED`.
- Produces: `SearchPanel.OnBufferActivated(IntPtr bufferId)`, `SearchPanel.OnFileClosed(IntPtr bufferId)`, `SearchPanel.OnDocumentModified(int modificationType)`, `SearchPanel.ClearHighlights()`.

- [ ] **Step 1: Add highlighter state and the debounce timer to SearchPanel**

In `SearchPanel`, add fields after `_engine`:

```csharp
        private readonly Highlighter _highlighter;
        private readonly Timer _highlightTimer = new Timer { Interval = 300 };
        private bool _suppressModifiedNotifications;
        private const int ScModInsertText = 0x1;
        private const int ScModDeleteText = 0x2;
```

In the constructor, after `_engine = new SearchEngine(_editor);` add:

```csharp
            _highlighter = new Highlighter(_editor, Math.Max(1, Main.settings.highlight_cap));
            _highlightTimer.Tick += (s, e) => { _highlightTimer.Stop(); RefreshHighlight(); };
```

At the end of `BuildLayout()` (before `ResumeLayout(true);`) add:

```csharp
            _searchBox.TextChanged += (s, e) => ScheduleHighlight();
            foreach (CheckBox toggle in new[] { _regexToggle, _dotToggle, _caseToggle, _wordsToggle })
                toggle.CheckedChanged += (s, e) => ScheduleHighlight();
            _highlightToggle.CheckedChanged += (s, e) => { if (_highlightToggle.Checked) ScheduleHighlight(); else Guarded(ClearHighlights); };
```

- [ ] **Step 2: Add the highlight methods**

Add a new region to `SearchPanel`:

```csharp
        #region highlighting

        private void ScheduleHighlight()
        {
            if (!HighlightEnabled)
                return;
            _highlightTimer.Stop();
            _highlightTimer.Start();
        }

        private void RefreshHighlight()
        {
            Guarded(() =>
            {
                if (!HighlightEnabled || !Visible || Main.isShuttingDown)
                    return;
                HighlightResult r = _highlighter.Apply(SearchText, CurrentOptions);
                if (r.IndicatorUnavailable) SetStatus("Highlighting unavailable (no free indicator)", true);
                else if (r.TooLarge) SetStatus("Document too large (over 2 GB)", true);
                else if (r.RegexError) SetStatus("Invalid regular expression", true);
                else if (r.Capped) SetStatus("Highlight capped at " + r.Count + " matches", false);
                else if (string.IsNullOrEmpty(SearchText)) SetStatus("Enter a search text", false);
                else SetStatus(r.Count + " matches", false);
            });
        }

        public void ClearHighlights()
        {
            _highlightTimer.Stop();
            _highlighter.ClearCurrent();
        }

        /// <summary>Another document became active: repaint it, or clean it up if we painted it earlier.</summary>
        public void OnBufferActivated(IntPtr bufferId)
        {
            Guarded(() =>
            {
                if (HighlightEnabled && Visible && !string.IsNullOrEmpty(SearchText))
                    ScheduleHighlight();
                else if (_highlighter.IsTouched(bufferId))
                    _highlighter.ClearCurrent();
            });
        }

        public void OnFileClosed(IntPtr bufferId)
        {
            _highlighter.Forget(bufferId);
        }

        public void OnDocumentModified(int modificationType)
        {
            if (_suppressModifiedNotifications || !HighlightEnabled || !Visible)
                return;
            if ((modificationType & (ScModInsertText | ScModDeleteText)) != 0)
                ScheduleHighlight();
        }

        #endregion
```

- [ ] **Step 3: Keep Replace All from re-triggering the timer on every edit**

In `DoReplaceAll`, wrap the engine call:

```csharp
            _suppressModifiedNotifications = true;
            ReplaceAllResult r;
            try
            {
                r = _engine.ReplaceAll(SearchText, ReplaceText, CurrentOptions);
            }
            finally
            {
                _suppressModifiedNotifications = false;
            }
            ScheduleHighlight();
```

(keep the existing status reporting after it).

- [ ] **Step 4: Clear highlights when the panel is hidden**

In `SearchPanel`, override `OnVisibleChanged`:

```csharp
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
                ScheduleHighlight();
            else
                Guarded(ClearHighlights);
        }
```

and make `OnDockingPanelClosed` also clear:

```csharp
        protected override void OnDockingPanelClosed()
        {
            Guarded(ClearHighlights);
            Main.OnPanelClosedByUser();
        }
```

- [ ] **Step 5: Forward notifications from Main**

In `Main.OnNotification`, add these cases to the switch:

```csharp
            case (uint)NppMsg.NPPN_BUFFERACTIVATED:
                Npp.editor = new ScintillaGateway(PluginBase.GetCurrentScintilla());
                searchPanel?.OnBufferActivated(notification.Header.IdFrom);
                break;
            case (uint)NppMsg.NPPN_FILECLOSED:
                searchPanel?.OnFileClosed(notification.Header.IdFrom);
                break;
            case (uint)SciMsg.SCN_MODIFIED:
                searchPanel?.OnDocumentModified(notification.ModificationType);
                break;
```

In `PluginCleanUp`, before closing the panel, add `searchPanel.ClearHighlights();` inside the existing `if (searchPanel != null && !searchPanel.IsDisposed)` block (first statement in the block).

In `HideSearchPanel`, before `Npp.notepad.HideDockingForm(searchPanel);` add `searchPanel.ClearHighlights();`.

- [ ] **Step 6: Build and install**

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" src\NppSearchReplace\NppSearchReplace.csproj /p:Configuration=Release /p:Platform=x64 /v:minimal
```

Expected: `Build succeeded.` Install as before.

- [ ] **Step 7: Manual verification in Notepad++**

Using the same three-line document as Task 10:

1. Turn **Highlight** on, type `apple`: within half a second all three occurrences get an orange rounded box; status reads `3 matches`. Delete the last letter: boxes follow `appl`.
2. Type a regex `(` with Regex on: boxes disappear, status is red `Invalid regular expression`.
3. Open a second tab with other text, switch back and forth: the highlight is present only in tabs whose text matches, and switching to a tab that was highlighted earlier after turning Highlight off clears it.
4. Edit the document (type `apple` somewhere): the new occurrence is highlighted after you stop typing.
5. Turn Highlight off: boxes vanish. Hide the panel with its X: boxes vanish and the Plugins menu item is unchecked.
6. Paste a 100,000-line file with `apple` on every line and search `apple`: status reads `Highlight capped at 20000 matches`, the editor stays responsive.
7. Replace All with highlight on: after it finishes, the highlight reflects the new text once.

- [ ] **Step 8: Commit**

```powershell
git add -A
git commit -m "Add live match highlighting with debounce and buffer tracking

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 12: History dropdowns and persisted panel state

**Files:**
- Modify: `src/NppSearchReplace/Forms/SearchPanel.cs`
- Modify: `src/NppSearchReplace/Main.cs`

**Interfaces:**
- Consumes: `SearchHistory` (Task 3), `Main.PluginConfigDirectory`, `Settings.splitter_percent`.
- Produces: `SearchPanel.SaveState()` (history + options + splitter), called from `Main.PluginCleanUp` instead of `SaveOptionsToSettings`.

- [ ] **Step 1: Load history and add a dropdown button beside each box**

In `SearchPanel` add fields:

```csharp
        private static readonly string HistoryPath = Path.Combine(Main.PluginConfigDirectory, "history.xml");
        private SearchHistory _history = SearchHistory.Load(HistoryPath);
        private Button _searchHistoryButton, _replaceHistoryButton;
```

Replace `MakeBox()` and the two `Panel1/Panel2` lines in `BuildLayout()` with:

```csharp
            _searchBox = MakeBox();
            _replaceBox = MakeBox();
            _searchHistoryButton = MakeHistoryButton(() => _history.SearchTexts, _searchBox);
            _replaceHistoryButton = MakeHistoryButton(() => _history.ReplaceTexts, _replaceBox);
            _split.Panel1.Controls.Add(_searchBox);
            _split.Panel1.Controls.Add(_searchHistoryButton);
            _split.Panel2.Controls.Add(_replaceBox);
            _split.Panel2.Controls.Add(_replaceHistoryButton);
```

and add:

```csharp
        private Button MakeHistoryButton(Func<System.Collections.Generic.List<string>> entries, MultilineTextBox box)
        {
            var b = new Button { Text = "\u25BE", Dock = DockStyle.Right, Width = 18, TabStop = false, Margin = new Padding(0) };
            new ToolTip().SetToolTip(b, "Recent entries");
            b.Click += (s, e) =>
            {
                var menu = new ContextMenuStrip();
                foreach (string entry in entries())
                {
                    string captured = entry;
                    menu.Items.Add(SearchHistory.ToDisplayLine(captured, 60), null, (s2, e2) =>
                    {
                        box.Text = captured;
                        box.SelectAll();
                        box.Focus();
                    });
                }
                if (menu.Items.Count == 0)
                    menu.Items.Add(new ToolStripMenuItem("(no history)") { Enabled = false });
                menu.Show(b, new Point(0, b.Height));
            };
            return b;
        }
```

The text boxes are `Dock = Fill`, so add them to the panel **before** the buttons as shown, otherwise the button is covered. (WinForms docks later-added controls first; Fill must be added first.)

- [ ] **Step 2: Record history on every action**

At the top of `DoFindNext`, `DoFindPrevious`, `DoCount` (inside the `Guarded` lambda, first line) add:

```csharp
            _history.PushSearch(SearchText);
```

At the top of `DoReplace` and `DoReplaceAll` add:

```csharp
            _history.PushSearch(SearchText);
            _history.PushReplace(ReplaceText);
```

- [ ] **Step 3: Persist the splitter and everything else in one call**

Add to `SearchPanel` (plus a field `private bool _splitterApplied;`):

```csharp
        /// <summary>Applied once, the first time the panel has a real width; later drags are the user's.</summary>
        private void ApplySplitterFromSettings()
        {
            if (_splitterApplied || _split.Width <= 0)
                return;
            int percent = Math.Max(10, Math.Min(90, Main.settings.splitter_percent));
            _split.SplitterDistance = Math.Max(_split.Panel1MinSize, _split.Width * percent / 100);
            _splitterApplied = true;
        }

        public void SaveState()
        {
            SaveOptionsToSettings();
            if (_split.Width > 0)
                Main.settings.splitter_percent = Math.Max(10, Math.Min(90, _split.SplitterDistance * 100 / _split.Width));
            try
            {
                _history.Save(HistoryPath);
            }
            catch (Exception)
            {
                // history is a convenience; never block shutdown over it
            }
        }
```

Call `ApplySplitterFromSettings()` at the end of `OnVisibleChanged` when `Visible` is true (the panel has a real width only once shown):

```csharp
            if (Visible)
            {
                ApplySplitterFromSettings();
                ScheduleHighlight();
            }
```

In `Main.PluginCleanUp`, replace `searchPanel.SaveOptionsToSettings();` with `searchPanel.SaveState();`.

Also save when the user hides the panel so a crash later does not lose it: in `Main.HideSearchPanel` add `searchPanel.SaveState(); settings.SaveToIniFile();` before hiding, and in `OnPanelClosedByUser` add the same two lines guarded by `if (searchPanel != null && !searchPanel.IsDisposed)`.

- [ ] **Step 4: Build and install**

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" src\NppSearchReplace\NppSearchReplace.csproj /p:Configuration=Release /p:Platform=x64 /v:minimal
```

Expected: `Build succeeded.` Install as before.

- [ ] **Step 5: Manual verification in Notepad++**

1. Run three different searches, open the search box's dropdown: the three entries appear most-recent-first, multiline entries show `↵` instead of line breaks. Click one: it loads into the box.
2. Drag the splitter to about one third, toggle Regex and Case on, Highlight on, close Notepad++, reopen: the panel is open, the toggles and splitter position are as left, and the dropdowns still list the history.
3. Check `%AppData%\Notepad++\plugins\config\NppSearchReplace\history.xml` exists and `NppSearchReplace.ini` has `use_regex=True`, `splitter_percent=33` (or close).
4. Close the panel with its X, reopen Notepad++: the panel stays closed.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "Add history dropdowns and persist panel state

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 13: README, manual test checklist, and release build check

**Files:**
- Create: `README.md`
- Create: `docs/manual-test.md`

**Interfaces:** none.

- [ ] **Step 1: Write the README**

`README.md`:

```markdown
# NppSearchReplace

A Notepad++ plugin that docks a modeless search-and-replace panel at the bottom of the window,
in the spirit of EditPad Pro's search panel: multiline search and replacement boxes, Notepad++'s
own regex engine, and optional live highlighting of every match in the active document.

## Requirements

- Notepad++ 8.5.6 or later, 64-bit
- .NET Framework 4.8 (included in Windows 10 May 2019 Update and later)

## Install

1. Build (below) or download `NppSearchReplace.dll` and `NppSearchReplace.Core.dll`.
2. Create `C:\Program Files\Notepad++\plugins\NppSearchReplace\` and copy both DLLs into it.
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
folder is not writable, so copy by hand or run the build from an elevated prompt.

## Layout

- `src/NppSearchReplace.Core` — search engine, match enumeration, highlighter, history. No Notepad++ dependency; fully unit tested.
- `src/NppSearchReplace` — the plugin: Notepad++ entry points, docked WinForms panel, Scintilla adapter, settings. Based on [NppCSharpPluginPack](https://github.com/molsonkiko/NppCSharpPluginPack).
- `tests/NppSearchReplace.Core.Tests` — MSTest suite with an in-memory editor fake.
- `docs/manual-test.md` — checklist for behaviour that needs a live Notepad++.

## Not in this version

Multi-file search, search in selection, bookmarks, and EditPad's Free-spacing/Adapt/Line/Invert modes.
```

- [ ] **Step 2: Write the manual test checklist**

`docs/manual-test.md`:

```markdown
# Manual test checklist

Run after any change to the plugin project. Use a new document containing exactly:

```
apple banana apple
Banana cherry
café apple
```

## Panel and keys
- [ ] Ctrl+Alt+F opens the panel docked at the bottom; the search box has focus; Ctrl+Alt+F again hides it.
- [ ] Selecting a word in the editor then Ctrl+Alt+F pre-fills the search box with it. A multi-line selection does not.
- [ ] Enter inserts a newline in either box; Ctrl+Enter / Ctrl+Shift+Enter navigate; Tab switches boxes; Escape focuses the editor.
- [ ] Ctrl+C / Ctrl+V / Ctrl+A work inside the boxes.

## Searching
- [ ] `apple`, Loop on: three Ctrl+Enter presses select each occurrence, the fourth reports `Wrapped (line 1)`.
- [ ] Loop off at the last match: `No more matches`.
- [ ] Case off finds `banana` and `Banana`; Case on finds only `banana`.
- [ ] Words on: `app` finds nothing; `apple` finds three.
- [ ] `café apple` (non-ASCII) is found with the selection exactly covering it; `apple` on line 3 is selected without a one-byte shift.
- [ ] Multiline literal `apple⏎Banana` matches across the line break in both Windows and Unix EOL documents.
- [ ] Regex `(ap)(ple)` with replacement `$2$1`: Replace gives `pleap`; Replace All reports `Replaced N`; one Ctrl+Z undoes all.
- [ ] Regex `(` shows red `Invalid regular expression`, no message box.
- [ ] Dot enabled only with Regex on; Words enabled only with Regex off. Regex `a.b` with Dot on matches across a newline.
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
```

- [ ] **Step 3: Full clean build and test run**

```powershell
git clean -xdf -e packages -- src tests
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" src\NppSearchReplace\NppSearchReplace.csproj /t:Restore /v:minimal
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" src\NppSearchReplace\NppSearchReplace.csproj /p:Configuration=Release /p:Platform=x64 /v:minimal
dotnet test tests\NppSearchReplace.Core.Tests
```

Expected: `Build succeeded.` and `Passed! - Failed: 0, Passed: 73`.

- [ ] **Step 4: Run the whole manual checklist once**

Install the fresh build and go through `docs/manual-test.md` top to bottom. Every box must be checked; fix and re-run anything that fails.

- [ ] **Step 5: Commit**

```powershell
git add -A
git commit -m "Add README and manual test checklist

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

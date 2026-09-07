# Source77NW samples

Runnable, explained scenarios. Each sample is a small **real tool**
in its own `App.<n>/` folder -- a `csproj`, a `App.<n>.main.cs`
entry file, and a `README.md`
that walks through what the code shows -- built the way apps on this
library are built. Samples prefer several types cooperating on a
genuine job over spotlighting one type at a time; the per-type
reference lives in the XML docs and in `Docs/`.

The `App.` prefix is a source-organization convention only -- each
sample's `AssemblyName` is pinned in its csproj, so the actual
tool/exe identity (`Lookup.exe`, `FileList.exe`, etc.) is unchanged.

Samples are *pure samples*: no test framework, no assertions -- the
explanation is the product. `Samples77NW.slnx` in this folder covers
every sample (plus the library, so go-to-definition lands in
source), and building it is the freshness check that samples keep
compiling against the current library.

## Running

From the repo root:

```
dotnet build Samples/Samples77NW.slnx
dotnet run --project Samples/App.ListFiles -- . +H -all
```

Each sample targets one modern TFM (the library itself proves
net481/net8.0/net10.0 -- see `../Source/README.md`). `App.ListView`
is `net8.0-windows` (WinForms): it builds everywhere via
`EnableWindowsTargeting`, but runs on Windows only.

## Source code classification

Same tier scheme as the library (`../Source/README.md`), mirrored here as
a simplified, samples-scoped illustration -- not the full library
curation.

`Core.*` folders contain code with no UI dependency at all.
`UI.*` folders contain UI platform-dependent source code. Source
code folders do not have subfolders and carry no namespace of their
own -- everything here is `Samples77NW`, no inner namespaces.

- **`Core.Base/`** -- domain-common code used by every sample:
  `Config.cs` (the one shared domain-level file, linked into every
  sample exe via `Directory.Build.props`), plus the `<n>OP` shared
  leaf-op pools (`FileOP`, `EditOP`, `WinOP`, ...) --
  a resource of reusable ops any sample app can draw from, not a
  UI-binding document an app is driven by.
- **`Core.NT/`** -- reserved, empty pending any NT-dependent
  domain-common code arriving here.
- **`UI.Base/`** -- the `UI` static partial class's one identifying
  declaration (`UI.cs`, `public static partial class UI`), generic
  and toolkit-agnostic. There is no separate controls class; every
  wrapped control lives as a nested type under `UI` instead
  (`UI.Win`, `UI.BaseButton`).
  Every other UI `.cs` file, at any tier, declares only
  `static partial class UI` -- accessibility lives here alone.
- **`UI.NT.Base/`** -- NT-dependent generic UI addons, e.g.
  `UI.Fonts` zoom mechanics (depends on `System.Drawing`,
  Windows-bound in modern .NET).
- **`UI.NT.Forms/`** -- actual WinForms controls; a coder never
  touches a raw control directly, only a `UI` subclass
  (`UI.BaseButton : Button`, `UI.Win : Form`).

Future folders might follow the same pattern for other os/platform
combinations -- `UI.NT.WPF/`, `Core.NT.DEV/`, etc. -- same one-dot-cap
rule as the library tier (see `../Source/README.md`).

## Shared identity (Config.cs)

Every sample exe links one shared [`Config.cs`](Core.Base/Config.cs)
(via `Directory.Build.props`, living in the pure classification
folder `Core.Base/` alongside other domain-common .cs) -- THE single
domain-level file: the domain-wide `[assembly:]` attributes (Company,
Copyright, Contact, DomainName, DomainGuid, DeployDebug,
ExeInterface) plus the one `static Config` class holding domain boot,
the Folders and Settings stores, Permit, and the Assets suppliers
behind `ResCode`. Nothing domain-level lives anywhere else. Every
sample boots through `Config.Initialize()` first in `Main()`.
Each sample's own entry file still declares its per-exe attributes
only: `AssemblyProduct`, `ExeCodeName`, `Guid`.

**Build stamp:** `AssemblyVersion` is not a hand-written date
string per exe -- this folder's `Directory.Build.props` auto-stamps it at
every build (`yyyy.MM.dd.HHmm`, matching `Exe.cs`'s own fallback
format), so it always reflects the actual build time. A project that
skips the stamp (GenerateAssemblyInfo disabled entirely, etc.) falls
back to `0.0.0.0`, which `Exe.cs` itself replaces with a version
derived from the exe file's last-write time -- never a real `0.0.0.0`
in practice.

## The samples

| Sample | A real tool that... | Featuring |
|--------|---------------------|-----------|
| [`App.ListFiles`](App.ListFiles/) | walks a folder tree and writes a filtered file listing as TSV, with include/exclude attribute filters and a top-level/recurse switch | `Exe` (identity), `Issue` (Kind dispatch), `Chars` (zero-alloc parsing), `ItemStack` (traversal worklist), `FileAttr`, `ExeLock`, `FS`, `ExitId` |
| [`App.FileList`](App.FileList/) | lists a file or folder's properties (name, size, dates, Crc32, ...) to csv/tsv/lsv/md, with caller-chosen columns and order | `EnumCodes` (property selection by name/token/caption/index), `FileAttr`, `Crc32` (reused ref-buffer), `ItemStack`, `Config` (opt-in output folder, `FolderId.Results`) |
| [`App.Lookup`](App.Lookup/) | loads an LSV glossary once and answers key queries by binary search over Chars views -- zero strings from file to console | `LsvDoc`/`LsvRecord`, `Chars` (views, CompareTo, Write), `ItemStack` (Comparer, Sort, BinarySearchNearest), `Issue`, `Exe` |
| [`App.ListView`](App.ListView/) | a WinForms front-end that builds a `FileList.exe` command line from UI controls, shells out to it, and loads the result into a real `ListView` control (Windows-only TFM) | `Csv.Reader.Reader` (parsing FileList's own csv/tsv output back), `LsvRecord` (parsing its lsv output by field name), an exe-owned `EnumVals`/LSV settings store (a real `Set()`+persist -- the last-used folder), `Issue`/`ExitId` (via `MessageBox`) |

## Find a type

| Type | See |
|------|-----|
| `Issue`, `IssueKind`, `ExitId` | `App.ListFiles` -- the error regime end to end |
| `Chars` | `App.ListFiles` -- command-line plucking; `App.Lookup` -- views as data: compare, sort, and Write without ToString |
| `ItemStack<T>` | `App.ListFiles` -- the traversal worklist; `App.Lookup` -- Comparer, `Sort`, `BinarySearchNearest` |
| `FileAttr` | `App.ListFiles` -- format, parse, and include/exclude filter; `App.FileList` -- same filters, plus an `Attr` output column |
| `ExeLock` | `App.ListFiles` -- single instance via a `KindId.File` lock |
| `Exe` | `App.ListFiles` -- EntryAssembly identity, `GetCommandLineParams`, `ExeNameOnly`, guarded `IsAdmin` |
| `FS` | `App.ListFiles` -- `ValidFolderPath_or_null`, `AsFileIssue` |
| `Crc32` | `App.FileList` -- one 64K ref-buffer reused across a whole listing, computed only for a selected column |
| `Config` | `App.FileList` -- opt-in output folder via `Config.FolderPath(Config.FolderId.Results)`; `App.ListView` -- its own exe-scoped `EnumVals`/LSV settings store, an actual `Set()`+persist of the last-used folder across runs |
| `LsvDoc`, `LsvRecord` | `App.Lookup` -- one text, records as views, fields on demand; `App.FileList` uses `LsvRecord`'s markers for its `lsv` output format; `App.ListView` parses that same lsv output back by field name |
| `EnumCodes`, `ResCode`, `EnumInfoAttribute` | `App.FileList` -- property selection/order by name, token, caption, or index |
| `Csv.Reader.Reader` | `App.ListView` -- parses `FileList`'s own csv/tsv output back (quoting and separator auto-detect included) |

Copyright (c) GDFrank - 77NW.net. All rights reserved.

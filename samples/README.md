# Source77NW samples

Runnable, explained scenarios. Each sample is a small **real tool**
in its own folder — a `csproj`, a `Program.cs`, and a `README.md`
that walks through what the code shows — built the way apps on this
library are built. Samples prefer several types cooperating on a
genuine job over spotlighting one type at a time; the per-type
reference lives in the XML docs and in `docs/`.

Samples are *pure samples*: no test framework, no assertions — the
explanation is the product. `SAMPLES.slnx` at the repo root covers
every sample (plus the library, so go-to-definition lands in
source), and building it is the freshness check that samples keep
compiling against the current library.

## Running

From the repo root:

```
dotnet build SAMPLES.slnx
dotnet run --project samples/Inspect -- . H -d 2
```

Each sample targets one modern TFM (the library itself proves
net481/net8.0/net10.0 — see `src/LIBS.csproj`). The `EnumCodes`
sample is `net8.0-windows` (WinForms): it builds everywhere via
`EnableWindowsTargeting`, but runs on Windows only.

## The samples

| Sample | A real tool that... | Featuring |
|--------|---------------------|-----------|
| [`Inspect`](Inspect/) | walks a folder tree and reports attribute bits, with filter, depth limit, and a depth-first/breadth-first switch | `Issue` (Kind dispatch), `Chars` (zero-alloc parsing), `ItemStack` (one structure, both walk orders), `FileAttr`/`FileAttrX`, `ExeLock`, `Exe`, `FS`, `ExitId` |
| [`Lookup`](Lookup/) | loads an LSV glossary once and answers key queries by binary search over Chars views — zero strings from file to console | `LsvDoc`/`LsvRecord`, `Chars` (views, CompareTo, Write), `ItemStack` (Comparer, Sort, BinarySearchNearest), `Issue`, `Exe` |
| [`EnumCodes`](EnumCodes/) | a WinForms app whose whole UI — menus, toggles, a radio group, buttons, shortcuts, icons, dispatch, persisted state — is declared by one enum (Windows-only TFM) | `EnumCodes`, `EnumCodesAttribute`/`EnumInfoAttribute`, `ResCode`, `CodeValId`/`CodeDefId`/`ResKind`, `BytesReader` (stream providers), `Chars` (VBAR fields, ctrl parsing) |

## Find a type

| Type | See |
|------|-----|
| `Issue`, `IssueKind`, `ExitId` | `Inspect` — the error regime end to end |
| `Chars` | `Inspect` — command-line plucking; `Lookup` — views as data: compare, sort, and Write without ToString |
| `ItemStack<T>` | `Inspect` — the traversal worklist, `Pop()` vs `Pluck()`; `Lookup` — Comparer, `Sort`, `BinarySearchNearest` |
| `FileAttr`, `FileAttrX` | `Inspect` — format, parse, and filter |
| `ExeLock` | `Inspect` — single instance via a `KindId.File` lock |
| `Exe` | `Inspect` — `GetCommandLineParams`, `ExeNameOnly`, guarded `IsAdmin` |
| `FS` | `Inspect` — `ValidFolderPath_or_null`, `AsFileIssue` |
| `LsvDoc`, `LsvRecord` | `Lookup` — one text, records as views, fields on demand |
| `EnumCodes`, `ResCode`, `EnumInfoAttribute` | `EnumCodes` — one enum as the whole UI: truth table, index-aligned slots, ByteCode persistence, icon/ctrl fields |

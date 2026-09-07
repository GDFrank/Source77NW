# ListView

A WinForms front-end for [`FileList`](../App.FileList/) - not a
reimplementation of file listing, a *driver*: it builds a `FileList.exe`
command line from UI controls (folder, filters, which properties, which
format), shells out to it, then loads whatever it wrote into a real
`System.Windows.Forms.ListView` control - hence the name.

The point of the sample is showing `EnumCodes`-style property data
(here, just names and order - no Text/Tags needed) driving a UI without
the UI being told HOW to render it: a checkbox list is one legitimate
choice among many (a listbox, grouped columns, whatever) - the
`EnumCodes` design principle that a code is just a code; how it
renders is the UI's decision.

## Running

Build `Samples77NW.slnx` first (ListView's own build copies `FileList.exe`
alongside it - see `App.ListView.csproj`'s `CopyFileListOutput` target),
then run `ListView.exe` directly (Windows only - `net8.0-windows`,
`EnableWindowsTargeting` keeps it buildable everywhere).

1. Pick or browse to a folder.
2. Optionally set include/exclude ARHSCE filters and recurse.
3. Check which properties you want, or click a preset (Default / Excel
   - the Excel preset's "Folder last" ordering is honored even though
   the checklist itself is declaration-order).
4. Pick a format (csv/tsv/lsv/md - all four round-trip back into the
   grid; the file is also kept on disk, under the samples-domain
   Results folder).
5. Click **List**.

The last folder you used persists across runs via ListView's own
exe-scoped settings store (see "Featuring" below) - close and reopen
ListView and it's still there.

## Coupling note

`FileListPropertyId`'s names are duplicated by hand in `MainForm.cs`
(`_PropertyNames`) rather than referenced from `FileList`'s assembly:
ListView shells out to `FileList.exe` as a subprocess (see
`App.ListView.csproj` - the `ProjectReference` to `App.FileList.csproj` has
`ReferenceOutputAssembly="false"`, a build-order/copy dependency only),
so there's no assembly reference to share the enum through, and
`FileListPropertyId` is `internal` to `FileList` regardless. If a
property is ever added/renamed in `FileList`, `MainForm.cs`'s
`_PropertyNames`/`_PresetExcel` need a matching hand-edit.

## Featuring

`EnumCodes`-style data driving UI construction (property list, presets)
without EnumCodes itself being present as a type here - a plain
`string[]` plays the same "names + order" role a small
`FileListPropertyId`-typed EnumCodes lookup would, deliberately kept
this simple for a UI that only ever displays column names, never looks
up their bits/flags. An exe-owned `EnumVals`/LSV settings store (a
real `Set()`+persist: the last-used folder survives a restart - a
fuller exercise than `FileList`'s Results-folder `Value()`-only use of
`Config`).
`Csv.Reader.Reader` (parses FileList's own csv/tsv output back, quoting
and all). `LsvRecord` (parses the lsv output's per-file records by field
name). `Issue`/`ExitId` (the same error regime as every console sample,
surfaced via `MessageBox` instead of stderr).

Copyright (c) GDFrank - 77NW.net. All rights reserved.

# FileList

A real tool: property-selectable file/folder listing to **csv, tsv, lsv,
or md**. A folder target lists its files (top-level only by default,
`-all` recurses); a file target reports just that one file.

Which columns appear, and in what order, is caller-chosen - not fixed
column layout like `ListFiles`. Columns are `FileListPropertyId`, an
[`EnumCodes`](../../Source/Core.Base/EnumCodes.cs)-registered enum, selectable
by name, token, caption, *or* numeric index.

## Running

```
dotnet run --project Samples/App.FileList -- .
dotnet run --project Samples/App.FileList -- . -all -preset:excel -format:tsv
dotnet run --project Samples/App.FileList -- somefile.txt -props:Name,Size,Crc32
dotnet run --project Samples/App.FileList -- . -out -open
```

## Switches

| Switch | Meaning |
|---|---|
| `path` | file or folder (default: current directory) |
| `+<attrs>` / `-<attrs>` | ARHSCE letters ([`FileAttr`](../../Source/Core.Base/FileAttr.cs)) - include/exclude, repeatable, contradictions permitted (exclude wins) |
| `-all` | recurse inner folders (folder targets only) |
| `-out[:file]` | write to a file instead of stdout - bare `-out` uses a timestamped file under the samples-domain Results folder (opt-in output only, via `Config.FolderPath(Config.FolderId.Results)`) |
| `-open` | open the output file when done (with `-out`) |
| `-props:a,b,c` | explicit column list - name, token, caption, or index, in the order given |
| `-preset:excel` | Name, Ext, Attr, Size, Updated, Created, Folder (no Crc32 by default - add it explicitly with `-props` if wanted) |
| `-preset:default` | every property, declaration order (the default) |
| `-format:csv\|tsv\|lsv\|md` | output format (default: csv) |

## Properties (`FileListPropertyId`)

`NameExt`, `Ext`, `Name`, `Path`, `Folder`, `Url`, `Attr`, `Size`,
`SizeText` (human-readable KB/MB/GB/TB/PB), `Updated`, `Created`,
`Crc32` (hashed only for files where the column is actually selected -
never computed speculatively).

## Featuring

`EnumCodes` (property selection/order as a truth table, looked up by
name/token/caption/index), `FileAttr` (ARHSCE include/exclude filters),
`Crc32` (one 64K buffer reused across an entire listing - see
`Crc32.TryCompute`'s ref-buffer parameter), `ItemStack` (the folder
traversal worklist), `Issue`/`ExitId` (the same error regime as every
other sample), `Config` (opt-in output folder via
`Config.FolderPath(Config.FolderId.Results)`).

Copyright (c) GDFrank - 77NW.net. All rights reserved.

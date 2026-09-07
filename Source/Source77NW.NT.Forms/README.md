# Source77NW.NT.Forms

Project folder for **`Source77NW.NT.Forms.dll`** -- the WinForms flavor of the library:
`Core.Base`'s and `Core.NT`'s sources compiled in, plus the `UI.Base`, `UI.NT.Base`, and
`UI.NT.Forms` tiers. An exe links exactly *one* Source77NW DLL -- never this one together with
`Source77NW.Core.Base.dll` or `Source77NW.Core.NT.dll`; every type they hold already exists in
here too.

Pure-classification layout ([building.md](../../Docs/building.md)): this folder holds only the
csproj -- no source of its own. Sources live in the classification folders
[`../Core.Base/`](../Core.Base), [`../Core.NT/`](../Core.NT), [`../UI.Base/`](../UI.Base),
[`../UI.NT.Base/`](../UI.NT.Base), and [`../UI.NT.Forms/`](../UI.NT.Forms), pulled in via linked
`Compile` globs. The tier naming scheme is in [Source/README.md](../README.md). A sibling
`Source77NW.NT.WPF/` project folder is reserved for a future WPF flavor, linking `Core.Base/` +
`Core.NT/` + a future `UI.NT.WPF/` tier the same way this project links `UI.NT.Forms/`.

Targets `net481;net8.0-windows;net10.0-windows` -- the `-windows` suffix on the modern TFMs
exists because the registry / P/Invoke sources are compile-time Windows-bound (`CA1416` would
fire on every call site otherwise); `net481` needs no suffix.

One static class, `NT`, holds the isolated NT system functions: direct members (e.g.
`NT.StoppedProcess`) and nested static classes grouping a subject (`NT.Mgmt`, `NT.MediaDrive`,
`NT.RegVals`, `NT.Recycle`, `NT.PutInfoSys`) -- nesting keeps the tier clustered in IDE lists
without a top-level name per subject. All of it lives in `Core.NT/`.

The `UI` static partial class follows the same shape across the UI tiers: one identifying
declaration in `UI.Base/`, addons in `UI.NT.Base/` and `UI.NT.Forms/`, every contributing file
elsewhere declaring only `static partial class UI`. The library-level UI tiers are reserved today;
the working illustration of the pattern is in [`Samples/`](../../Samples/README.md).

Copyright (c) GDFrank - 77NW.net. All rights reserved.

# Source77NW.Core.NT

Project folder for **`Source77NW.Core.NT.dll`** -- the Windows-dependent flavor of the Core
library: `Core.Base`'s sources compiled in, plus `Core.NT`'s NT-bound modules (registry, system
management, shell...). No UI.

Pure-classification layout ([building.md](../../Docs/building.md)): this folder holds only the
csproj -- no source of its own. Sources live in the classification folders
[`../Core.Base/`](../Core.Base) and [`../Core.NT/`](../Core.NT), pulled in via linked `Compile`
globs.

Targets `net481;net8.0-windows;net10.0-windows` -- the `-windows` suffix on the modern TFMs exists
because the registry / P/Invoke sources are compile-time Windows-bound (`CA1416` would fire on
every call site otherwise); `net481` needs no suffix. Never link this DLL together with
`Source77NW.Core.Base.dll` (or a further flavor) -- every Core type already exists in here too.

Copyright (c) GDFrank - 77NW.net. All rights reserved.

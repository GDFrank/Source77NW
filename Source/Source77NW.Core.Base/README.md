# Source77NW.Core.Base

Project folder for **`Source77NW.Core.Base.dll`** -- the foundation library: assembly-role-agnostic
modules, no UI or OS dependencies.

Pure-classification layout ([building.md](../../Docs/building.md)): this folder holds only the
csproj -- no source of its own. Its sources live in the classification folder
[`../Core.Base/`](../Core.Base), pulled in via a linked `Compile` glob. The csproj is the honest
manifest of exactly that.

Multi-targets `net481;net8.0;net10.0`. Flavor projects (`Source77NW.Core.NT`,
`Source77NW.NT.Forms`, ...) compile `Core.Base/`'s sources **in** rather than referencing this DLL --
an exe links exactly *one* Source77NW DLL for its context; never this one and a flavor together.

Copyright (c) GDFrank - 77NW.net. All rights reserved.

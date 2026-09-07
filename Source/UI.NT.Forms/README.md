# UI.NT.Forms/

Reserved pure classification folder (plain `.cs`, no csproj, no subfolders) for WinForms-based
UI controls at the library level -- the `UI` static partial class's Forms tier -- once any
library-grade content arrives here. Today's `UI.BaseButton`/`UI.Win` wrapped-control pattern
lives only as a simplified illustration under [`Samples/UI.NT.Forms/`](../../Samples/README.md);
this is where a full curation would land.

Wrapped controls are nested types under `UI` (`UI.BaseButton`, `UI.Win`), not a separate
controls class -- a coder never touches a raw Forms control directly. Linked in by
[`Source77NW.NT.Forms/`](../Source77NW.NT.Forms/README.md) (`Core.Base/` + `Core.NT/` + `UI.Base/`
+ `UI.NT.Base/` + this tier). Full tier convention: [Source/README.md](../README.md).

Copyright (c) GDFrank - 77NW.net. All rights reserved.

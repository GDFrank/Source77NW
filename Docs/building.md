# Building

How the code is organized into projects and built -- the *operational* companion to
[coding.md](coding.md) (writing the source) and [releasing.md](releasing.md) (shipping it).

## Layout: pure classification

Two namespace realms sit at repo root -- `Source/` (libraries) and `Samples/` (samples) --
each with its own `.slnx` named for the namespace (`Source77NW.slnx` / `Samples77NW.slnx`). Under a realm, every folder is **either** pure
classification content **or** a project folder, never both:

- **Pure folder** -- only `.cs` files, no csproj, no subfolders. Compiles into whichever
  project(s) link it via a glob `Compile Include`.
- **Project folder** -- only its `.csproj` (plus a `README.md` where one exists), no source of its
  own. The csproj is a short, honest manifest of exactly which classification folders it's built
  from.

Reclassifying a module = move the file -- no project edits, when the consuming csproj globs the
whole folder. The tier naming scheme itself lives in [Source77NW's README](../Source/README.md).

## One namespace, one DLL

All library sources declare the single namespace `Source77NW` -- no sub-namespaces. Flavor
projects compile the Core sources **in** via linked globs rather than referencing the Core DLL:

```xml
<Compile Include="../Core.Base/**\*.cs" LinkBase="Core.Base" />
```

So an exe links exactly **one** Source77NW DLL for its context. Never link the Core DLL and a
flavor DLL together -- every Core type would exist in both.

## Targets and the C# 7.3 baseline

Libraries multi-target `net481;net8.0;net10.0` (NT flavors use MSBuild's `-windows` suffix
on the modern TFMs -- the TFM literal is MSBuild's word; NT is this codebase's word for the
same boundary). Because net481 is in the set, **all source compiles under C# 7.3** -- which
rules out, among others:

- nullable reference types, switch expressions, ranges/indices (`[..]`, `[^1]`)
- `using` declarations (statements only), default interface implementations
- struct field initializers, async streams
- verbatim string content must start on the same line as the `@"`

Per-TFM differences are handled with `#if` blocks (e.g. `NETCOREAPP`) and conditional
`<Reference>` groups in the csproj -- see any flavor csproj's `net481` ItemGroup for the shape.

## Compile flags

Behavior that differs by build is compiled in or out via `DefineConstants` -- never runtime
dispatch. The interface split is structural; the rest are *optional* flags -- defined here so
every project that uses one uses the same name, but any given project may use none of them:

- **`CONSOLE` / `SERVICE` / (neither = UI)** -- the *interface* split. Interface-dependent code
  (how `Critical`/`Log` sinks present, how the permission prompt asks) lives behind
  `#if CONSOLE` / `#if SERVICE` / `#else` blocks in `Config.cs`.
- **`DEV`** -- an in-app development environment: sections or whole modules built to rework the
  app itself, with full access to the exe's own types -- generating tables, rewriting a csproj,
  whatever the job needs. Not `DEBUG`: a DEV build can ship in an `ALPHA` release for runtime
  creation while users work (e.g. dynamically building out text for a new language); `BETA` and
  production builds compile it away entirely.
- **`ALPHA` / `BETA`** -- build-status flags read by `Exe.cs`, stamped into the build's version
  tag so any shipped binary visibly declares which testing tier it's in. Bound to a sequential
  per-app build number that increments through time as builds are produced -- a distinct,
  app-level identifier on its own axis, not the repo version (see "Versions" below; in
  particular not the repo's `.Build` field either, which counts repo *content refreshes*, not
  app builds). Tier flags never appear in release tags. Neither flag is set for a production
  build.
- **`WIP`** -- lets changed code coexist with the code it replaces, switchable per build:
  `#if WIP` *new* `#else` *current valid path* `#endif`. Purely *new* code needs no `#else` --
  absent is already a valid build. WIP is also a **project-level** idea, not just a flag: each
  realm (`Source/`, `Samples/`) keeps two `.slnx` files -- the prod one (`Source77NW.slnx`,
  `Samples77NW.slnx`) is the shipping set, and a `.WIP.slnx` twin (`Source77NW.WIP.slnx`,
  `Samples77NW.WIP.slnx`) is a strict superset holding every project, ready or not, for local
  build/test. A project born only in the WIP twin is excluded from the sync that refreshes the
  public repo *and* from the release zips -- the diff between the two `.slnx` files *is* the
  exclusion list, computed at sync time rather than hand-declared, so there's nothing to keep in
  sync by hand. It graduates by adding the same `<Project>` entry to the prod `.slnx` too. Code-
  level WIP rides inside a shipping project; project-level WIP keeps the whole project out of the
  shipping picture until it earns its way in. The shipping side of that story is
  [releasing.md](releasing.md).
- **`TEST` / `DEMO`** -- may manage root-level items in both `Config.cs` and `*.main.cs`. The
  DEMO pattern: build the *real* app with the flag making it behave as a demo -- reviewer
  windows to toggle options, dummy data in place of live sources, output confined to a
  throwaway demo folder. Compiling without the flag **is** the release; the demo is not a
  fork -- the DEMO build actively creates the real thing alongside it.
  **Asset-loading scaffold:** wherever a sample needs an asset/resource and the
  real production loading mechanism doesn't exist yet (e.g. `Config.cs`'s `assetMgr`/`ResCode`
  stream suppliers, still stubbed as of this writing), `#if DEMO` loads a known sample asset
  directly (or generates one dynamically) so the demo runs and teaches *as if* fully
  implemented; `#else` is where the real mechanism's call goes once it exists. This keeps every
  sample buildable and runnable today without waiting on domain-level plumbing decisions, and
  makes "this is demo scaffolding" vs "this is the real path" visible at a glance in the source
  rather than needing a comment to explain it. Like `ALPHA`/`BETA`, `DEMO` is stamped into the
  build's visible version tag (`Exe.DeployDemo`/`Exe.IsDemo`, folded into
  `Exe.ExeVersion_withDeployDebugTag` alongside `DeployStage`/`DeployDebug`) so a demo build
  visibly identifies itself in a title bar, About page, or log header.
- **`OutputType`** -- the samples tree defaults to `Exe` (console) via its
  `Directory.Build.props`; any *windowed* sample overrides `<OutputType>WinExe</OutputType>` in
  its own csproj (the project body evaluates after imported props, so the override just wins).

## Assembly info

No loose `AssemblyInfo.cs` files. The `[assembly:]` story splits by tier:

- **Domain-wide** -- one block in `Config.cs` (linked into every exe in the domain).
- **Per-exe** -- in that exe's `<name>.main.cs`, alongside `Main()` -- the one place an exe's
  initial context is established.

## Embedded resources (ResPack / ResCode)

Binary assets don't ship as loose files or `.resx` -- they ship as **`.pack` bundles embedded as
assembly manifest resources** (`*.RES.*.pack`):

- **`ResPack.Builder`** (development-time; an inner class of `ResPack` by design -- it sees the
  pack format's privates without exposing them -- and shipped in the library because consumers
  need it to build their own packs; release apps just never call it) writes a pack from an
  assets folder: header, concatenated asset streams, sorted manifest tail.
- **`ResPack.Loaded(assembly, out Issue)`** at boot scans the assembly's manifest resources,
  indexes every pack, and registers stream/value suppliers with `ResCode`.
- **`ResCode`** is the app-side handle -- a tiny struct (a cache index + a code index, **no stored
  text**). Captions, tooltips, and assets resolve *by code* through the registered suppliers. The
  payoff: a language ("lingo") switch needs nothing more than walking the visible controls and
  re-pulling their captions -- no per-control language state, no cache to invalidate.
- **`EnumCodes`** supplies the code tables underneath -- an enum viewed as a validated truth table
  of codes, with per-member `Text`/`Tags` metadata carrying caption and association data.

## Versions

`Directory.Version.props` at repo root is the **single source of truth** for
`Major`/`Minor`/`Patch`/`Build`, imported explicitly by the root `Directory.Build.props` (unlike
`Directory.Build.props` itself, a `.Version.` file does **not** auto-import -- the `<Import>` is
deliberate). Every library csproj derives from it:

```xml
<AssemblyVersion>$(Major).0.0.0</AssemblyVersion>                <!-- binding contract -->
<Version>$(Major).$(Minor).$(Patch).$(Build)</Version>           <!-- what consumers see -->
<InformationalVersion>$(Version)+$(BuildStamp)</InformationalVersion>
```

The version is 4-part -- `M.m.p.b`. `Major.Minor.Patch` is the **release**; `Build` counts
**content refreshes** shipped to the public repo since that release (a release itself is `.0`,
each refresh increments it -- `1.2.3.4` = "release 1.2.3 plus 4 refreshes"). Release tags and
zip names stay 3-part `vM.m.p` -- releases are `M.m.p` events; see
[releasing.md](releasing.md) "Versions between releases."

`AssemblyVersion` pins to `Major.0.0.0` on purpose: minor/patch releases never force dependents
to rebind. That's also why **major** is the bump that matters for anything breaking -- see
[releasing.md](releasing.md) for how the bump gets declared and consumed.

---

*Companion to [coding.md](coding.md) and [releasing.md](releasing.md).*

Copyright (c) GDFrank - 77NW.net. All rights reserved.

# Coding

Source code naming and coding conventions used in this repo -- the *writing the source* third of a
trio: [building.md](building.md) covers projects, compile flags, and versions; [releasing.md](releasing.md)
covers how work stages and ships.

## Design principles

A few rules of thumb that explain *why* the conventions below look the way they do:

- **KISS as a recurring drive** -- eliminate complex dialogs, extra processing layers, per-class
  assignments; prefer designs where the simple case needs no ceremony. Conservative feature
  adoption: less is more.
- **Zero-allocation where possible** -- allocation is a decision, not a habit; minimal heap
  motion when parsing or producing results.
- **Soft semantics for data** (HTML-like) -- absence/undefined is a *first-class state*, not an
  error. Enums carry `None = 0` with operational meaning; `null`/default reads as "undefined," a
  valid value; operations softly do or don't do (the `Try*`/`Did*`/`Got*` surface below).
- **Loud vs soft splits by kind, not by mood** -- an invalid *operation* (contract misuse, the
  program is wrong) throws via `Issue`; absent/undefined *data* (domain state) gets defined
  behavior, never a throw.
- **Bare-metal cores, wrapped exposure** -- core types (`ItemStack`, `TextBuilder`) are
  single-threaded, no locks, no defensive ceremony; misuse fails loud. Public/managed exposure
  comes from agent wrappers via composition (cores are `sealed`; wrap, don't derive) -- that's
  where argument validation and `Try*` softening live, while the core contract stays sharp.
- **`sealed` by default** -- a class stays sealed until extension has demonstrated meaning and
  usefulness. It's both a contract statement (no unaudited subclass promises) and a JIT enabler.
  Unsealed + virtual is a *deliberate* extension surface, never leftover openness.
- **Explicit, readable mnemonics** -- parameter and method names that explain themselves (the
  naming sections below are this principle applied).

## Private Methods and Properties

Private methods and properties are always `_`-prefixed. Public exposure, when needed, is a thin
same-named wrapper (minus the underscore) over the private member -- the underscore alone marks
"this is the real implementation," and dropping it marks "this is the public face of it":

```csharp
private object _FooItem;
public  object FooItem => _FooItem;

private bool _FooMethod(...) { ... }
public  bool  FooMethod => _FooMethod(...);
public  bool  FooMethod(...) { /* curate and/or guard */ return _FooMethod(...); }
```

The public member may be a pass-through to the private one, or it may curate a parameter (say,
convert a code to an index) or act as a guard in front of the private member.

## Method Naming

- **`Try*` / `Did*` / `Got*`** -- `bool` + `out Issue returnIssue`: the no-throw pattern (see
  `Issue.cs`). Failure is expected, not exceptional -- test the `bool`, discard the `Issue`
  with `out _` when the *why* doesn't matter.
- **`*_or_null`** -- same shape, one level up: the value, or `null`, alongside its own
  `out Issue returnIssue`. See example below (Parameter Naming).
- **`*_or_default`** -- the value, or a defined default (not `null`), for callers who'd rather
  always get *something* back.
- **`Is*` / `Has*`** -- properties that read as assertions (`IsAdmin`, `HasContent`).

The name alone says how to handle failure -- no doc lookup needed.

## Parameter Naming

The naming visually marks the parameter as `the`/`out`/`ref` at a glance, and often carries
"tip" text describing the *what* right inside the symbol name.

- **`theFoo`** -- a primary input parameter to be used or worked on.
- **`returnFoo`** / **`outFoo`** -- an output (`out`) parameter.
- **`updateFoo`** / **`refFoo`** -- a `ref` parameter.
- Simple names are used as-is where a full convention adds nothing, e.g. `ignoreCase`.
- Full-sentence parameter names spell out the contract inline, e.g.
  `thePath_or_FileUrl_or_NullOrWhiteSpace` -- the accepted shapes are legible from the signature
  alone, no separate doc lookup needed.

```csharp
// FS.ValidFolderPath_or_null - full signature, all params:
public static string ValidFolderPath_or_null(
    string thePath_or_FileUrl_or_NullOrWhiteSpace,
    out Issue returnIssue,
    bool issue_if_is_file = true)

// caller who only cares about success/failure - discard the Issue:
string sPath = FS.ValidFolderPath_or_null(sInput, out _);

// caller who wants to know why, on failure:
string sPath = FS.ValidFolderPath_or_null(sInput, out Issue xIssue);
if (sPath == null) { /* xIssue explains why */ }
```

## Local Var Naming

A single lowercase-letter prefix by type, so a local's kind is visible without a declaration
lookup:

| prefix | type |
|---|---|
| `b` | bool |
| `c` | char |
| `d` | double |
| `i` | int / index |
| `s` | string |
| `v` | struct |
| `x` | ref |
| `y` | byte |

So a local reads as `bFound`, `iIndex`, `sPath`, `vSlot`, `xItem`, `yFlags`.

## Enumeration Pattern

Cursor-based walks, not enumerators or collections built up front -- the same shape appears
across `Chars`, `LsvRecord`, `EnumCodes`, and elsewhere:

```csharp
int cursor = 0;
while (theSource.GotNext(ref cursor, out var theValue))
{
    // ...
}
```

`Got*`/`Did*`/`Try*` naming applies here too -- a `false` return just means "nothing left,"
never an exception.

## Loop Style

Pre-increment `while`, not post-increment `for`, for forward iteration:

```csharp
int i = -1;
while (++i < count)
{
    // ...
}
```

Fewer symbols, one less instruction than the `for` equivalent.

## Module Naming

Applies to library `*.cs` only (one `.cs` file is one "module"). A bare `<name>.cs` is just the
normal case -- the file for class/struct `<name>` -- and doesn't need its own rule. Two real
patterns beyond that:

- **`<name>-<suffix>.cs`** -- additional functions/members of the `<name>` class, split into a
  second file for size/organization; not a nested type, just the same class continued.
- **`<name1>.<name2>.cs`** -- a nested class/struct `<name2>` declared inside `<name1>`.

## XML doc comments (`///`)

Industry-standard XML doc tags only -- they render correctly everywhere (tooltips, metadata view,
doc tooling) and surprise no reader. Readability comes from terse voice and lean coverage, never
custom layout. `CS1591` is live repo-wide: every public member carries a `///` summary
(per-file exemptions in `.editorconfig` for caption-name enums and ASCII-const files).

**Coverage** -- every public/internal type gets `<summary>`, plus `<remarks>` when there's
rationale, invariants, or gotchas worth keeping (remarks are the old `/* */` header's successor;
*history* goes to git, never `///`). Private members get `///` only when the contract is
non-obvious -- don't pad.

**Contract by signature** -- documented once, in `Issue.cs`'s remarks, never restated per member:

```
<T>  F(..., out Issue returnIssue)   never throws; result valid when returnIssue is null
bool F(..., out Issue returnIssue)   never throws; true -> issue null, false -> issue set
Try* / Did* / Got*                   never throws (the name says it)
everything else                      fails LOUD via Issue on contract misuse
```

Consequences (the anti-bloat -- these are *subtractive* rules):

- No `<exception>` tags on conforming members -- the global contract already says it.
- No `<param>` for vocabulary-carrying names (`the*`/`return*`/`update*`... -- this doc owns the
  vocab); `<param>` only when the name can't carry the semantics (units, ranges, ownership), and
  then all-or-none per member.
- `void` members never get `<returns>`; others fold return meaning into the summary's closing
  sentences ("False when already initialized. Empty when the folder is absent.").
- **"Never throws."** -- the standardized closing sentence for no-throw members *without* signature
  markers (pure computations, lookups, formatters). Never written on `out Issue`/`Try*` members --
  their signature already says it.

**Summary style** -- always one paragraph: contiguous wrapped lines, no blank `///` lines, no
lists or structure (the moment content wants a second paragraph, it's remarks material). Terse,
front-loaded, dash-connected; no "This method..."/"Gets or sets..." boilerplate. 1-2 lines typical.

**Mechanics** -- pure ASCII, `->` not arrow glyphs; `&lt;` `&amp;` escapes for literal `<`/`&`;
`<c>...</c>` for inline code tokens; HTML-ish tags (`<br/>`, `<b>`) banned (VS-only rendering);
`<see cref>` at type level where navigation pays, bare names in member prose; every cref verified
against the real surface, never invented.

## Changelog Markers

A permanent, in-source changelog, separate from git history and from XML doc comments -- visible
to anyone reading the file, grep-able across the whole codebase, and never stripped or rewritten
once stamped.

**Four classes** -- the marker word says what kind of change it is, and with that which version
bump it implies. Markers propose; the maintainer disposes -- the bump itself is a release-time
call (see [releasing.md](releasing.md) "Declare intent"):

| marker | means | implies |
|---|---|---|
| `FIXED` | existing behavior now does what it was always meant to | patch bump |
| `CHANGED` | existing behavior deliberately different; nothing downstream breaks | patch bump |
| `ADDED` | new backward-compatible surface -- a type, member, or capability that wasn't there before | minor bump |
| `BREAKS` | a consumer's build or observable behavior breaks -- removal, rename, re-signature, contract change | major bump |

The word choice *is* the review question: does this break anyone? Yes -> `BREAKS`; no ->
`CHANGED`. Internal refactors nobody downstream would notice get no marker at all -- the changelog
records what a consumer experiences; git has the rest.

**Marker grammar** -- one form for every class, two optional parts:

```
// <marker>[(<ver>)] [<ref>]: <description>
```

- `// FIXED: <description>` -- pending (unreleased), no ref. The minimum drive-by form.
- `// FIXED <ref>: <description>` -- pending, with a citable ref (see "Refs" below).
- `// FIXED(<ver>): <description>` -- stamped at release time.
- `// FIXED(<ver>) <ref>: <description>` -- stamped, ref preserved.

Every class reads the same way. Stamping inserts `(<ver>)` immediately after the marker word
-- nothing else on the line moves, and a ref survives stamping untouched. Pending vs stamped is
structural: a marker word followed by `(` is stamped; without one it is pending. Once stamped, a
marker is permanent -- never removed or edited again, even if a later change touches the same
area; each change gets its own line.

**Refs** -- the optional `<ref>` is a *local citation anchor*: a short token (a date, a code, an
ID -- whatever reads well) that other comments in the same module can point at, e.g.
`// the quote-state tracking exists because of FIXED 2024-07a`. The marker line is the
definition site; everything else is a citation. Two rules keep that unambiguous:

- Refs are unique **per file**, not per repo -- they're local handles; the module provides the
  context.
- A citation never *starts* a line in marker form -- cite mid-sentence ("see FIXED 2024-07a"),
  and it can never be mistaken for a definition, since tooling only recognizes markers at the
  start of a comment line, colon and all.

**Two placements** -- a marker always gets its own line; a marker trailing a statement on the
same line isn't one, and tooling won't see it:

- **File-level** -- on its own line, after the license header, before `using`s. No method context;
  shows up in the changelog under the module name alone.
- **Method-level** -- the first line inside the method body, immediately after the opening
  brace, below the `///` summary and signature. Carries both the module and method name into the
  changelog. This exact position is a house rule, not a style preference -- it's what makes
  automatic method-name resolution possible without a real parser.

`BREAKS` is **file-level only** -- the site it describes may no longer exist (a removed method has
no body to mark). Its description names what broke and the one-line migration.

Multiple changes on the same method or file just stack, oldest to newest:

```csharp
public LsvRecord Parse(string line)
{
    // FIXED(1.0.2): embedded CR/LF in a quoted field no longer errors
    // FIXED(1.0.4) 2026-03b: escaped "" pair now un-escapes to one literal "
    // CHANGED(1.1.0): trailing whitespace in an unquoted field is now trimmed
    // FIXED: null input threw NullReferenceException instead of Issue
    ...
}
```

**Longer explanation**, when the one-line message isn't enough: a `/* */` paragraph directly
below the marker line it belongs to. Free-form, not parsed by anything -- purely for a human
reading the source later. Keep the marker line itself terse regardless; the paragraph is the
exception, not the norm, since it's a permanent fixture in the file, not a scratch note.

```csharp
public LsvRecord Parse(string line)
{
    // FIXED: quoted fields with embedded delimiters were corrupted
    /*
     * The reader was scanning for the delimiter character without
     * tracking whether it was inside a quoted field, so anything
     * like "Smith, John" in a quoted CSV cell got split into two
     * fields at the comma instead of being treated as one value.
     * Fixed by tracking quote-state across the whole scan, not just
     * per-character.
     */
    ...
}
```

File-level markers follow the same pattern, just outside any method -- license header first, then
the marker (and optional paragraph), then `using`s:

```csharp
// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

// BREAKS: Load(string) removed -- use TryLoad(string, out Issue)
// FIXED: null input threw NullReferenceException instead of Issue
/*
 * Empty/null strings passed to ResPack.Load() were falling through
 * to the normal read path instead of the guard clause. Now both are
 * treated as "no pack" via Try* semantics, per the module's own
 * soft-data convention - never an exception for an absent input.
 */

using System;
```

## Authoring .md

The `.md` files in this repo are first-class documentation -- written with the same care as
source:

- **Easy to scan** -- precise words that keep paragraphs small, or bullets that do the same job.
- **Structure that invites scrolling** -- headings and shape should make scrolling down feel like
  an invitation, not an excuse to stop reading.
- **No fluff paragraphs.** If a sentence doesn't inform, it goes.
- **Code pictures** -- a fenced ``` block whenever it explains better than prose: a function's
  shape, where a `FIXED` marker goes in a `.cs`, a config file's fields.
- **Refer, don't restate** -- generic material lives once in `Docs/`; a folder's local `README.md`
  points there and adds only that folder's own considerations. One place of truth per fact.

---

*Companion to the root [README.md](../README.md) -- see also [building.md](building.md),
[releasing.md](releasing.md), and the [LSV format spec](LSV.SPEC.md).*

Copyright (c) GDFrank - 77NW.net. All rights reserved.

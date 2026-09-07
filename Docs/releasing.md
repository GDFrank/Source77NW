# Releasing

How work in this repo stages and ships -- the third of the trio with [coding.md](coding.md)
(writing the source) and [building.md](building.md) (projects, flags, versions).

## Two repos, one book

**Source77NW.Dev** (this repo) is the working copy of the book -- what you see here is the actual
content that ships, pre-publication, with its full history intact. **Source77NW** is the
published, release-facing repo: release code plus downloadable packages, updated infrequently.

Source77NW deliberately keeps only a **single commit** of history -- each update replaces it
wholesale. Its public change record is not git history but the changelog files it carries,
compiled from the in-source markers (see "Markers become the changelog" below).
Source77NW.Dev is where full history lives, forever.

Source77NW's content moves in two ways (both maintainer-run, one trigger): a **content
refresh** -- the current state mirrored over, the version's `.Build` field +1, no release -- or
a **release**, which happens only when pending markers in the libraries require one. See
"Versions between releases" below.

Either way, what moves is **gated**: work-in-progress projects are held back -- excluded from
the mirror and from the release zips because each realm's `.WIP.slnx` twin (a strict superset
used for local build/test) lists them and the prod `.slnx` doesn't -- that diff *is* the
exclusion, computed at sync time, so the published repo only ever shows finished work. A
project under construction lives its whole WIP life in Source77NW.Dev and first appears in
Source77NW already done, the moment it's added to the prod `.slnx` too. (Code-level `WIP`
*inside* a shipping project is [building.md](building.md)'s story -- same idea, one tier down.)

## Declare intent as you work

Rather than reconstructing at release time what the accumulated work adds up to, say what your
change means for the version *while the work happens*. The bump type itself is **determined by
the maintainer at release time** -- what keeps that decision easy is contributors flagging
line-crossing changes as they land (commit message, PR description, or just say so).

The rule of thumb for what to flag: **major** = anything that would break a consumer's build or
observable behavior (this is the bump that matters for binding -- see
[building.md](building.md) "Versions"; the `BREAKS` marker, below); **minor** = new
backward-compatible surface (the `ADDED` marker); **patch** = fixes and non-breaking changes
nobody downstream has to react to (the `FIXED` and `CHANGED` markers). When your change crosses
a line, flag it -- don't leave it to be rediscovered at release time.

## Markers become the changelog

Four marker classes carry the in-source changelog: `// FIXED:` and `// CHANGED:` for patch-tier
work, `// ADDED:` for new backward-compatible surface, `// BREAKS:` for anything that breaks a
consumer. All follow the same grammar -- `// <marker>[(<version>)] [<ref>]: <description>` --
placed at the site the marker documents (what each class means and where it goes:
[coding.md](coding.md) "Changelog Markers").

At release time, pending markers are stamped in place with the real version -- permanent,
in-source -- and compiled into that release's changelog, which ships in Source77NW. The source
itself is the changelog's source of truth; the `.md` files are its compiled view.

Pending markers of any class are also the **release trigger**: a release happens when the
*libraries* (`Source/`) have pending markers. The highest class pending is the bump the release
prompt offers by default (any `BREAKS` -> major, else any `ADDED` -> minor, else patch); the
maintainer can override it there. Pending markers in `Samples/` never
force a release -- samples version in lockstep, so they ride along and get stamped when the next
library-driven release lands.

## Versions between releases

The repo version is 4-part -- `Major.Minor.Patch.Build` (see [building.md](building.md)
"Versions"). Releases are `M.m.p` events: tags and zip names stay 3-part (`vM.m.p`), and a
release always ships as `.0`. Between releases, **content refreshes** mirror the current state
into Source77NW with only the `.Build` field incremented -- `1.2.3.4` means "release 1.2.3,
plus 4 refreshes since." A refresh updates source content only: no new tag, no new GitHub
Release, no rebuilt zips -- the release assets stay the frozen box below.

## A release is a frozen box

A release is a point-in-time package, decoupled from whatever the repos do afterward:

- **Library zip** -- the compiled DLLs in TFM-named subfolders (`net481/`, `net8.0/`,
  `net10.0/`), identical DLL identity in each; the folder alone carries the TFM. No source.
- **Samples zip** -- built alongside: each sample's build output, one folder per sample -- run
  the exe directly, no build step. Samples are documentation-by-example and are versioned in
  lockstep with the libraries: one version = one coherent state across both.

Attached to the GitHub Release on Source77NW. Refreshes never touch these assets -- a release
stays exactly what it was until a new release ships, and **one release per version** holds:
republishing the same `M.m.p` supersedes the old release wholesale rather than sitting beside
it. The release pipeline itself is maintainer-run tooling, outside this repo.

---

*Companion to [coding.md](coding.md) and [building.md](building.md).*

Copyright (c) GDFrank - 77NW.net. All rights reserved.

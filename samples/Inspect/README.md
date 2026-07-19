# Inspect — a folder-tree attribute reporter

A small real tool: walk a folder tree, print each entry's attribute
bits, optionally filter by attribute, limit depth, choose the walk
order. Its purpose here is to show several Source77NW types doing a
job *together* — the way the library is actually used — rather than
posing one type at a time.

```
usage: Inspect [folder] [attrs] [-d depth] [-b]
  folder   root to walk (default: current directory)
  attrs    ARHSCE letters - show only entries with any of
           these bits, e.g. H, AR, "A.H..." (default: all)
  -d N     maximum depth below the root (default: unlimited)
  -b       breadth-first order (default: depth-first)
```

Run it from the repo root:

```
dotnet run --project samples/Inspect -- . H -d 2
```

## The cast

| Type | Role in this tool |
|------|-------------------|
| `Issue` | The **only** exception type in the whole program — every failure, native or foreign, becomes one, and handling dispatches on its `Kind` |
| `Chars` | Zero-allocation command-line parsing: every token is a *view* into one string |
| `ItemStack<T>` | The traversal worklist — and the reason one switch flips depth-first to breadth-first |
| `FileAttr` / `FileAttrX` | The attribute selections, their `"ARHSCE"`/`"DVORSTN"` dot-format, and the filter test |
| `ExeLock` | One Inspect at a time, via a `KindId.File` lock — the exclusive open **is** the lock |
| `Exe` | The command line as a ready-to-pluck `Chars`; the exe name; the (Windows-only, guarded) elevation check |
| `FS` | Path validation to an `Issue`, and `AsFileIssue` — reframing a caught BCL exception into the same regime |
| `ExitId` | The process exit code, mapped from how the run actually ended |

## The command line, plucked not split

`Exe.GetCommandLineParams()` returns the process command line as a
`Chars` **cursor** already positioned past the executable path. From
there the whole parse is plucking:

```csharp
Chars vParams = Exe.GetCommandLineParams();

while (vParams.PluckedVisible_or_QuotedValue(out Chars vToken))
```

Nothing is `Split()`, no token array is built. `Chars` is a struct
view — `(BotIndex, TopIndex)` over one base string — and plucking
moves the bounds. Each `vToken` is itself a view, so testing its
first char (`vToken.BotChar_or_NUL == Chars.DASH`), consuming that
dash (`PluckChar_or_NUL()`), or demanding all-digits
(`PluckedDigits(out int) && vNum.IsEmpty` — plucked *empty* means
nothing but digits) all happen in place. Quoted arguments come for
free from `PluckedVisible_or_QuotedValue`.

Two SOFT touches, in the library's sense that **absence is a state,
not an error**: no folder argument means the current directory, and
no attrs argument means no filter. Contrast that with what *is* an
error — an unknown option, a non-numeric depth — which is LOUD: a
`BadEntry` Issue, immediately.

The attrs argument never even becomes a string on the happy path:
the token view goes straight into `FileAttr.Get(Chars, out Issue)`,
which parses `ARHSCE` letters (dots ignored, case-insensitive) and
answers a bad letter with a `BadEntry` Issue of its own.

## One structure, both walk orders

The traversal is a loop, not recursion. Pending folders live on an
`ItemStack<Visit>` and the whole difference between depth-first and
breadth-first is which end the next folder comes from:

```csharp
Visit vAt = _Breadth ? vWork.Pluck() : vWork.Pop();
```

`Pop()` takes the newest (LIFO — dive into the last folder found);
`Pluck()` takes the oldest (FIFO — finish this level first).
`ItemStack` is built as a *sliding window* over its slot array, so
taking from the bottom is as cheap as taking from the top — it is a
stack and a queue in one structure, and this sample lets you watch
the same tree print in both shapes:

```
=== depth-first ===                    === breadth-first ===
...... .......       6  readme.txt     ...... .......       6  readme.txt
...... D......   <DIR>  pics           ...... D......   <DIR>  pics
..H... .......       2  .hidden.cfg    ..H... .......       2  .hidden.cfg
...... D......   <DIR>  docs           ...... D......   <DIR>  docs
...... .......       2    notes.md     ...... ....... 153,600  photo.raw
...... D......   <DIR>    old          ...... .......       2    notes.md
...... .......       2      archive.log ...... D......  <DIR>    old
...... ....... 153,600  photo.raw      ...... .......      2      archive.log
```

`ItemStack` is a **bare-metal core** in the library's CORE vs AGENT
pattern: single-threaded, lock-free, no async, loud on misuse. A
synchronous console walk is exactly its habitat. When exposure needs
guarding, that job belongs to an agent wrapper (`StackAgent`) —
never to the core.

## Attribute bits as text

Each entry prints two fixed-width selections:

```
..H... .......              2  .hidden.cfg
...... D......          <DIR>  docs
```

`FileAttr` covers the six "content" bits (**A**rchive **R**eadOnly
**H**idden **S**ystem **C**ompressed **E**ncrypted), `FileAttrX` the
seven "structural" ones (**D**irectory De**v**ice **O**ffline
**R**eparsePoint **S**parseFile **T**emporary
**N**otContentIndexed). One char per bit, `.` when off — the same
format their `Get(Chars, out Issue)` parses back in, which is
exactly how the filter argument arrives. The filter test is one
call: `_Filter.Selected(xEntry)` — true when the entry shares any
selected bit.

(On Windows/NTFS the full range of bits shows up; on Unix, .NET maps
what it can — dot-files report as Hidden, which is why the `H`
filter works everywhere.)

Note the enum members: `A_Archive`, `H_Hidden` — the letter is part
of the name. That is the library's caption-name enum style: the
identifier *is* the display information.

## One lock, one line

```csharp
if (!ExeLock.GotLock(sLockPath, ExeLock.KindId.File, out ExeLock _, out Issue vLockIssue))
```

`KindId.File` opens (or creates) the file with `FileShare.None` —
the exclusive open **is** the lock; there is nothing else to hold.
The registry behind `ExeLock` shares one instance per name, and
`ExeLock.DisposeAll()` in the `finally` releases everything at
shutdown. (The exclusion is OS-enforced on Windows, advisory between
processes on Unix.)

## The Issue regime — dispatch by Kind, not by type

This program `catch`es exactly one exception type of its own. Every
failure — a bad option, a missing folder, an unreadable directory,
even a foreign BCL exception — ends up as an `Issue`, and *what
happens next* is a dispatch on its `Kind`:

```csharp
if (theIssue.IsProgrammingIssue)                    // >= ProgramIssue: a bug
    Console.Error.WriteLine(theIssue.Header_Detail_Message_Inner);
else if (theIssue.IsAny(IssueKind.NeedPermit, IssueKind.WrongPermit, IssueKind.LockedAccess))
    Console.WriteLine("  ! " + ...);                // friendly line, walk continues
else if (theIssue.Kind == IssueKind.BadEntry)
    { Console.WriteLine(theIssue.Message); _Usage(); }
else
    Console.WriteLine(theIssue.Header_Message);
```

That is the design intent of `Issue`: **one exception type, many
kinds** — data instead of hierarchy — so the same object supports a
humane dialog line *and* hard-core forensics. The forensic side is
always there:

```
Issue: NoSuch (4.100.6)
No such folder
"C:\demo\nope\"
```

`4.100.6` is Kind 4 (`NoSuch`), issueSource 100 (this program), Spot
6 (the exact raise site in `Program.cs`). The same three values pack
into `Exception.HResult`, so the origin survives even through code
that only sees `Exception`.

The pieces this sample uses, and where they come from:

- **Raising.** `Issue.Create(issueSource, spot, params...)` composes
  the message from its params by type: an `IssueKind` ranks in, an
  exception becomes the inner, and any *other* enum renders its name
  with underscores as spaces. Hence the `Say` enum — user-facing
  messages living as greppable, typo-proof identifiers:
  `Say.Unknown_option` prints as `Unknown option`.
- **Classifying strangers.** The last-resort `catch (Exception)`
  wraps the foreigner via `Issue.KindOf(theException)`, which maps
  well-known BCL types to their closest Kind — so even unexpected
  failures enter the same dispatch.
- **Reframing at the walk.** An unreadable folder throws a BCL
  exception; `FS.AsFileIssue(exception, path)` reframes it as a
  Kind-classified Issue (`NoSuch` / `LockedAccess` / `NeedPermit` /
  other) with a caption and the path. The walk reports it and
  **continues** — SOFT at the walk level, because a denied folder is
  a condition of the filesystem, not a failure of the program.
- **Exiting.** `ExitId` maps the ending to the process exit code:
  `Completed` (0), `Canceled` (1, second instance), `Failed` (3,
  operational), `Critical` (4, programming fault).

## House style, visible in passing

A few library conventions this sample inherits, worth noticing:

- **`Got*` / `Plucked*` / `*_or_null` / `*_or_NUL`** — the bool +
  `out` surface everywhere a miss is a legitimate outcome; the name
  tells you the miss behavior.
- **`const ushort issueSource`** per module, distinct `Spot` per
  raise site. Core tiers reserve 65,000+; samples and apps use the
  low range (this one is 100).
- **C# 7.3, no nullable, unchecked arithmetic** — inherited from the
  repo `Directory.Build.props`; `Exe`'s initializer actually
  verifies the unchecked contract at startup.
- Samples relax library-grade strictness (no XML doc file, not
  packable) via `samples/Directory.Build.props` — the library's own
  rules stay untouched.

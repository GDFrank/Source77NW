# ListFiles — a filtered file lister, TSV out

A small real tool: walk a folder, filter files by attribute bits, and
write a TSV listing meant to paste straight into Excel. Its purpose
here is to show several Source77NW types doing a job *together* — the
way the library is actually used — rather than posing one type at a
time. It also demonstrates the **Exe identity contract**: the
`[assembly:]` block at the top of `ListFiles.ExeMain.cs` is what
`Exe.DomainName`, `Exe.ExeCodeName`, `Exe.ExeGuid`,
`Exe.DomainFolderPath(...)`, and friends read at startup.

```
usage: ListFiles [path] [+<attrs>] [-<attrs>] [-all] [-out[:<file>]]
  path      root folder to list (default: current directory)
  +<attrs>  ARHSCE letters - show only files sharing one of these
            bits, e.g. +H, +AR (default: no include filter)
  -<attrs>  ARHSCE letters - hide files sharing one of these bits
            (default: no exclude filter; a letter in both +/- is
            excluded - exclude is applied after include)
  -all      recurse inner folders (default: top-level only)
  -out      write TSV to a timestamped file under
            Exe.DomainFolderPath(UserDocuments) instead of stdout
  -out:file write TSV to that exact file path instead of stdout
```

Run it from the repo root:

```
dotnet run --project samples/ListFiles -- . +H -R -all
dotnet run --project samples/ListFiles -- . -out
```

Banner and run-summary text always go to **stderr**; the TSV lines
(header + one row per file) are the only thing on **stdout** — so a
bare run pipes or redirects cleanly regardless of `-out`:

```
dotnet run --project samples/ListFiles -- . > listing.tsv
```

## The cast

| Type | Role in this tool |
|------|-------------------|
| `Exe` | EntryAssembly identity from the `[assembly:]` block: `DomainName`, `ExeCodeName`, `ExeGuid`, `ExeVersion`, `Contact`, `DomainFolderPath(...)`, the command line, `ExeNameOnly`, guarded `IsAdmin` |
| `Issue` | The **only** exception type in the whole program — every failure, native or foreign, becomes one, and handling dispatches on its `Kind` |
| `Chars` | Zero-allocation command-line parsing: every token is a *view* into one string |
| `ItemStack<T>` | The traversal worklist — a LIFO stack of pending folders |
| `FileAttr` | The six-bit ARHSCE selection, its dot-format text, and the include/exclude filter tests |
| `ExeLock` | One ListFiles at a time, via a `KindId.File` lock — the exclusive open **is** the lock |
| `FS` | Path validation to an `Issue`, and `AsFileIssue` — reframing a caught BCL exception into the same regime |
| `ExitId` | The process exit code, mapped from how the run actually ended |

## Assembly identity, not csproj synthesis (D4)

`samples/Directory.Build.props` sets `GenerateAssemblyInfo=false`, so
nothing is synthesized: every value `Exe` surfaces is a hand-written
`[assembly:]` attribute right here in `ListFiles.ExeMain.cs`. The file
marks which attributes are domain-wide (would live in one shared
`EntryAssembly.Domain.cs` in a real multi-exe domain — compare
AppLab's `EXE$ExeInfo.cs` + `EXE.<code>.ExeInfo.cs` split) versus
per-exe. `DomainName` is deliberately `Source77NW.example` and
`Contact` is `mailto:NOBODYHERE@SAMPLES.invalid` — both land on
IANA/RFC 2606 reserved, guaranteed-dead placeholder domains, so the
sample can show the real contract without pointing anywhere real.

## Traditional arg parsing vs. the Source77NW ops model

This sample's command line is parsed by hand, one `Chars` token at a
time (`_ParsedCommandLine` above) — deliberately the same kind of
parsing any .NET console app writes, not the library's own
[`Cmd.OpsBase.CmdApp`](../../src) operations model (named commands
registered as `Cmd.Ops*` types, dispatched by the app's command
router). That model is a real, larger piece of the library, but
samples are demonstrations of individual types cooperating, not of
the ops framework — so `ListFiles` (and `Lookup`) stick to plain
positional/switch parsing, the same shape as `System.CommandLine` or
hand-rolled `args[]` code, just built on `Chars` views instead of
`string.Split()`.

## The command line, plucked not split

`Exe.GetCommandLineParams()` returns the process command line as a
`Chars` **cursor** already positioned past the executable path. From
there the whole parse is plucking:

```csharp
Chars vParams = Exe.GetCommandLineParams();

while (vParams.PluckedVisible_or_QuotedValue(out Chars vToken))
```

Nothing is `Split()`, no token array is built. Each `vToken` is
itself a view, so testing its first char
(`vToken.BotChar_or_NUL == Chars.PLUS` / `Chars.DASH`), consuming
that lead character (`PluckChar_or_NUL()`), and testing the
remainder (`"all"`, `"out"`, `"out:..."`, or ARHSCE letters) all
happen in place. `-all` and the two `-out` forms are reserved
`-<word>` tokens tested before falling through to
`FileAttr.Get(Chars, out Issue)`, which parses `ARHSCE` letters
(dots ignored, case-insensitive) and answers a bad letter with a
`BadEntry` Issue of its own.

Two SOFT touches, in the library's sense that **absence is a state,
not an error**: no path argument means the current directory, and no
`+`/`-` argument means no filter (show everything). Contrast that
with what *is* an error — an unknown option, an unresolvable path —
which is LOUD: a `BadEntry` Issue, immediately.

## +/- filters: repeatable, contradictions permitted

`+<attrs>` and `-<attrs>` can each appear more than once — every
occurrence just ORs more bits into `_IncludeBits` / `_ExcludeBits`.
Naming the same letter in both is allowed, not an error; the walk
applies include first, exclude second, so a contested letter always
comes out excluded. That order is documented right in the usage text
(`-h`), not left to guesswork.

## Scope: top-level by default, `-all` to recurse

Without `-all`, `ListFiles` only lists the given folder's immediate
children — matching folders are still counted but never opened. With
`-all`, every subfolder discovered gets pushed onto the same
`ItemStack<DirectoryInfo>` worklist, so the walk goes all the way
down. `ItemStack` is a **bare-metal core** in the library's CORE vs
AGENT pattern: single-threaded, lock-free, no async, loud on misuse —
exactly the habitat for a synchronous console walk.

## TSV out: one path to stdout or a file

```csharp
private static TextWriter _OpenOutput()
{
    if (!_GotOut) return Console.Out;
    ...
    return new StreamWriter(_OutFilePath, append: false, Encoding.UTF8);
}
```

Bare `-out` resolves its file path through
`Exe.DomainFolderPath(Exe.DomainFolderId.UserDocuments)` — the same
per-domain folder resolution every Source77NW exe gets for free once
`DomainName` is declared. `-out:<file>` takes an exact path instead.
Either way the four TSV columns are identical:
`FileName`, `Length` (raw bytes), `Attributes` (FileAttr's fixed
6-char ARHSCE text — sortable and column-aligned once pasted into
Excel), `Folder` (full path). No `FileAttrX`: that's NT structural
esoterica, out of scope for this sample.

## One lock, one line

```csharp
if (!ExeLock.GotLock(sLockPath, ExeLock.KindId.File, out ExeLock _, out Issue vLockIssue))
```

`KindId.File` opens (or creates) the file with `FileShare.None` — the
exclusive open **is** the lock; there is nothing else to hold.
`ExeLock.DisposeAll()` in the `finally` releases everything at
shutdown.

## The Issue regime — dispatch by Kind, not by type

This program `catch`es exactly one exception type of its own. Every
failure — a bad option, a missing folder, an unreadable directory,
even a foreign BCL exception — ends up as an `Issue`, and *what
happens next* is a dispatch on its `Kind`, with everything routed to
**stderr** so stdout stays pure TSV:

```csharp
if (theIssue.IsProgrammingIssue)                    // >= ProgramIssue: a bug
    Console.Error.WriteLine(theIssue.Header_Detail_Message_Inner);
else if (theIssue.IsAny(IssueKind.NeedPermit, IssueKind.WrongPermit, IssueKind.LockedAccess))
    Console.Error.WriteLine("  ! " + ...);          // friendly line, walk continues
else if (theIssue.Kind == IssueKind.BadEntry)
    { Console.Error.WriteLine(theIssue.Message); _Usage(); }
else
    Console.Error.WriteLine(theIssue.Header_Message);
```

That is the design intent of `Issue`: **one exception type, many
kinds** — data instead of hierarchy — so the same object supports a
humane dialog line *and* hard-core forensics (`Kind`, `issueSource`,
`Spot`, message, inner chain — the same three values also pack into
`Exception.HResult`, so the origin survives even through code that
only sees `Exception`).

- **Raising.** `Issue.Create(issueSource, spot, params...)` composes
  the message from its params by type — hence the `Say` enum:
  user-facing messages living as greppable, typo-proof identifiers
  (`Say.Unknown_option` prints as `Unknown option`).
- **Classifying strangers.** The last-resort `catch (Exception)`
  wraps the foreigner via `Issue.KindOf(theException)`.
- **Reframing at the walk.** An unreadable folder throws a BCL
  exception; `FS.AsFileIssue(exception, path)` reframes it as a
  Kind-classified Issue. The walk reports it and **continues** — SOFT
  at the walk level, because a denied folder is a condition of the
  filesystem, not a failure of the program.
- **Exiting.** `ExitId` maps the ending to the process exit code:
  `Completed` (0), `Canceled` (1, second instance), `Failed` (3,
  operational), `Critical` (4, programming fault).

## House style, visible in passing

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
  packable, `GenerateAssemblyInfo=false`) via
  `samples/Directory.Build.props` — the library's own rules stay
  untouched.

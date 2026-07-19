# Lookup — zero-copy key lookup over one loaded text

Load a glossary document **once** into a single string, index every
record as views into that string, sort the index, answer key queries
by binary search over the views. No intermediate strings on load, no
strings compared during lookup — and, it turns out, none needed for
output either.

This is the production pattern behind AppLab's `Cmd.OpsUser` (which
loads user-defined operations the same way), reduced to its essence
on the published types.

```
usage: Lookup [-f file.lsv] [key ...]
  no keys: interactive - a key per line, empty line ends
```

```
dotnet run --project samples/Lookup -- chars itemstack
dotnet run --project samples/Lookup            (interactive)
```

## The cast

| Type | Role in this tool |
|------|-------------------|
| `LsvDoc` / `LsvRecord` | One file → one `DocText` string; records plucked as views over it |
| `Chars` | Every key and every value, held and compared as `(BotIndex, TopIndex)` views |
| `ItemStack<Entry>` | The index: capacity-estimated, custom-`Comparer` sorted, binary-searched |
| `Issue` / `ExitId` | Load failures and bad entries, in the same Kind-dispatch regime as `ListFiles` |
| `Exe` | Command-line as a `Chars` cursor; the default data file beside the exe |

## One string, many views

`LsvDoc.LoadedDocFromFile` reads the file into **one** string —
`DocText` — and everything after that is bookkeeping over it. The
index entry is deliberately tiny:

```csharp
private struct Entry
{
    public Chars Key;           // = the name after the record code, captured once
    public LsvRecord Record;    // named fields, enumerated on demand
}
```

A `Chars` is a base-string reference plus two ints; an `LsvRecord`
is the same plus a fields offset. Loading N records allocates the
entries' slot array and *nothing per record* — no key strings, no
value strings, no per-line substrings. The load loop is the OpsUser
loop:

```csharp
while (_Doc.PluckedItemRecord(out LsvRecord vRecord))
{
    vRecord.Context.GotNameAndText(out Chars vCode, out Chars vKey);

    if (!vCode.Equals(RecordCode, ignoreCase: true)) continue;

    _Entries.Push(new Entry() { Key = vKey.Trim(), Record = vRecord });
}

_Entries.Sort();
```

The `GotNameAndText` split is the record-code dispatch — `:def`
records are indexed by the name that follows the code; `:rem`
remarks and `:::` dividers fall through the filter, exactly as
`Cmd.OpsUser` skips REM records in an OPS document.

(`Cmd.OpsUser` compresses one step further — storing raw
`ValueBot`/`ValueTop` ints and rebuilding the view in its
`Value(index)` accessor, since the base string already lives once in
its `LsvDoc` reference. Same idea, tighter packing.)

The capacity estimate is also the OpsUser move: `DocText.Length`
divided by an expected record size, floored at 16 — sized from the
data, not guessed.

## Sorting and searching views

`ItemStack` sorts and binary-searches through its `Comparer`
property, so the index orders itself by *content* of the key views:

```csharp
Comparer = Comparer<Entry>.Create((a, b) => a.Key.CompareTo(b.Key, ignoreCase: true))
```

The interesting moment is the query. A queried key is a `Chars` view
over the *command line* (or an input line) — a different base string
than `DocText`. `Chars.CompareTo(Chars)` compares content, not
identity, so one probe entry:

```csharp
_Entries.BinarySearchNearest(new Entry() { Key = theKey }, out bool bFound);
```

binary-searches document-backed keys with a query-backed key, and
still no string exists on either side. `BinarySearchNearest` also
gives the miss behavior for free — the nearest key becomes the
"did you mean" suggestion:

```
? spott - not found; nearest is Spot
```

Keys typed on the command line get the same treatment as the data:
they are plucked as views and pushed — as views — onto an
`ItemStack<Chars>`, then `Pluck()`ed FIFO after the load so answers
come out in the order asked.

## On-demand — and then not even that

The stated rule is *materialize only when necessary*: hold views,
call `ToString()` at the moment text must leave the view world. This
sample has exactly one such moment on load (`-f`'s path must become
a real string to reach the file system) — and on output it turns out
**no** moment is necessary:

```csharp
vEntry.Key.Write(Console.Out);

while (vEntry.Record.GotNextFieldNameAndValue(ref iCursor, out Chars vName, out Chars vValue))
{
    vName.Write(Console.Out);

    while (vValue.PluckedLine(out Chars vLine))
    {
        vLine.Write(Console.Out);   // re-indented in the real code
    }
}
```

`Chars.Write(TextWriter)` streams each view directly — the field
name, and a multi-line value re-indented line by line, every line
itself a view. From file read to console, the happy path of this
program materializes zero strings from the document.

Where OpsUser *does* intern its tokens (`vName.ToString()`), it is
for a reason this sample can point at: those tokens cross into
CmdApp's global string-keyed lookup — a boundary where the string
earns its allocation. That is the pattern's real discipline: not
"never make strings," but *know which boundary pays for one*.

## The LSV format, on display

The data file (`glossary.lsv`, copied beside the exe) is written to
show the format's strengths, under the house convention:

```
:<recordCode> <name/values>    begins a record
.<fieldName> <value>           begins a named field
unmarked lines                 continue the field's value
```

```
:def SOFT
.desc Absence is a state, not an error: Try*/Got*/Did* surfaces
report a miss with bool + out instead of throwing.
.ant LOUD
```

Because the markers are positional (a line may not *begin* with `:`
or `.` — the only restriction on value text), a value continues
across unmarked lines with **no escaping**. Fields are **optional**
— `Spot` carries only a `.desc`, and `Lookup` simply prints what a
record has — and **repeatable** — `Chars` carries two `.note`
fields, and the enumeration yields both in order. Preamble above
the first record is free documentation space; `:rem` records hold
remarks the code filter skips; a `::::::` line is a pure visual
divider (repeated markers collapse). The first record is the
document header — `Lookup` checks `IsDocName("GLOSSARY")` and
answers a wrong document with a `BadData` Issue, exactly as OpsUser
guards its `OPS` documents.

# Highlights

Brief tours of the modules the root README name-drops -- each a little more than a one-liner,
none a manual. Every module documents itself fully in its own `///` doc comments (the single
owner of the detail -- Visual Studio tooltips *are* the reference manual); each section below
ends with where to go deeper.

## Chars -- scan text without making strings

A `Chars` is a lightweight **view** over a base string -- one string reference plus two indexes,
cheap to copy, never a substring allocation:

```
TextBase:   C : \ w o r k \ n o t e s . t x t
                  ^                   ^
               BotIndex            TopIndex
view:           "work/notes"     (no new string made)
```

Scanning *consumes* the view -- `Pluck*` from the bot, `Pop*` from the top -- handing back
smaller views over the same base string:

```csharp
Chars v = new Chars("  alpha \"two words/"  123  ");
v.PluckedVisible_or_QuotedValue(out Chars a);   // a -> alpha
v.PluckedVisible_or_QuotedValue(out Chars b);   // b -> two words   (quotes handled)
v.PluckedDigits(out int n);                     // n -> 123         (overflow-safe)
```

Underneath sits `CharsCat`, a 64-bit category-flag system: every ASCII char owns a unique
flag, with named sums (`DIGIT`, `Ascii_Letter_Digit`, `Any_Visible`, ...) so scans read as
intent -- `PluckWhile(CharsCat.DIGIT)`, `PluckUntil(CharsCat.Ascii_CR_LF)`, `ContainsOnly(...)`.
A `Chars` also *acts* like a string where it counts: compares, `Contains`, `IndexOf`,
indexing, and dictionary-key use. It's the parsing substrate for paths, command lines, CSV,
settings, and LSV throughout the library.

**More:** `Core.Base/Chars.cs`; the `Try*`/`Plucked*`/`Found*` naming key is
[coding.md](coding.md).

## EnumCodes, ResCode, Bits -- the enum *is* the table

Instead of parallel tables for captions, tips, shortcuts, and icons, one enum carries it all
as attribute metadata:

```csharp
[EnumCodes("Edit commands")]
enum EditCmd : uint
{
    [EnumInfo("|icon edit.cut.ico|ctrl ctrl-X|tip Cut selected|")]
    Cut = 1,
    // ...
}
```

`EnumCodes` registers the enum as a validated truth table of codes. `ResCode` is the app-side
handle -- a tiny struct (a cache index plus a code index, **no stored text**) that resolves
caption / menu / button / tip / ctrl-key / icon / echo / blob *by code*, O(1), through
registered suppliers. The payoff: a language switch is just walking the visible controls and
re-pulling their captions -- no per-control language state to invalidate.

`Bits` gives flag-valued enums a shared bit-section layout over the low `ByteCode` byte
EnumCodes requires:

```
bit 31                                                        bit 0
G G G G  C C C C  E E E E E E E E  V V V V V V V V  B B B B B B B B
Grp      Cmd      Etc              Val              ByteCode
```

with pre-shifted section enums so local enums compose flags without writing shifts.

**More:** [building.md](building.md) "Embedded resources (ResPack / ResCode)";
`Core.Base/EnumCodes.cs`, `ResCode.cs`, `Bits.cs`; the `App.FileList` sample selects its
columns through an `EnumCodes`-registered enum.

## ItemStack -- one structure, three patterns

A single-array list that serves stack, queue, and deque use without ever shifting elements --
FIFO works by letting the bottom *slide up* inside the buffer:

```
slots:   .  .  a  b  c  d  .  .
               ^           ^
            ItemsBot    ItemsTop
Push(x)  -> lands at ItemsTop
Pop()    -> takes d (LIFO)
Pluck()  -> takes a, ItemsBot slides right (FIFO, no shifting)
```

Plus sparse `Set`, `Sort`/`BinarySearchNearest`, cursor enumeration (`GotNext`/`FoundNext`),
`IList`/`ICollection` vocabulary, and optional recycled buffers via `Heap`. It's a bare-metal
core -- single-threaded, fail-loud, no defensive ceremony; `StackAgent` is its wrapped,
public-facing exposure, meant to be subclassed for invariants, tallies, or locking.

**More:** `Core.Base/ItemStack.cs`, `StackAgent.cs`, `Heap.cs`; the core/agent split is the
"bare-metal cores, wrapped exposure" principle in [coding.md](coding.md) "Design principles".

## BytesReader / BytesWriter -- BinaryReader/Writer, house rules

Sealed specializations of the BCL pair, adding what binary persistence actually needs:

- **"Any" integers** -- 7-bit variable-length encoding (`WriteAny_Int32` ... `WriteAny_UInt64`,
  each paired with a `ReadAny_*`): small values take one byte, every value round-trips exactly.
- **Length-prefixed byte blocks** (`WriteAny_Bytes` -- null travels as -1 and comes back null),
  Guids, and stream copy-through (`ReadAndWrite`).
- **Save/Load contracts** -- `IBytesWriter`/`IBytesReader` let any type persist itself.
- **Soft factories** -- `Created(path, out writer, out Issue)`: a failed file open is data, not
  an exception.
- **Dynamic in-stream compression and encryption** -- the headline. Layers switch on and off
  *mid-stream* by hot-swapping the output stream, so one file can hold plain and
  compressed/encrypted sections in a single pass:

```
[ header (plain) ][ payload (GZip) ][ footer (plain) ]
                 ^                 ^
         StartCompression    StopCompression
```

  The two **combine**, in one enforced order -- encryption is layered first
  (`StartedEncryption(key)`, keyed via `CryptoKey`), compression on top -- so bytes flow
  GZip -> Crypto -> base stream: data is compressed *before* it's encrypted, the only order
  that gains anything (ciphertext looks random and won't compress). The writer's own guard
  makes the wrong order unreachable (`Encryption_not_allowed_while_compressing`), and the
  reader enforces the mirror on the way back (decompression is allowed while decrypting,
  never the reverse):

```
plain bytes -> [ GZip ] -> [ Crypto ] -> base stream
               2nd: StartCompression
                           1st: StartedEncryption(key)
```

  One of each layer at a time; `Dispose` unwinds every layer -- flushing the crypto final
  block -- down to the base stream.

**More:** `Core.Base/BytesWriter.cs`, `BytesReader.cs`, `BytesReaderStream.cs`,
`CryptoKey.cs`.

## AS, FS -- strings named, files softened

`AS` (Ascii Strings) is the string counterpart to `Chars`' char constants: named ASCII
control/punctuation strings (`AS.TAB`, `AS.QUOTE`), frequent combined strings, DOT
file-extension strings, timestamp formats, URL schemes. Named constants over embedded
literals: easy recognition, consistent spelling, and the eyeball never has to re-verify the
"stuff".

`FS` is the file-system workhorse wearing the library's soft surface end to end: path
validation and normalization (including `file://` URL conversion), well-known folder
resolution, soft create/copy/delete, text and stream readers/writers with UTF-8 BOM control,
BOM and encoding *detection*, path classification, process starting. Everything reports
failure as `*_or_null` / `Try*` / `Got*` plus an out `Issue` -- no exception-driven flow.

**More:** `Core.Base/AS.cs`, `FS.cs`; nearby kin `FileAttr.cs`, `FileAttrX.cs`, `FileExt.cs`,
`Zipping.cs`.

## LsvRecord, LsvDoc -- records without escaping

LSV (Line Separated Values) is csv's spirit with line markers instead of delimiters: a line
starting `:` opens a record, a line starting `.` opens a field -- so multi-line values need
**no escaping at all** (the sole constraint: a value's own lines never begin with `:` or `.`):

```
:person id 42
.name Grace Hopper
.note A multi-line value
just keeps going, line after line -
no quoting, no escaping.
:person id 43
...
```

`LsvRecord` parses one record at a time with a cursor, riding on `Chars` views -- no
per-field string allocation. `LsvDoc` is the association that makes a *file* of them a
document: the **first** record is the header (document name, type, context/identity), and
later records are plucked one at a time from the remainder. Header describes, stream
delivers.

**More:** [LSV.SPEC.md](LSV.SPEC.md) -- the full format spec (tokens, reserved typeCodes, the
`DOC` schema); `Core.Base/LsvRecord.cs`, `LsvDoc.cs`.

## Also in Core.Base

The same `///`-documented standard applies to the rest of the folder -- a few worth knowing
exist: `Issue` (the single exception type: humane caption for dialogs plus a forensic
source/spot trail for logs), `TextBuilder` (allocation-conscious text building),
`Csv.Reader`, `Zipping`, `Crc32`, `CryptoKey`, `WordId`, `EnumVals`, `Exe`/`ExeLock`,
`Pulser`, `Listeners`, `Heap`. Open the source -- the summaries are the manual.

---

*Index of all docs: [README.md](README.md). How this all builds: [building.md](building.md).*

Copyright (c) GDFrank - 77NW.net. All rights reserved.

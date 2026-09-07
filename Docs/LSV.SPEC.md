# LSV -- Line Separated Values

*A compact working spec -- syntax first, schemas second.*

## What

LSV is a plain-text record format, similar in spirit to `csv`/`tsv` but line-marker based
instead of delimiter based. It stores multi-line values with no escaping mechanism -- no quoted
fields, no backslash escapes -- as long as one constraint holds: a value's own lines never begin
with a colon or a dot.

## Nomenclature

- `csv` -- Comma Separated Values
- `tsv` -- Tab Separated Values
- `lsv` -- Line Separated Values
- `token` -- a valid token (see Tokens, below)
- `record` -- an LSV row, analogous to a `csv` row
- `field` -- an LSV column, analogous to a `csv` column

## Markers

- **`RecordMarker`** -- `<LF>:` (a.k.a. `LF:`) -- starts a new record; the "row marker"
- **`FieldMarker`** -- `<LF>.` (a.k.a. `LF.`) -- starts a new field within a record; the
  "column marker"
- where `<LF>` is a line feed -- assumed present at BOF (beginning of file) even with no literal
  preceding newline

## Tokens

A `token` is ASCII alphanumeric, with optional embedded dots to separate words, and must begin
with a letter.

## Records

Records are identified by:

```
LF:<typeCode> <attributes>
```

where `typeCode` must be a valid `token`. Any invalid `typeCode` is treated as a remark and
ignored.

Reserved `typeCode`s:

- `REM` -- remarks (i.e. an intentionally-invalid token)
- `DOC` -- a `DOC` header record (see DOC, below)
- `AIDOC` -- an AI-generated/AI-maintained document (see AIDOC, below)
- `LSV` -- reserved for `tsv` <-> `csv` <-> `lsv` reformatting

Reservation happens at **two levels**: the `typeCode`s above are reserved at the *syntax*
level, in every `.lsv` file; a schema like `:DOC` reserves further record codes and fields at
its own *schema* level (see DOC, below).

## Fields

Fields are identified by:

```
LF.<fieldName> <value up to next field marker>
```

`fieldName` may be written as:

- `LF."field name"` -- matching a `csv` column header, quoted
- `LF.<token>` -- when the `csv` column header is itself a valid `token`

## Records as comments or separator bars

Any record whose `typeCode` isn't a valid token is a *comment record* -- ignored as content
(per Records, above) while still acting as a record boundary: it terminates the record before
it, which is why a comment or bar sits between records, never between the fields of one
record. Conventional prefixes:

```
://  comment text, code-style
:==================   major separation bar
:------------------   sub separation bar
:REM remark text (the reserved remark code - see Records, above)
```

Bar width, pairing, and style are authoring preference, not spec -- extend `:==` / `:--` to
any width that suits the file. Repeated bare markers (`::::` / `....`) also collapse to a
single marker and serve the same visual-separator purpose.

## Parsing

The reference implementation (`LsvRecord.cs`) parses with a cursor rather than splitting the
whole file up front:

- `LsvRecord.Parsed(ref unparsed, out record)` takes the remaining text by reference, returning
  `false` once no record remains -- the natural shape for a `while` loop that walks record-by-record
- within a record, fields are walked the same way: `cursor = 0; while (record.GotNextFieldValue(ref
  cursor, out value)) { ... }`
- `GotNextFieldNameAndValue(ref cursor, out name, out value)` splits a raw field into its name
  and value in one step
- `Record` / `Context` / `Fields` expose the raw record text, the trimmed header context, and
  the trimmed fields text, respectively
- field values come back trimmed -- surrounding CR/LF is stripped for you

This is a streaming, single-pass walk -- not a build-a-tree-then-query model -- consistent with
the library's zero-allocation, minimal-heap-motion philosophy (see `Chars`, `ItemStack`, etc.).

## DOC

`DOC` is one of LSV's reserved header-record `typeCode`s (see `LsvDoc.cs`) -- the general-purpose
document schema for captured or authored content (articles, notes, snippets, round-tripping with
HTML/Markdown/PDF/Word). A `:DOC {id}` record's reserved fields:

| field | meaning |
|---|---|
| `.id` | same value as the header directive (redundant, for validation) |
| `.title` | required |
| `.author` | author name(s) |
| `.url` | source URL, if captured from the web |
| `.created` / `.updated` / `.captured` | ISO 8601 timestamps |
| `.keywords` | space-separated (quote multi-word phrases) |
| `.category` | one of: `article`, `documentation`, `reference`, `tutorial`, `recipe`, `blog-post`, `news`, `research`, `technical`, `creative`, `notes` |
| `.format` | original source format: `html` \| `markdown` \| `text` |
| `.codes` | default `<ref>` for record codes used here but not defined by the schema -- see Record-code aliasing, below |
| `.code` | `<code> <ref>` -- overrides `.codes` for one specific record code |
| `.chunks` / `.chunk` | see Chunking, below |

A `:DOC` can also carry **resource records** (`:img`, `:link`, `:colors`, `:layout`, `:meta` --
extracted references reused via `<ref-*>` placeholders) and **content records** (`:frame`,
`:summary`, `:abstract`, `:content` -- the body). See Notes, below, for where the fuller
catalog lives.

A `:DOC` isn't limited to its reserved fields above -- it may also carry locally-defined fields
of its own, as long as they don't collide with the reserved names.

### Record-code aliasing

`.codes <ref>` declares, up in the header, where this document's record codes are defined;
`.code <code> <ref>` overrides that default for one specific code. `<ref>` is deliberately
loose -- an officially registered standard, a URL, or simply a description. Neither field is
mandatory, and undeclared codes remain plain local use, as always. The point is efficient
production: LSV is at heart a flat database where size is a factor, and a one-line pointer up
top beats restating a code catalog in every file -- the codes themselves stay "standard" while
their definitions live wherever the `<ref>` says.

A field's value can optionally carry its own content type, declared via the `:DOC`'s own
attributes -- the mechanism exists, but no content-type codes are specified yet (deliberately
deferred; a field is plain text until this is designed further).

### Chunking

Any document type can be **chunked** once it's too large for one physical file:

- `.chunks 0` -- not chunked
- `.chunks n` (n > 0) -- split across n chunk files; chunk 0 is metadata-only, chunks 1..n carry
  content
- `.chunk <n>` -- appears in a chunk file's own header, saying which chunk number it is

**File naming** (shown for `DOC`; same pattern for any chunked type):

| piece | filename |
|---|---|
| chunk 0 (meta only) | `doc.{identity}.0` |
| content chunk *n* | `DOC.{identity}.{n}.lsv` |

A mismatch between filename and declared type/chunk invalidates the load -- renaming a file is
enough to take it "out of the loop" without touching its contents.

## AIDOC

Reserved header `typeCode` for documents **generated and maintained by an AI**, carrying that
AI's own conventions in the `AIDOC` record's fields -- the document tells its reader (human or
another AI) how it is organized, rather than assuming a shared schema. Typical shape:

```
:AIDOC <identity> <date>
.about <what this document is, in one field>
:==================
:schema
.<recordType> <one line defining that type and its fields>
.<recordType> <...>
```

The header's fields plus a following `:schema` record make the file *self-describing*: a cold
session -- or a different AI entirely -- decodes the file from the file, no external schema
registry required. Beyond that pattern, conventions are the authoring AI's to define per
document; `AIDOC` reserves the space rather than fixing a schema.

## Notes

- LSV only defines *syntax*; each record `typeCode` (`DOC`, `CHAT`, ...) defines its own *schema*
  on top. `:DOC` and `:CHAT` are the two schemas currently specified in the internal design notes
  (`LSV-DOC.txt`, `LSV-CHAT.txt`) -- `:DOC`'s fuller catalog (resource records, content records)
  isn't reproduced in full here; ask if you want it expanded into this doc directly.
- `:CHAT` records a single human/AI conversation turn by turn (`:msg {speaker}`, `.ref`, `.post`)
  -- a different schema on the same base syntax, not covered above.
- Nothing about the LSV syntax itself requires a record's fields to match a predeclared schema --
  the parser only cares about markers and trimmed values. LSV also gets used informally as a
  lightweight structured-notes format: a `typeCode` invented on the spot, with whatever fields
  make sense, no formal schema behind it at all. Schema conformance is a convention layered on
  top by whoever's reading the file, not something the format itself enforces.
- `csv` can convert to an `LsvDoc`, have its fields reordered and/or renamed (in code), then
  convert back out to `csv` -- going from one vendor's column naming/order to another's. `LsvDoc`
  works as an editable intermediate for csv-to-csv vendor reformatting, not just a csv<->lsv
  format in itself.

## Examples

A formal `:DOC` header, abbreviated:

```
:DOC webdev-intro-2026
.id webdev-intro-2026
.title Introduction to Modern Web Development
.category tutorial
.format html
.chunks 0
```

Informal, schema-free use -- a `typeCode` invented on the spot, structured as `.title`/`.content`
fields per record:

```
:DOC reference What is a Reference File
.created 2026-01-15T22:43:00Z
.topic Documentation, File Organization

:definition
.title What is a Reference File
.content
A standalone document containing just the key facts for quick lookup later.
No conversation flow, no context - just the information you need to remember.

:comparison
.title Chat Capture vs Reference File
.content
Chat capture: full conversation with context, shows how a decision was reached.
Reference file: just the facts/decisions, no reading required - like an index card.
```

---

*Source of truth: [LSV.SPEC.kb.txt](LSV.SPEC.kb.txt) -- this document is a generated view of
it, regenerated when the source changes. The spec, written in the format it specifies.*

Copyright (c) GDFrank - 77NW.net. All rights reserved.

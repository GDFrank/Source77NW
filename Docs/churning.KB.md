# Churning the KB: LSV77 in practice

Companion to [churning.md](churning.md), which describes the author-AI method. This one is
narrower: how the KB's files are actually shaped, which parts of the LSV format they use, and
what that shape does - and doesn't do - for the AI's processing. It is not the format spec;
that is [LSV.SPEC.md](LSV.SPEC.md). This is the subset in daily use, and why.

## Three layers, one syntax

- **LSV** - the syntax. A linefeed followed by `:` opens a record; a linefeed followed by `.`
  opens a field inside it; everything else is value text. Parsed by `LsvRecord.cs`.
- **LsvDoc** - a document built on LSV: a `:DOC` header record, optional definitions, then
  records. Parsed by `LsvDoc.cs`.
- **LSV77** - a *profile* of LsvDoc, the KB's use of it: a small fixed vocabulary of reserved
  records, a handful of field natures, a reference grammar, and the rule that every topic
  file is one.

Nothing in LSV77 adds syntax. It adds conventions on top of two parsers that already exist in
the library, which is the point: the same C# that reads a `.kb` file in a tool reads it in the
AI's head, and a human reads it by eye.

## The parts in use

**A `:DOC` header on every file.** First record, states what the file is:

```
:DOC NAMING how source identifiers are formed
.kind spec
.space Dev
.base BASE
```

`.kind` says how a reader should treat the file (spec, boot, method, did, notes...). `.space`
names the folder it lives in. `.base` names the file whose legal and identity fields it
inherits, so no topic file repeats a copyright or an author.

**Records with a name, fields with one fact.** A record's directive line is
`:<type> <name> <title>`; the name is the record's address. Fields are `.<name> <value>`,
one fact per field, one subject per record. A value that holds two clauses becomes two
fields. Rationale does not ride in a truth field at all - the *why* lives in the dated ledger,
not in the rule.

**Optional typing via `.DEF`.** A record named `<name>.DEF` defines the record kind `<name>` -
the same relation a file extension has to a file - and governs every record beneath it in the
dot hierarchy (`:rule.DEF` governs `:rule` and `:rule.web-note` alike; the narrowest match
wins):

```
:verdict.DEF  a per-file classification during churn; ref = the flag
.field means  text
```

Seven natures cover everything: `text`, `list` (one item per line, order is meaning), `ref`,
`date`, `flag` (presence is the value), `enum`, and `md` - the only nature allowed to hold
prose. Typing is optional richness, never a prerequisite: an undeclared record is still plain
name/value pairs.

**Boxed prose.** `:CONTENT` turns the rest of the file into one text value, closed by `:EOD`.
This is how an entire Markdown document rides inside an LsvDoc without escaping: header on
top, the whole article boxed beneath. The KB's topic files are shaped this way; working files
(jobs, ledger entries, input notes) are not.

**References.** `@space:topic:record:field`, right-truncatable, blank segments meaning "this
document." A ref resolves algorithmically - the folder tree *is* the lookup table: `<space>/`
is a folder, `<topic>` a file in it, record and field found inside. No index to maintain, no
registry to drift.

**Directives.** `@@<tag> ... @@@` blocks are the working markup for questions, notes, and
pointers while a file is being churned. They are described in the root README; here the only
relevant fact is that they never appear in a published file, and inside a `.kb` file they
become records like everything else.

**Two files per topic.** Each topic has an AI shadow, `AI/<space>/<topic>.kb`, fully typed,
and an author-side `<space>/<topic>.md`, which is a rendering of the shadow. Changes land in
the shadow; the `.md` regenerates. Hand-editing the rendering is what creates drift, so the
pipeline forbids it.

## What this does for the AI

**Classification at the first token.** A record says what it is before its content is read.
A paragraph has to be read in full to be classified. When a session boots on a few thousand
lines, the difference between "scan for `:rule`" and "read everything and infer" is most of
the cost.

**Addressability without search.** A ref is a path, not a query. Following
`@Config:BOOT:L7` is a folder, a file, and a record name - three exact matches, zero
inference. The AI does not have to remember where a fact lives; the address encodes it, and
the same address works for a script.

**One fact, one place, checkable.** Because a fact is a single field line, "is this stated
twice?" and "do these two files agree?" become line comparisons rather than a reading
exercise. The audit phase of the churn pipeline depends on this; it would be impractical over
prose.

**Truth without narration.** Truth fields carry no dates, no history, no hedges, no "this used
to be." What loads at boot is only what is currently true. History exists, in the ledger, and
is consulted rather than re-read.

**Sweepable.** Line-anchored typed records mean a trivial script can walk the KB and compile
every open question, every feedback record, every rule of a kind, into one view - then discard
the view. The refinement pass becomes queryable instead of re-readable.

**The same parser everywhere.** `LsvRecord` and `LsvDoc` read a `.kb` file with no
intermediate allocations. A tool, a lint, a future viewer, and the AI all decode the file from
the file - the header and definitions travel with it.

## What it does not do

**Prose is still prose.** An `md` field or a `:CONTENT` box is opaque to the format. A view
can be generated from typed fields; boxed text is only ever copied through. The authored
articles gain a header and an address from LSV77, nothing more - the typing dividend is real
only above the `:CONTENT` line.

**The discipline is paid for by the author.** One fact per field, no rationale in truth, no
asides, no dates: these are constraints on writing, and the AI enforces them during churn.
Composition still happens in free text and chat; the format is where content settles, not
where it starts.

**Small files carry fixed overhead.** A five-line topic still needs its header. The format
earns its keep as the KB grows; it is not the cheapest shape for a note.

**Refs bind to layout.** Because an address is a path, moving or renaming a topic invalidates
every ref to it. The lint catches this, but the coupling is a deliberate trade: no registry
to maintain, at the price of renames being a churn item rather than a free act.

**It replaces nothing about judgment.** A typed record can be wrong exactly as easily as a
sentence can. What the format buys is that the wrong fact is in one place, named, and cheap
to find - so the churn that catches it is short.

## Net

The format's whole contribution is separating *what kind of thing this is* from *what it
says*, at the line level, in plain text. That separation is what makes the KB loadable in
pieces, checkable by script, and addressable by path - and those three properties are what a
stateless collaborator needs most, because it cannot afford to re-read the world every
morning.

---

*Method: [churning.md](churning.md). Format spec: [LSV.SPEC.md](LSV.SPEC.md). Index of all
docs: [README.md](README.md).*

Copyright (c) GDFrank - 77NW.net. All rights reserved.

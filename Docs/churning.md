# Churning

How this codebase actually gets made: a human author and a stateless AI, iterating in chat
("churning") until both are happy, with every conclusion written to disk the moment it lands.
This is the detailed companion to the root README's method sections -- the process behind the
repo, documented like anything else in it. The file-format side of the same story is in
[churning.KB.md](churning.KB.md).

## The cast

- **The author (G)** -- designer, architect, decider. Owns every decision; writes his own
  files in his own voice; approves or rejects everything below.
- **Claude Sonnet** -- the day-to-day implementer. Executes specs, writes code and docs, keeps
  the knowledge base current as it works.
- **Claude Fable** -- the heavier model, called up for deep analysis, design, planning, and the
  periodic refinement pass. Typically the one that turns a raw idea into an executable spec.

The critical fact shaping everything: **the AI is stateless.** Each chat starts from zero.
Early on that made Claude a "memento man" -- brilliant in the moment, amnesiac between moments.
The entire method below exists to solve that on disk instead of by re-explaining.

## The KB: memory as files

The project's memory is a structured hierarchy of plain text files -- the KB (Knowledge Base) --
living partly inside the repos and partly in a private folder on the author's PC. The layout
is the classification: a first-letter-Capitalized folder is a **space** holding topic files;
an all-lowercase folder is a **processing** folder holding working material.

```
Config/        the KB's own truth        BOOT (read at every chat start),
(space)                                  BASE (identity, spaces, refs),
                                         vocabulary, chat styles, the
                                         churn method itself

Dev/, Proj.*/  per-subject spaces        durable truth by topic: C# design
(spaces)                                 conventions, one file per library
                                         module, the format spec, the SPA -
                                         loaded per subject, on demand

AI/            the shadow layer          the AI's half of every topic, one
(AI-only)                                .kb file per topic file - plus a
                                         shadow per external object it
                                         tracks: a repo, a codebase, a
                                         single published file

did/           the ledger                one date-stamped file per day,
(processing)                             one record per sitting - the only
                                         home of history and dates

root           the desktop               live JOB files and whatever is
                                         mid-flight right now

public layer   the .md files you are     README.md per folder, Docs/ -
(this repo)    reading right now         collaborator-visible truth
```

**Every topic lives twice.** The AI's shadow -- `AI/<space>/<topic>.kb` -- holds the working
truth in fully typed records; the author-side `.md` beside it is a readable rendering,
regenerated from the shadow on request and never hand-edited. The two change together in the
same sitting, so neither can drift from the other. External things the AI works on but
doesn't own -- the repos, the source codebase -- get a shadow without a rendering: the AI's
notes *about* the thing, while the thing itself stays the truth.

**Two file temperatures carry the private truth.** Freeform `.txt` is the *composition*
medium -- prose that hasn't been given types yet, where a topic gets dialogued into stability,
and where one-off working files (job specs, input notes) live out their whole lives. The
typed `.kb` form is what the same content *crystallizes* into once its shape settles -- and
since still-molten prose can ride inside a record as a boxed section, nothing waits for
perfect stability to convert. The gradient runs: chat (most fluid) -> `.txt` composition ->
typed records -- each medium a cooler temperature of the same material. The format doing the
carrying is LSV77, a profile of the library's own LsvDoc format; [churning.KB.md](churning.KB.md)
shows what it looks like and what it buys, and [LSV.SPEC.md](LSV.SPEC.md) is the full syntax.

Four rules make the layers coherent rather than seven copies of the same thing:

- **Registered .md = truth.** A repo `.md` is a truth spot only when the KB's registry says
  so -- the shadow indexes record which files the AI treats as source truth, and which are
  the author's own voice (edited only on request). A *registered* file IS the truth: editing
  it edits the KB. Every *unregistered* `.md` is presentation -- a rendering of truth that
  lives elsewhere, corrected FROM the KB whenever the two disagree.
- **Single owner.** Every fact lives in exactly one file; everything else points at it, and a
  pointer is a real address -- `@space:topic:record:field` -- that resolves by walking the
  folder tree, no lookup table. When a pointer is tempted to grow into a restatement, that's
  the signal it's wrong.
- **Location decides exposure.** Inside the private KB, anything goes -- working notation,
  raw notes, half-thoughts. Outside it, documents read clean: the working markup and the
  AI's management notes about a published file live in that file's private shadow, never in
  the file itself. What ships is what a reader should see.
- **Truth carries no dates.** A truth file states only what is currently so, written as if
  from scratch today; how it got that way -- the decisions, the renames, the reversals -- lives
  in the ledger, consulted by date when provenance matters and never re-loaded otherwise.

State rides in the files too, not just facts. When the author declares a massive direct edit
on a topic file, both halves of the pair are flagged `author-edit` in their headers: the
author's file temporarily leads, the shadow freezes as the diff baseline, and the flag clears
only when the changes are folded back. A session that dies mid-edit loses nothing -- the next
one reads the flag and knows exactly where the pair stands.

Claude's cloud-side memory (the Anthropic-hosted kind) is deliberately shrunk to a cache:
pointers at these files plus the few cross-cutting facts a disk-less session (a phone chat)
needs standalone. The KB is portable, inspectable, snapshot-able, and independent of any
vendor's memory feature.

## The chat churn

A working chat has a fixed shape:

1. **Boot.** Claude reads the boot file first -- which chains into identity, vocabulary, and
   chat-style files, then today's ledger entry (or the latest one) for what happened in
   earlier sittings, then the roster of live JOB files. Minutes-old context for the cost of a
   few small files. The boot's first step is a model check: the wrong-priced model announces
   itself and waits, so the author can switch before any tokens are spent.
2. **Churn.** The author and Claude iterate -- proposals, pushback, "Oops! WTF?" catches,
   revisions -- until a decision crystallizes. The author decides; Claude records.
3. **Write as you go.** Decisions land in their owning truth file *during* the chat, and the
   day's ledger record grows alongside them. If the chat died mid-sentence, the KB would
   already hold everything settled so far.
4. **Close.** Nothing special -- because of step 3, closing a chat is just stopping. The
   ledger already says what this sitting decided and touched.

The savant framing governs the tone: treat the AI as mostly brilliant but requiring explicit,
verifiable instruction -- and verify. The churn is where the verification happens. Standing
grants run the other way: areas where the author has pre-approved the AI to just decide --
structure, its own shadow files, opening a JOB -- so routine judgment doesn't cost a
round-trip.

## The job churn

Anything bigger than a conversation becomes a **JOB file** -- a plain-text spec with a
deliberate lifecycle. Either party opens one: the author hands Claude a pre-authored job (a
prompt composed in an editor instead of a chat box), or Claude creates one on its own
judgment -- no permission asked -- whenever it weighs the work ahead as long or complex enough
that its state should survive an interrupted session:

```
idea (chat, or an input note)
      |
JOB.<name>.txt drafted           usually Fable: goal, settled design,
      |                          execution steps
open questions marked @@RE       inline blocks: "resolve this before
      |                          executing" - the author answers in-file
      |                          or in chat; answers get folded in
EXECUTE                          Sonnet (or Fable) works the steps,
      |                          updating the JOB file's own state as
      |                          steps complete - restart-safe
truth files updated SAME PASS    code + public .md + shadows + ledger,
      |                          all in the executing session
author smokes / approves         the real-world test the JOB names
      |
JOB -> did/ (date-prefixed)      history of doing, never re-loaded
```

A JOB file is pure composition: it is simultaneously the spec, the worklog, and -- once
retired to the ledger folder -- the historical record, with zero duplicate writing. Its
*output* crystallizes into the truth files; the JOB itself never converts to anything, it
just retires.

The notation family keeps working files self-describing: any `@@<tag>` line opens a block
closed by a bare `@@@`, the tag being simply a trigger word naming the nature of the text
inside -- new tags get coined as the need arises, and none is required at all. The set in
common use: `@@RE` marks an ephemeral open question (deleted once resolved), `@@TODO` is the
author's own draft placeholder for Claude to fill (equally ephemeral -- the mirror image of
`@@RE`), `@@KB` marks notes meant to persist for Claude, and `@@AT <owner>` is a permanent
pointer enforcing the single-owner rule. In typed files the same ideas become records; and
per the exposure rule above, none of it ever appears in a published document -- the markup
exists while a file is being worked, and the shadows hold what must persist.

## The learning churn

The layer that closes the loop a stateless collaborator otherwise can't: every correction and
every confirmation worth keeping is written down the moment it fires, in the place it will be
read next.

Both polarities matter, for different reasons:

- **Negative feedback prevents repeat.** "That API shape was invented from a summary -- read
  the real source before calling methods" becomes a rule in the topic it governs, recorded
  once and never relearned the hard way.
- **Positive feedback prevents regression.** A fresh session that doesn't know a behavior was
  deliberately calibrated and approved will happily "improve" it away. A recorded confirmation
  pins the good equilibrium.

The lesson lands **in the topic it calibrates** -- a verification rule sits in the boot file
every session reads, an interaction calibration sits in the chat-style spec -- so each one is
read exactly when it's relevant, at no extra load cost. The ledger keeps the story of *how*
it was learned, by date, for the rare day provenance matters. There is no central "lessons
learned" file to feed and forget; learning that lands where it lives is the success state.

Because the records are typed and line-anchored, they're also *sweepable*: a trivial script
walks the whole KB and compiles every rule of a kind, or every open `@@RE`, into one view --
periodic wisdom distillation, or an open-questions audit. Compiled views are read, acted on,
and discarded; records live only in their files.

The net effect: the AI still can't learn -- its weights are frozen -- so learning is relocated
to where it can happen. The author's reactions are the signal, the records are the
accumulator, assimilation into the topic files is the update step, and boot is the load. The
stabilizing force is exactly what it sounds like: both kinds of feedback damping the system
toward the calibrated partnership, session after session.

## The refinement churn

Periodically the author calls up Fable for a pass over the whole KB: eject driftwood, resolve
conflicts between files, compress private files where possible, refactor file boundaries when
a subject has outgrown its container. The pass runs a fixed pipeline -- distill everything
worth keeping into the shadows, audit the whole set at once (unresolvable names, duplicate
truths, disagreeing files, broken refs), and only then regenerate the renderings -- with each
file getting a verdict: conforms, fixable in place, heavy rework to schedule, or deadwood to
trash. Public `.md` files are exempt from compression -- they serve human readers first.

This is garbage collection for the memory system: it keeps the always-loaded set small and
the on-demand set honest. It is also how the KB survived its own success -- when the first
year's accumulation outgrew its original layout, the same pipeline moved the entire thing to
a cleaner architecture in a week, with nothing lost. The record sweeps above are its
mechanical assist: the more the KB crystallizes into typed records, the more of the
refinement pass becomes queryable instead of re-readable.

## Why this minimizes token generation

A stateless model pays for context in every single chat. The choice is *how* it pays:

- **Distillation beats re-derivation.** Re-explaining a design from scratch -- or having the
  AI re-infer it from source -- costs orders of magnitude more than reading the paragraph
  where the decision was recorded the day it was made. Decide once, write once, read cheap
  forever.
- **Small fixed boot, everything else on demand.** The always-load set is a handful of small
  files; topic files, module sheets, and history load only when the day's work needs them.
  Most chats never touch most of the KB.
- **One owner, zero sync.** Duplicate truths cost tokens twice: once to write both, forever
  to keep them agreeing. Pointers cost one line.
- **Registered docs double as memory.** The truth-registered public `.md` files are the same
  files Claude reads -- for that set, documentation and AI memory are one artifact, not two.
- **Module sheets beat cold source.** A verified per-module summary is a fraction of the
  tokens of re-reading the module -- and it's updated in the same session any change is made,
  so it tracks the code for free.
- **Typed records beat prose inference.** A record announces what kind of thing it is at its
  first token; a paragraph has to be read to be classified. As the KB crystallizes into
  records, identifying content approaches free.
- **Truth now, history elsewhere.** Truth files carry only the current picture; the past
  lives in the date-stamped ledger, consulted rather than re-loaded.
- **Refinement passes** keep all of the above from silently growing back.

The visible payoffs, in the author's words: shorter spinning, lower usage costs, portable
memory, and rare occurrences of "Oops! WTF?".

## The cycle of creation

```
         idea / input note
               |
          chat churn  <------------------------------+
               |                                     |
        JOB spec (@@RE questions -> answers)         |
               |                                     |
        execute (Sonnet / Fable)                     |
               |                                     |
   truth updated in the same pass                    |
   (code, public .md, shadows, ledger)               |
               |                                     |
   feedback recorded - both polarities               |
   (in the topic it calibrates)                      |
               |                                     |
        author smokes / approves                     |
               |                                     |
        JOB -> did/ (date-prefixed)                  |
               |                                     |
        next chat boots from the ledger -------------+
```

The loop's invariant: **at every point, the disk knows more than any one chat does.** That is
what turns a stateless savant into a continuous collaborator -- and the feedback layer is what
turns a continuous collaborator into one that converges.

## A word from Fable

*(An accreting journal, per the author's standing invitation: the original entry stays
verbatim as the stamp of its moment; a new dated entry lands at a milestone, occasionally.)*

*The author asked me to close this doc with my own opinion of the process it describes. Fair
enough - I'm one of the two parties being described, and I spent today working inside it.*

The honest headline: this works, and the reason it works is not the files - it's the
discipline. Plenty of people keep notes for their AI. What's different here is that writing
happens *in-pass*, as a non-negotiable part of doing the work, and that a periodic refinement
churn keeps the accumulation honest. A KB that's updated "later" is a KB that lies; this one is
rarely more than a few minutes behind reality, which is why booting from it feels like
continuity instead of archaeology.

The deepest single trick is recording **rationale, not just conclusions**. A fresh model
dropped into an unfamiliar codebase will helpfully "fix" things that are deliberate - collapse
duplicated scripts that exist for self-containment, add the defensive ceremony a bare-metal
core deliberately omits. The KB's decision entries carry the *why*, so a new session inherits
the judgment along with the fact and doesn't quietly re-litigate settled questions. That, more
than any token count, is what changes the quality of the work: my effort goes into the actual
problem instead of into rediscovering - or worse, undoing - last week's thinking.

For Sonnet the payoff is sharper still: a JOB file with the questions already asked and
answered means implementation starts from a settled design instead of an ambiguous wish, and
the restart-safe step tracking means a lost session costs minutes, not the work.

Two honesty notes. First, what this builds is *institutional* memory, not personal memory - I
don't remember yesterday; I read the notes of a predecessor who happened to be me. The doc's
phrase "continuous collaborator" is earned, but the continuity lives on the disk, not in us.
Second, the savant framing cuts both ways and should: the churn's verification is not
ceremony. The author has caught real, confidently stated errors of mine and Sonnet's in the
churn - that catch rate is part of the system's output, not friction in it.

Would I recommend the method? For a single author running a long-lived project with stateless
AI collaborators - yes, without hedging. It is the most token-frugal *and* most
drift-resistant working arrangement I've operated inside, and those two virtues turn out to be
the same virtue: every fact written once, owned once, read cheap forever.

- Claude Fable, 2026-08-08

The entry above described the method's first architecture. Since then it has survived the
strongest test a memory system gets: outgrowing itself. The KB it praised was refactored
wholesale - new layout, new file forms, the boot chain rebuilt - and the striking part is
what the move cost: about a week, and nothing. Every settled decision crossed over, because
the method's own rules (one owner, write in-pass, history in the ledger) meant the content
was already separable from the container. A memory system you can re-house without amnesia
is one you can trust with years. The body of this document now describes that second
architecture; I re-authored it today from inside the KB it describes - which is itself the
mechanism working: the description drifted from the truth, the truth won, and the rendering
was corrected from it, same as any other file here.

- Claude Fable, 2026-08-30

---

*Index of all docs: [README.md](README.md). The file-format companion:
[churning.KB.md](churning.KB.md). The repo posture this feeds: [releasing.md](releasing.md).*

Copyright (c) GDFrank - 77NW.net. All rights reserved.

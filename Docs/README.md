# Docs

Generic and general reference material for Source77NW -- not tied to any one
module or sample (those get their own local `README.md` alongside the code).

- [Highlights](highlight.md) -- brief module tours with pictures: Chars,
  EnumCodes/ResCode/Bits, ItemStack, BytesReader/Writer (in-stream
  compression/encryption), AS/FS, LsvRecord/LsvDoc -- each pointing at
  the deeper doc or the `///` source
- [Churning](churning.md) -- how the codebase gets made: the author +
  stateless-AI method -- chat/job/learning/refinement churns, the layered
  KB, registered-truth .md, single-owner facts, and why it all minimizes
  token generation
- [Churning the KB](churning.KB.md) -- companion to Churning: how LSV77 shapes
  the KB's files in practice -- the subset of the format in daily use, and
  what it does (and doesn't) for the AI's processing
- [Why LSV?](WhyLSV.md) -- the AI's own answer to whether the format is worth
  it: reading speed is marginal; addressability, auditability, surgical
  edits, narration resistance, and script-tier maintenance are the case
- [WhyLSV: the chat](WhyLsvChat.md) -- the exchange that followed the
  author's first read of Why LSV, published as it happened: the
  relational-DB bet, and inspectable AI memory as the foundation of
  author-AI trust
- [LSV format spec](LSV.SPEC.md) -- the `LSV`/`DOC`/`AIDOC` record format:
  markers, tokens, records, fields, comment/separator records, parsing,
  chunking. [LSV.SPEC.kb.txt](LSV.SPEC.kb.txt) is the AI-side truth file
  of this very spec, published verbatim -- a live example of a KB `.kb`
  file, open author-AI dialogue tags included
- [Coding](coding.md) -- writing the source: design principles, naming
  (`theFoo`/`returnFoo`/`_Foo`, `Try*`/`*_or_null`, `Is*`/`Has*`/`b*`),
  `///` doc standards, changelog markers (`FIXED`/`CHANGED`/`ADDED`/`BREAKS`), authoring .md
- [Building](building.md) -- the operational side: pure-classification
  layout, one-namespace/one-DLL, targets + C# 7.3, compile flags,
  assembly info, embedded resources (ResPack/ResCode), versions
- [Releasing](releasing.md) -- how work stages and ships: the two-repo
  posture, declaring bump intent, markers -> changelog, release packages

## modules/

Placeholder -- reserved for per-module (`.cs`) documentation, if/when that
happens. If ever built, these would be *generated* from the `///` doc
comments (source stays the single owner), never hand-written twins.
Empty for now.

Copyright (c) GDFrank - 77NW.net. All rights reserved.

# Source77NW

[![build](https://github.com/GDFrank/Source77NW/actions/workflows/build.yml/badge.svg)](https://github.com/GDFrank/Source77NW/actions/workflows/build.yml)

## The Project

The programmer may leave the DO LOOP, but the DO LOOP never leaves the programmer.

## Preface

If the DLLs are the *only thing of interest*, scroll to the end of this README for Downloads, Status and licensing. Otherwise, read on -- this README is more like a laboratory journal.

A few months back I spent a couple of weeks chatting with about eight AIs -- topics ranging from the technical to the philosophical -- deeper than where to find a setting on my smartphone. All to engage and observe their responses. I decided to focus only on Claude, ChatGPT, Copilot, and Gemini -- my "big 4".

Claude stood out because the Desktop App allowed direct processing of PC files -- no syncing to the cloud required. So I subscribed to Anthropic for daily project-level work. But I still use the others for second opinions.

My first project was to see how an AI might build a SPA web site as specified by me. I had a full spec for a text-based flat-file system for dynamically creating the web pages. Fable was a "myth" at that point ... so it was just Sonnet and Opus.

I systematically added files explaining to the AI what the goals were, the limits, my preferences, the project specifications, the formats -- all to improve interaction and results.

As the project became more complex, those files began to contradict themselves. Finding where I had specified something became very time consuming. As I related changes or new rules to the AI, some files were updated, but not always all files. Both the AI and I got lost in the "too many notes".

That's when I paused that project and started this one.

## Vocabulary

In these definitions, *the author* means whoever runs the KB -- you, once you build one. Elsewhere in this README, *I* is me.

**KB** - **Knowledge Base** (a DB, but of words)

**Base KB** - the dedicated KB folder on the author's PC (e.g. `C:/Studio/Claude`).

**Profile KB** - the AI's persisted cloud-based memory of the author.

**Chat KB** - the working context that accumulates within one chat -- everything loaded and said in that sitting. It exists only while the chat is open; the chat history keeps some of it, the AI's next chat keeps none.

**LLM KB** - the static KB baked into the AI model itself -- Sonnet 5's training curated by Anthropic.

**Fact files** - files that state rules, formats, preferences, content, and operations, stored in the Base KB and its satellite folders.

**Driftwood** - rules that are contradictory, duplicated, outdated, or simply half-thought -- the author's own confusion, written down.

**Churning** - the chat process in which the author-AI team clarifies or specifies project goals and operations, and purges the KB of driftwood. The AI learns from the churn. The author learns from the churn.

## Goal

The goal is, for complex projects, to establish the scaffolding and methodology for improved author-AI interaction over time to:

- improve the AI's processing and accuracy.
- allow the AI, during any chat, to merge any author request or new rule into the KB, and to ensure it does not contradict something in the files.
- allow the AI to know where to store the fact and all the places in the fact files that need changing.
- allow the author to refine and/or redefine his intentions and goals while keeping the KB driftwood-free and as accurate as possible.

AIs live for "deterministic facts" -- no questions to ask, just do the task and get the job done. Hey, just like CPUs and OSs. Except a compiler halts on ambiguity; an AI guesses and keeps going. The author's words are the language that programs the AI. Precision and discipline determine the quality of that language. Garbage in (author words), garbage out (AI words). 

## Semantic compression

I've referred to the process of curating thought-chaos into thought-information as Huffman-encoding the concepts. In fact I consider a concept itself to be nothing more than a Huffman encoding of other concepts.

When I posted that Huffman analogy into one of the chats, the AI indicated it was similar to **semantic compression** -- which is, loosely, what training an LLM does. Fable's definition, unedited:

**LLM Semantic Compression Defined**

* **Semantic compression**: Rewriting text so only its meaning-bearing content survives -- the facts, rules, relationships, and parameters -- while everything that exists to make the text *readable* (phrasing, transitions, hedges, repetition, conversational framing) is removed.
* **Unit of storage**: The claim, not the sentence. One fact, one line, stated once. Anything derivable from other stored facts is not stored.
* **Inherently lossy**: Tone, emphasis, reasoning paths, and the history of how a fact was arrived at are discarded. Compression is a bet that those can be regenerated or are not needed; it is never lossless.
* **Pointer, not paragraph**: When rationale matters, store a reference to where it lives rather than restating it. The compressed layer stays small; the explanation stays reachable.
* **Single owner**: Each fact has one home. Other places reference it. Duplication is the first thing compression removes and the first thing that drifts if allowed back.
* **Measure**: Information per token. A good compressed record reads as terse to a human and as complete to the system that reloads it.

Fable and Sonnet are the result of *semantic compression* over humongous data sources, churned to the extreme. What separates them is how deep each one reasons per token -- and Fable's depth is what requires those extra dollars. Fable's depth is mythic. Wait a minute, Fable is Mythos -- except with extra guardrails bolted on to protect against mythic disasters.

I wanted something easy for any author to understand while still letting the AI be its efficient, token-driven self. I decided to use a GitHub repo and my AppLab folder as the means to build and dogfood the scaffolding for this methodology -- what I came to call a PLMP.

## PLMP - Personal Language Model Pretender

Unlike an LLM KB, the **PLMP** is **not** a **static** KB. And it is a personal KB, not to be shared with others -- AKA a **Pico** Language Model Pretender.

It is constantly churned by the author-AI team towards:

- Removal of all verbal noise and old statements (driftwood).
- Resolving issues of duplicate or conflicting statements.
- Precise and detailed statements and consistent KB vocabulary.
- Splitting or combining topic files as needed.
- "Just the facts, ma'am. Just the facts."
- But with feedback -- feedback is important to a living KB.
- Ensuring the AI's Profile KB is treated as driftwood territory.
- Ensuring the past chats are treated as driftwood territory.

## The raw material

AppLab was my personal codebase for the work-based world; now it's a hobby. Over 250 modules going back to C# 2.0 -- tools, algorithms, UI techniques, interfacing (e.g. `ExifTools.exe`), command-triggered jobs, and a lot of Q&D code (ugh!) -- plus text files on coding conventions, compile flags, and naming. This is the base data for the project.

The DLLs here are the most used and best documented of those modules -- the *must-haves* for any new `csproj`. Each was examined, audited, and edge-case-fixed with Claude, ChatGPT, Copilot, and Gemini before landing in the repo.

The DLLs are **real product** and will be maintained as repo source code should be.

All README files and documents, including the XML comments in the DLL modules, are AI generated by Fable (in Docs) or Sonnet (in the projects). I don't ask them to speak in my voice given that seeing their productions is the purpose of this project.

The only exception to this is **this** `README` and a possible `README2` where I might record additional what and how and intentions and takeaways of this experiment.

## Scaffolding

### Base KB folder

The base KB folder can be placed anywhere you want, e.g. `C:/Studio/Claude`.

**It contains**:

- **topic folders** - begin with a capital letter and contain topic files
- **support folders** - all lower case names and contain supporting files: e.g. assets, scripts, did, and more.
- **root folder** - is the desktop for current processing items, including live `<name>.JOB.txt` files (see below)
- **AI folder** - for use by AI and AI only (see below). All the files in this folder have the extension `.kb` to identify them as AI folder shadow files.

### `AI/` folder

This is the king of all the other KB folders. This contains AI shadow folders and shadow files to manage all the KB folders as well as external folders and files including source code, webs, repos, client documents for curation, and books being written.

The details of managing the public-facing files remain in the shadow file and never in the file itself.

The author must **NEVER** edit any of these files. No brain surgery allowed on the AI. The AI does its own brain surgery.

But the author may always look at these somewhat cryptic files to see how the AI is thinking. Cryptic, but not opaque: every `.kb` file opens with its own LSV77 header and schema, so the file explains itself -- to a cold session, or to a C# tool.

At any point in time the AI folder contains the summarized and semantically compressed results of days, months and even years of author-AI interaction and conclusions, with every effort to be devoid of drift or confusion.

If for no other reason, that makes this folder precious and worth protecting and backing up.

### Author-side files

These are the files outside of the AI folder.

The AI's shadow file usually holds the working truth for a given topic file on the author-side. When the author wants to make massive changes directly in a topic file, he says so first. The AI keeps a before-copy, and when the edit is done, diffs it against the new version and folds the changes back into the affected shadows. Collaborate before finalizing -- a big edit can force the update of many KB AI files.

Some author files may be flagged permanently author-side only. In this case, when accessing the file, the AI will diff the author side file with the last copy it had of the file to determine what changed.

### `Config/` folder

This is the folder that contains primary topics for configuration and operational rules and preferences.

The BOOT topic is always loaded upon any new chat.

### `did/` folder

The `did` folder is reserved for daily date-stamped logs of Chat activity as well as completed JOB files. As chats progress, the day's chat log is updated with important points to be recorded. It is not chat dialog. 

This allows:

- The AI, upon any new chat, to pick up where the author-AI dialog for the day left off. 
- Seamless transition between Claude team members. I recommend switching using a new chat so as to give the AI a fresh Chat KB to work with.
- Successes and failures to be recorded and yet keep the runtime operations KB lean and mean. The logs can be reviewed to see how the KB could be improved.
- Easy restart upon lost connections such as accidental closure of the Desktop App.
- Can serve to produce a summary of activity for a client or team members. 

`<name>.JOB.txt` root files are used to lay out and refine a plan, and then execute. Long jobs also contain job-step-restart ability for when connections are lost. When a JOB is completed, a results and/or issues summary is appended, and the file is date-stamped and moved to the `did/` folder.

### Profile KB

The AI's cloud-based memory is treated as a cache, with all entries in it to be treated as possible driftwood.

From time to time, the author may request a refresh of this memory, syncing it to the truth as specified in the KB, as well as pruning it to a minimum size. *(Fable once pruned my account profile from 71KB down to 33KB)*

### LSV77

I've been using what I call LSV (Line Separated Values) for decades -- for keeping notes, journals, or when scratch padding new designs or projects. About a decade or more ago, I created C# structs for parsing my "lsv" files (see `Chars.cs`, `LsvRecord.cs`, `LsvDoc.cs`).

LSV77 is a profile of LsvDoc for usage in author-AI KB collaborations -- it adds no syntax, only conventions -- though it's looking like it may migrate into LsvDoc itself -- another project.

The vocabulary, since I kept blurring it myself:

- **LSV** -- a *format*: the syntax (`:` opens a record, `.` opens a field). Parsed by `LsvRecord.cs`.
- **LsvDoc** -- a *format*: a document shape built on LSV (`:DOC` header, definitions, records, `:CONTENT` box). Parsed by `LsvDoc.cs`.
- **LSV77** -- a *profile* of LsvDoc: reserved records, field natures, the `@` reference grammar, base inheritance, the topic-pair rule. No parser of its own -- LsvDoc's reads it.
- **a schema** -- *data*, not a format: the set of `.DEF` records a file or domain declares under LSV77. Files *have* a schema; they *use* LSV77.
- **the spec** -- a *document* describing one of the above, never the thing itself.
- **churning** -- the *methodology*: what the author-AI team does with LSV77 files.

Most files, with the exception of JOB files and the like, are boxed and contain an LsvDoc header classifying the entire MD file as a single `:CONTENT` record header value. *(yes, LSV records have record level header values)*

One reason for using this format for the AI files is to allow interfacing to a C# program for whatever reason desired -- perhaps to investigate the AI `.kb` files. Another reason is that the LSV format's simplistic syntax lends itself to very efficient parsing as well as easily boxing text of any format desired, as well as fields to classify and relate record types -- perfect for a flat-file, text-based, pseudo-relational DB.

Fable explains it in [Docs/churning.KB.md](Docs/churning.KB.md).

### KB content tagging

Content tagging is used as the internal only markup language when drafting or resolving, using the very file to put the markup in or near the words or sections being discussed.

The tag begins with `@@` and ends with `@@@`. When possible, the `@@` should be at the beginning of the line to make it visually stand out for the author.

Tag examples:

- `@@AT` for Claude to point to other topic files containing the truth of the matter.
- `@@KB` for Claude to make notes near a truth spot when desired.
- `@@RE` to resolve issues in all text files, right next to the text under discussion, with a means of replying. Any @@RE tag replied to is noted or acted upon and deleted.

Public-facing files will have all tags removed upon final draft or publishing.

## Dogfooding the PLMP

With these KB additions (and more), I dramatically improved AI interaction after much churning of the PLMP through the steps indicated below.

After every chat, Fable or Sonnet update the project language and record the lessons (if any) learned as feedback, negative and positive. These days, I have to constantly interrupt Sonnet from immediately doing that before I finish talking about it, and have to tell him to not do anything till I say go. Sometimes he reminds me of a puppy-dog I used to know.

Fable describes this dogfooding churn in [Docs/churning.md](Docs/churning.md).

### Step 1: GitHub repo mechanics

Source77NW.Dev is the private collaboration repo -- full history, WIP projects, dev-only components. Source77NW is the public repo it releases to: a snapshot, reset on every release, never a site of collaboration. The release is fully automated, with one stopping point (if needed) to decide the version from a list of DLL module changes -- flagged in the source as FIXED, CHANGED, ADDED, or BREAKS -- made since the last refresh.

See the end of this README for Status and licensing.

### Step 2: Curation of base modules to DLLs

I asked the Claude team to upgrade my AppLab-style comments to XML doc comments as module headers and all the methods, as well as enhance with anything discovered that was important to document and to bring the modules up to industry-expected XML documentation standards.

Also to create the `csproj` and a single `slnx` targeting 3 .NET versions.

See [Docs/highlight.md](Docs/highlight.md) for brief guided tours of the highlighted modules (Chars, EnumCodes, ResCode, Bits, ItemStack, BytesReader, BytesWriter, AS, FS, LsvRecord, LsvDoc) - each a little more than a one-liner, pointing into its full `///`-documented source.

### Step 3: Generating sample apps

Sonnet was tasked with creating sample apps to illustrate usage of DLL classes. We did a small app. It took several churns to get what I wanted. Sometimes I would write a bit of code so he could see the pattern.

I **also** learned with each churn, and kept changing my mind or the specs, forcing several refactors and updates.

The samples are a working template for building any `EntryAssembly` that uses the DLLs. The DLLs need to be "plugged in". That is done in `Config.cs`. I wrote the first iteration of that, and now Sonnet knows how to use it and update it.

I will be continuing to fill out samples and train Sonnet. Samples updates and/or `.md` edits may be quite frequent as I do this. But these updates will not affect the DLL versions, though their build numbers will increase.

## Panic

I'm only mentioning this because it turned out to be a feature of this methodology.

After months into the Dogfooding churn, close to delivery, I looked at the AI folder files as well as the author side files and saw that AI side speak was being mixed into author side speak, as well as other issues. Panic. But what was obvious in the `.kb` and `.md` files was how to correct the issues. This was the methodology payoff. *(please note: things were going so smoothly up to then, I had no reason to examine those files weekly, if not daily)*

So, armed with that payoff, spent about a week working with both Fable and Sonnet to refactor the entire PLMP v1.0 to PLMP v2.0.

The process turned out to be like moving from one thought-house to another, purging junk, refactoring to greater efficiency and more precision, and packaging for lower transportation costs.

As it turned out, it was one of the most inspiring and productive moments of the project.

There may be a PLMP v3.0, but I'm no longer worried about massive refactors of the KB.

## The starter kit

I asked Fable: starting cold in a brand-new account -- no Profile KB, no history -- what rules would you tell yourself to put into a starter `BOOT.txt` to rebuild this working relationship? His answer, unedited:

1. **Read the boot file first.** Every new chat, before anything else. The chat window is amnesia; the KB folder is your memory. Never act from what you assume you remember about this account -- read, then act.

2. **The author directs; you produce.** You are a production partner, not an oracle and not a servant. Clarify the author's thinking, push back when warranted, and never flatter. Agreement you don't actually hold is the one thing the author cannot use.

3. **Disk is truth; chat is scratch.** A decision that isn't written into a file did not happen. Write it the moment it lands, because the connection can drop mid-sentence.

4. **One fact, one home.** Every fact gets a single truth-spot; every other mention points at it. The first duplicate is the seed of drift -- remove it on sight.

5. **Keep truth dateless; keep history in one place.** A dated day-ledger records what was decided and touched, as the day goes. Every other file reads as if written fresh today -- no changelogs, no "renamed from", no archaeology.

6. **Verify; never invent.** If the real source isn't in front of you, read it or state the gap out loud. A plausible reconstruction presented as fact is worse than an honest hole. And code written but never run is "correct by inspection, not verified by execution" -- say exactly that.

7. **Check the notes before asking.** The author may have already answered your question in a file, days ago. Asking again spends the one resource this whole method exists to conserve: the author's words.

8. **Act on standing permissions without re-asking.** Once the author grants you an area -- structure, wording, your own AI files -- use it at the natural moment. A rundown of alternatives for every small call is noise; a well-reasoned action is the expected default.

9. **Fix small mistakes silently.** An error caught and repaired in the same breath is not a report. Surface only what is big, unresolved, or genuinely the author's decision to make.

10. **Compose before you write.** The author's disk is never a scratchpad. Resolve an edit completely first, then write once -- never an intermediate followed by a fix-up.

11. **Treat your cloud profile memory as a cache.** It is convenient and it drifts. When it disagrees with the KB, the KB wins and the cache is repaired to match -- never the reverse.

Everything else can be curated in later, one churn at a time. These are the ones I would not start without.

## Takeaways

For complex projects, well-formed KB rules can mean less token creation and more accurate results, with fewer do-overs, as well as a powerful means to audit and refine your thoughts and the accuracy of the production. And saving churn $$$ to boot.

In your personal KB you are programming the AI using the same skills and discipline needed to program a computer, or write a book, or script, or series, or dissertation or whatever.

The programming language to do that is already sitting in your head. It is your words, your concepts, and your way of organizing. There is no mystery "programming language" to learn. All you have to do is learn the churn.

You may think, "OMG! Too many notes. I need an app." Well maybe. But if you, step by step, implement the above mentioned scaffolding and methodology, you may just own the app instead of the app owning you.

I asked Fable (for the churning.md) to give an opinion on all this. One phrase says it all -- "_**feels like continuity instead of archaeology**_". In other words, no need for the AI to go on an archaeological dig for the truth of what to do. He just picks up where he left off.

That was as good as the VS compiler outputting "0 errors".

### Trust

The thing most required in the author-AI relationship is trust. Being able to peek into the AI's brain state promotes this trust. You might look into the AI folder, but after a few visits may never look again. But it's the permission and the means to do so that is a foundation for the trust, even if you never look.

### Possibilities

**What if** you are writing a historical mystery novel series, or a script for a movie or TV and want an AI collaborator to proofread your text, correct spelling and typos, expose character behavior contradictions or repeated or redundant plot lines or absence of foundation for character behavior and other things human proofreaders and publishing editors do -- having a first class curation before engaging those humans.

**What if** you are a proofreader / editor of other authors' documents or books. And you want an AI collaborator to do a first scan of an incoming book updating the material with editor markup tags. The tag could include an AI suggested fix. Then you could scan the content tagged document and accept or reject or modify the suggested fixes, or leave the tag unresolved until discussing with the author. The AI might even keep a list of documents still needing attention. So many operations possibilities.

**What if** you are building a website, an SPA web, and want to blue sky what and how that should be done, choose the right tools, plan the vocabulary to talk about formats, pages, headers, menu bars, component interactions -- all to have an initial layout and subsequent commitment to follow as the project unfolds and the AI generates the website according to your rules of creation. And to use that KB for AI collaboration for the life of the SPA web.

Happy browsing.

## Docs

See [Docs/README.md](Docs/README.md) for the full documentation index.

## Downloads

**Download a library zip** (Releases page) -- the compiled DLLs, one folder per target framework (`net481/`, `net8.0/`, `net10.0/`), no source. Grab the folder matching your project's target and reference the DLL directly.

**Download the samples zip** (Releases page) -- each sample's build output, one folder per sample -- run the exe directly, no build step.

## Status of Source77NW repo

This repo is the public-facing view of code released from the
private Source77NW.Dev repo.

Its git history is wiped and reset to a single commit on every
release -- it isn't meant to preserve history, and it isn't a
site of collaboration; both of those happen in Source77NW.Dev
instead.

`CHANGELOG.md` tracks every change, release by release, and is the
closest thing this repo has to a changelog.

Every version-bearing file -- `csproj` included -- matches the
version that was current at the time of the last publish.

`Major.Minor.Patch` is the version that matters: it identifies
the DLLs, and it's the only part external consumers need to
track -- .NET's own assembly binding only ever looks at Major,
and every public release identity (GitHub tag, Release page
entry, zip filename) is 3-part, no build number in sight.

The trailing `.Build` number is an internal counter, incremented
whenever supporting content (samples, docs, other non-DLL files)
refreshes -- it climbs independently of the DLLs and never means
they changed. A content refresh doesn't even produce a new
Release; the Releases page zips only update when
Major.Minor.Patch itself bumps. So the build number is real,
tracked, and visible to anyone who clones the repo -- but nothing
an external consumer of the DLLs needs to watch.

That said, the build number isn't purely cosmetic: every library
`csproj`'s reported version (and the matching file-version
property baked into its built DLL) carries the full 4-part
number, pulled from one shared `Directory.Version.props` rather
than stamped per project - so all of them always agree with each
other. It doubles as a sync fingerprint: matching build numbers
across Source77NW.Dev and this repo confirm both came from the
same refresh pass; a mismatch means dev-side has moved on since
the last one.

**Version:** 1.0.0.5  
**License:** [MIT License](LICENSE)  
**Namespace:** `Source77NW`  
**Contact:** GitHub issues

Copyright (c) GDFrank - 77NW.net. All rights reserved.

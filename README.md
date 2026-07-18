# Source77NW

[![build](https://github.com/GDFrank/Source77NW/actions/workflows/build.yml/badge.svg)](https://github.com/GDFrank/Source77NW/actions/workflows/build.yml)

**Performance-focused C# utilities for Windows — 20 years of refinement, curated for release.**

Zero-allocation parsing, minimal heap motion, explicit readable code. Developed and production-tested in a working application environment since ~2005, now presented as a formal library.

## Targets

One project, three outputs:

| TFM | Why |
|-----|-----|
| `net481` | The final .NET Framework — native on Windows 11 |
| `net8.0` | The most-deployed .NET LTS |
| `net10.0` | The current frontier LTS |

Sources are C# 7.3 baseline with `#if` blocks carrying per-target differences — the same files compile on all three.

## Philosophy

- **Zero-allocation** where possible; allocation is a decision, not a habit
- **Bare-metal cores, wrapped exposure** — core types (`ItemStack`, `TextBuilder`) are single-threaded, lock-free, and fail loud on misuse; safety, validation, and softening live in agent wrappers (`StackAgent`), never fattening the core
- **Soft semantics for data** — absence/undefined is a first-class state, not an error; `Try*`/`Got*`/`Did*` surfaces instead of exception-driven flow
- **Explicit over clever** — direct mnemonics, sealed-by-default classes, conservative feature adoption

## Highlights

- **Chars** — zero-allocation char scanning over strings: parse command lines, CSV, paths without intermediate string churn
- **ItemStack&lt;T&gt;** — one lightweight structure serving stack, queue, and deque patterns via a sliding-window buffer; `StackAgent<T>` is its public-facing wrapper
- **Issue** — a single exception type carrying a user-friendly message plus a forensic trail (source module + spot) so dialogs stay humane while logs pin the exact raise site
- **LSV** — Line Separated Values: a minimal line-oriented data format (`LsvRecord`, `LsvDoc`)
- **BytesReader / BytesWriter / TextBuilder** — allocation-conscious binary and text I/O
- **FS, FileAttr, FileExt, Zipping** — file-system utilities with BOM/encoding detection and Issue-based error handling

## Structure

```
src/Source77NW/           the library (one project, all sources)
tests/Source77NW.Tests/
samples/                  runnable demos - learn the API in place
```

## Conventions (reader's key)

- `theFoo` primary input param, `returnFoo` out param, `_Foo` private backing field
- `Try*` returns bool + out; `*_or_null` / `*_or_default` name their failure mode
- Booleans read as assertions: `Is*`/`Has*` properties, `b*` locals

## Status

Curation in progress: visibility pass (internal → public) and XML documentation are being applied module by module.

**License:** [MIT](LICENSE)
**Namespace:** `Source77NW`
**Contact:** GitHub issues
"# Source77NW" 
"# Source77NW" 
"# Source77NW" 

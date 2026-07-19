# EnumCodes — one enum is the whole UI

A small WinForms app whose **entire user interface — menus, items,
toggles, a radio group, toolbar buttons, shortcuts, tips, icons, and
the dispatch that answers every click — is declared by one enum.**
No XML, no designer files, no string keys, no per-control handlers.

Open `Cmd.cs` first: that enum *is* the application's truth table.
`MainForm.cs` reads it once at startup and builds everything.

## The binding chain

```
Cmd.Edit_Cut
  [EnumInfo("|cap Cut|mnu t|tip Cut selected text"
          + "|ctrl ctrl-X|icon edit.cut.ico|", CodeDefId.action)]
   │
   │  EnumCodes.ForType(typeof(Cmd))     reflection ONCE, validated,
   ▼                                     cached singleton
index i  ⟷  Cmd value  ⟷  ByteCode       (1:1, throw-validated)
   │
   │  ResCode.For(code).Val(CodeValId.…)
   ▼
cap / mnu / but text, tip, ctrl keys, icon stream name
   │
   │  ToolStripItem / Button with .Tag = i
   ▼
click → (int)Tag → Slots[i] → dispatch    O(1), zero searching
```

## What each piece contributes

**`EnumInfoAttribute`** on a member carries one VBAR string in `Chars`
`PluckedDelimitedText` format. The field names are the `CodeValId`
tokens themselves — `cap mnu but tip ctrl icon echo blob` — so the id
enum and the text format share one vocabulary, and lookups are
`Chars` views over the attribute string: no substrings, no parsing
tables, adding a field kind is adding a `CodeValId` member.

**Defaulting** means bare members still work: `cap` falls back to the
member's `NameAsCaption` (`Edit_Cut` → `Edit Cut`), and `mnu`/`but`
ensure an `&` accelerator — `|mnu t|` marks the *t* of *Cut* → `Cu&t`.
Language packs and resource managers are pure overrides registered
via `ResCode.Register`, with zero changes to the UI build.

**Structure** comes from the first `CodeDefId` in each member's Tags
(`menu` / `action` / `toggle` / `radio`) plus order: a menu member is
followed by its children; a radio is followed by its toggles and
displays between separators.

**Associations** live in the Tags too: the radio lists its member
toggles *in its own declaration* —

```csharp
[EnumInfo("|cap Text Size|…|", CodeDefId.radio,
    Cmd.View_Small, Cmd.View_Medium, Cmd.View_Large)]
View_Size = 8,
```

— so who-belongs-to-what is table data, greppable at the source. The
form wires the behavior (check one, clear siblings) from that list.

**The logic table** is one index-aligned array, `Slots[Cmds.Count]`:
def kind, radio owner, live menu item, optional button. The index is
the join key everywhere. Every control's `Tag` is its index; **one**
shared click handler does `Tag → Slots[i]` and dispatches — no
dictionaries, no name lookups, no event maze.

**The dispatch** switches on the enum value — the code acting as a
message id. Grep any `Cmd` member and you find its declaration, its
wiring, and its behavior.

**ByteCode** is the persisted reference (index and name are
runtime-only): the app saves checked toggles as raw ByteCodes with a
leading `EnumCodesAttribute.Version` byte, and reloads through
`IndexOfByteCode` — a *direct array element* here, because `Cmd`'s
ByteCodes are dense. Bump `Version` and stored state self-invalidates.

**Icons** are *names*, never paths, resolved through registered
stream providers (first-found-wins, exception-isolated): provider 1
serves real files from an `icons\` folder beside the exe; provider 2
synthesizes a letter tile so the sample shows images with zero
committed binaries. Drop real `.ico`/`.png` files into `icons\` and
they take over — no code change.

**Ctrl sequences** (`ctrl-X`, `ctrl-shift-S`) are a `Chars` pluck
loop away from WinForms `Keys`.

## Why it's fast

- Reflection and validation exactly **once** per enum type;
  singletons are cached by type handle with lock-free reads.
- Uniqueness of values, ByteCodes, and names is throw-validated at
  first touch (LOUD) — runtime lookups need no guards.
- Every interaction is `Tag` int → array index: **O(1)**. The only
  binary searches run at startup binding; the ByteCode lookup is
  direct-indexed when codes are dense.
- Attribute text is read through `Chars` views — no intermediate
  strings until a control property actually needs one.

## Running

Windows only (`net8.0-windows`; `EnableWindowsTargeting` keeps the
solution *building* everywhere):

```
dotnet run --project samples/EnumCodes
```

Check some toggles, restart, and watch them come back — that's
ByteCode persistence. Featuring: `EnumCodes`, `EnumCodesAttribute`,
`EnumInfoAttribute`, `ResCode`, `CodeValId`, `CodeDefId`, `ResKind`,
`BytesReader`, `Chars`, `Issue`/`ExitId`.

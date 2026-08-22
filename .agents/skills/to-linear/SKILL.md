---
name: to-linear
description: Break a plan, spec, referenced issue, or current conversation into approved Linear milestones and tracer-bullet tickets in a confirmed project, with native dependencies, Backlog status, and focused labels.
disable-model-invocation: true
---

# To Linear

Create narrow, end-to-end Linear tickets. Every ticket belongs to the confirmed project and a native milestone. Mark only agent-suitable work with `Agent`.

## Process

### 1. Confirm the destination

Before analysing or drafting tickets:

1. List all available teams with the Linear MCP server and ask the user to choose one.
2. List that team's projects and ask the user to choose one.
3. Repeat both names and obtain explicit confirmation.

Never infer the destination. Every ticket in the run uses the confirmed team and project unless the user restarts selection.

### 2. Resolve the source, context, and existing tickets

Resolve exactly one **primary source** for scope, in this order:

1. An explicit source supplied with the invocation or explicitly designated by the user for this run: a file, issue identifier or URL, or pasted plan/spec. Fetch and read the full body and comments of a referenced issue.
2. Otherwise, the most recent settled plan or spec in the current conversation. If it points to an artifact, fetch and read that artifact fully.
3. Otherwise, root `PLAN.md`, when present; read it fully.
4. Otherwise, stop and ask the user for the source.

An explicit source wins even when `PLAN.md` exists. State which source was selected. Do not merge multiple plausible sources into new scope; ask when source selection is ambiguous or when a contradiction materially changes the intended outcome.

Treat `CONTEXT.md`, applicable `AGENTS.md`, referenced sources, relevant ADRs, designs, code, and tests as supporting context. Read them when they affect ticket accuracy. `CONTEXT.md` is authoritative for domain language, boundaries, and durable decisions, but supporting context never silently expands the primary source.

Preserve provenance when the primary source has a stable reference. If it is a Linear issue in the confirmed team and project, propose a native parent relationship. Otherwise include the reference in each new ticket's optional `Source` section. Never modify or close the source issue unless the user explicitly approves it.

For broad work, use subagents for bounded, independent, read-only exploration such as separate module investigations, prior-art searches, or overlap checks. Continue other independent analysis while they work, then reconcile every result before drafting. The primary agent owns source selection, the complete proposal, user approval, and every Linear mutation; never create or edit Linear items concurrently through subagents.

After destination confirmation and before drafting, list the project's milestones and every issue whose team and project both match the confirmed destination, following pagination and including completed or archived issues where available. Search same-team issues in other projects only to surface possible cross-project overlap; never reuse them in this run. Compare in-project milestone scope and issue titles, descriptions, acceptance criteria, and status with the primary source:

- Reuse existing milestones that match a planned phase.
- Reuse existing issues that already cover planned work only when their team and project both match the confirmed destination; do not duplicate them.
- Narrow or omit proposed work that partially overlaps, and explain the overlap during review.
- Do not modify, rename, or close existing issues unless the user explicitly approves it.

Recheck for newly created overlaps immediately before publishing.

### 3. Draft milestones and tracer-bullet tickets

Each ticket must deliver a narrow, complete, independently verifiable outcome. Prefer vertical slices across necessary layers over layer-by-layer tasks. Size agent work for one fresh context window and human work as one focused action. Include human judgement, credentials, physical action, or approval as **Human** tickets; publish them normally without `Agent`.

Before slicing, look for prefactoring that makes the requested change easier. Create prefactoring tickets first only when they have concrete, independently verifiable outcomes needed by later slices; never invent unrelated cleanup.

Assign every ticket to exactly one native Linear project milestone. Reuse an existing milestone that matches the outcome; otherwise create one, including for a single-ticket change. Milestone names describe outcomes or phases and have no numeric prefix. Order milestones chronologically and topologically: every prerequisite milestone must appear earlier.

Within each milestone, name every issue:

```text
III - Title
```

`III` is zero-padded to three digits. Start each milestone at `000`, increment in topological display order (`001`, `002`), and reset to `000` for the next milestone. Avoid numbers already used in that milestone.

Topologically order all tickets without inventing dependencies. Every blocker must be earlier in the total milestone/task order:

- A ticket may depend on a lower issue number in its milestone.
- A ticket may depend on any ticket in an earlier milestone.
- A ticket must never depend on a later issue in its milestone or any future milestone.

A lower issue number does not itself create a dependency; only a native `blockedBy` relationship does. Keep independent tickets unblocked. They form the execution **frontier** and may be assigned to separate agents concurrently.

If an existing milestone or issue conflicts with this order, surface it and ask before renaming, moving, or renumbering it. For wide mechanical refactors that cannot land green as vertical slices, use ordered expand–migrate–contract tickets.

### 4. Review with the user

Present the selected primary source and the complete proposal in milestone order. For each milestone show existing/new status; for each ticket show title, existing/new status, blockers, delivered outcome, acceptance criteria, executor (`Agent` or `Human`), labels, and source provenance. Explicitly show omitted or narrowed overlaps and the initial frontier of independent agent tickets.

Ask the user to approve milestone names/order, ticket order, granularity, dependencies, overlap decisions, criteria, labels, provenance, and parallel frontier. Do not publish until explicitly approved.

### 5. Prepare Linear metadata

Use the Linear MCP server to:

1. Resolve the confirmed team's unambiguous `Backlog` state.
2. List existing labels; reuse durable type/domain labels.
3. Create missing durable labels only when useful.
4. Ensure `Agent` exists if any ticket is agent-suitable.

Use two or three labels where useful. Agent tickets normally use `Agent`, one type, and optionally one domain label. Human tickets use type/domain labels but never `Agent`; do not invent `Human` unless asked. Avoid status, project, team, redundant, or one-off labels.

### 6. Publish and verify

After the final overlap recheck, create missing milestones in approved chronological order, then create issues milestone-by-milestone and ascending by title number. Assign each newly created issue to its milestone and the confirmed `team`, `project`, resolved Backlog `state`, and approved `labels`. Preserve every reused issue's existing state, milestone, project, labels, and relations unless the reviewed proposal explicitly listed each intended mutation and the user approved it. Add approved native parent and `blockedBy` relationships using existing or already-created identifiers. Never use prose instead of available native relations.

Use Linear's native milestone/issue reordering capability when available. Otherwise creation order plus numeric titles is the source of truth; verify the returned order and clearly report any manual Linear reorder still required. Never invent milestone target dates merely to force ordering.

Do not assign, delegate, or add issues to a cycle unless asked. For newly created issues, verify milestone assignment, title, team, project, Backlog state, labels, and blockers. For reused issues, verify identity and approved mutations without normalizing unapproved metadata. Confirm new agent tickets have `Agent`, new human tickets do not, and every newly created or explicitly approved blocker is earlier. Correct in-scope mismatches before reporting identifiers and URLs.

End with plain text `Milestone order: <first> → <second> → <third>` followed by issue identifiers/URLs in that same milestone and ascending-number order. Then write `Parallel frontier: <issues>` listing unblocked `Agent` tickets that separate agents can implement concurrently, or `Parallel frontier: none`.

## Issue description

```md
## What to build
<End-to-end outcome from the user's perspective.>
## Acceptance criteria
- [ ] <Observable, verifiable outcome>
## Context
<Durable decisions and constraints only; omit if unnecessary.>
## Source
<Stable source reference; omit when represented by a native parent relation or no stable reference exists.>
```

Avoid brittle paths and snippets. Example: milestone `Quote flow` contains `000 - Create quote form`, then `001 - Validate requests`; milestone `Launch` starts again at `000 - Approve production wording`.

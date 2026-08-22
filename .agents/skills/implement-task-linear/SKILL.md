---
name: implement-task-linear
description: Implement one Linear issue end to end in the current repository, using its optional originating spec, plan, domain context, and ADRs when available.
disable-model-invocation: true
---

# Implement Task from Linear

Implement exactly one Linear issue. The issue is the required input and scope boundary; its provenance chain and repository context refine how to implement it without silently adding work.

## 1. Resolve the target and Linear boundary

Require an unambiguous Linear issue identifier or URL. Ask for one when absent.

Fetch the target's full description, status, labels, team, project, relation identifiers, comments, and requirement-bearing attachments. Record its exact team ID and project ID. If it has no project, ask the user to assign it or authorize assignment to a named project before inspecting surrounding Linear work.

After resolving the target, apply this boundary to every other Linear issue read or mutation:

- query with both recorded IDs, then verify both IDs on every result;
- discover parents, sub-issues, blockers, duplicates, and related issues through scoped queries rather than directly fetching an unverified identifier;
- treat a cross-scope relation as unavailable context and report only its existence;
- create issues only with both recorded IDs.

Resolve the team's relevant workflow states, especially In Progress, In Review, Backlog, and any blocked state, by ID. Report ambiguity instead of guessing.

This step is complete when the target, acceptance criteria, team/project IDs, and usable workflow states are known.

## 2. Follow the provenance chain

Determine the repository root and read every applicable `AGENTS.md` before other repository work.

Then resolve context in this order. Each source is optional unless the target explicitly depends on it:

1. **Originating spec** — follow every explicit `Source` path or URL, spec link, design link, or requirement-bearing attachment in the target. When `to-linear` represented the spec as a native parent, find that parent through a team-and-project-scoped query, verify both IDs, then read its full description and comments. If no provenance is recorded, search `docs/`, `specs/`, and `.scratch/` for a single clear match to the target title, source name, or feature slug; ask before choosing among plausible matches. Inspect in-scope parents, blockers, milestone or cycle neighbours, and overlapping issues when they can clarify intent, dependencies, or drift.
2. **Plan** — read root `PLAN.md` fully when it exists. Use it for the larger outcome, sequencing, dependencies, and the target's place in the plan.
3. **Domain context** — read root `CONTEXT.md` fully when it exists. Also read root `CONTEXT-MAP.md` fully when it exists and follow every context entry applicable to the target. Context files define project language and domain boundaries, not implementation scope.
4. **Decisions** — read applicable ADRs under root `docs/adr/` and any context-specific `docs/adr/`. Follow pertinent artifact pointers from the selected spec, plan, context files, ADRs, and current conversation.
5. **Repository truth** — inspect the implicated code, tests, documentation, schemas, migrations, generated artifacts, configuration, and git status. Preserve unrelated user changes.

The prerequisite skills may leave different parts of this chain:

- `grill-with-docs` may leave `CONTEXT.md`, `CONTEXT-MAP.md`, and ADRs;
- `to-spec` publishes implementation decisions, agreed testing seams, and out-of-scope boundaries in an originating tracker issue;
- `to-linear` carries that origin into the target through a native parent or `Source` reference and may copy durable constraints into `Context`.

Missing optional artifacts do not block implementation. Explicit pointers do require resolution: read them, or report why they are unavailable.

Use this evidence hierarchy:

- the target issue defines the deliverable and acceptance criteria;
- the originating spec and `PLAN.md` explain intent, sequencing, and exclusions without expanding the target;
- context files and ADRs constrain vocabulary, boundaries, and durable decisions;
- the repository defines current implementation truth.

Surface material conflicts or stale evidence. Ask only when a conflict changes the intended behaviour, public interface, compatibility, or scope.

This step is complete when every explicit provenance pointer is resolved or reported unavailable, every applicable context artifact is read, and every target requirement is classified as implemented, partial, missing, contradicted, or blocked.

## 3. Plan the delivery

Before editing, give the user:

- the target outcome and assumptions;
- the context sources consulted, including missing or unavailable expected artifacts;
- material drift between Linear, the originating spec, `PLAN.md`, domain documents, and the repository;
- a short vertical-slice plan with a verification check for each step;
- the public testing seams already agreed in the spec, or the highest existing seams that fit the change.

Map every acceptance criterion and durable constraint to an implementation change or concrete verification. Existing behaviour counts only after verification. If choosing a seam would create or materially change a public interface, confirm it with the user when the upstream artifacts did not already settle it.

Move the target to the resolved In Progress state when implementation starts, unless it is already further along or the user asked to preserve its status.

This step is complete when every acceptance criterion has a delivery and verification path and every material design choice is settled.

## 4. Implement in verified slices

Implement the minimum coherent change that satisfies the whole target:

- follow repository conventions and preserve unrelated work;
- for testable behaviour, work red then green at the agreed public seams, one vertical slice at a time;
- run the smallest relevant test and typecheck after each slice, then the broader relevant suite once near the end;
- update required documentation, schemas, migrations, generated artifacts, and configuration;
- keep required target work in the current change rather than deferring it to manufacture completion;
- leave commits, pushes, pull requests, deployment, and release to an explicit user request.

When a test cannot reasonably be written or a full suite cannot run, use the strongest available verification and record the limitation.

Continue until every acceptance criterion passes or a genuine blocker remains.

## 5. Review and reconcile

Inspect the complete diff before changing final Linear state:

- **Standards** — check every changed area against applicable repository instructions, conventions, and ADRs.
- **Spec** — check every target criterion and originating-spec constraint for missing work, incorrect behaviour, and scope creep.

Fix actionable findings and rerun affected checks. Re-fetch the target and every mutated related issue, then reconfirm the team/project boundary.

When every required outcome is implemented and verified:

1. Add a concise target comment mapping each acceptance criterion to implementation and verification evidence.
2. Move the target to In Review only when its current state is earlier.

Preserve completed, cancelled, archived, In Review, and other terminal or further-along states. When blocked, leave the target In Progress or use the resolved blocked state and record the unmet criterion, evidence, and dependency.

## Related Linear work

The user authorizes minimal related-issue mutations inside the recorded team/project boundary when implementation evidence justifies them.

Create a follow-up only when it is concrete, independently actionable, genuinely outside the target, and absent after a scoped overlap search. Create it in the same team/project, normally in Backlog, with observable acceptance criteria and a native relation to the target when supported. A prerequisite that blocks completion must be related and reported as a blocker rather than used to declare the target complete.

Update an existing related issue only when repository evidence makes its description, relations, or status stale. Before treating it as satisfied, read every acceptance criterion and verify complete coverage. Move it to In Review only from an earlier state; preserve terminal and further-along states. Partial or title-only overlap does not justify a transition.

## Handoff

Report:

- target identifier, URL, and final status;
- implemented outcome and changed surfaces;
- verification commands and results;
- acceptance-criteria coverage;
- context sources used and unavailable explicit pointers;
- material drift or conflicts found;
- every related issue read that changed an implementation decision;
- every issue created or modified, with reason, status, and URL;
- blockers, limitations, and follow-up work.

Say explicitly when no related Linear issues changed.

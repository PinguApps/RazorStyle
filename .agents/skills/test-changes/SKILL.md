---
name: test-changes
description: Explain how to manually inspect and test the work just completed in this conversation, using PowerShell commands where commands are needed.
disable-model-invocation: true
---

# Test Changes

Produce a practical handoff for manually verifying the work completed in the current conversation.

## 1. Establish the test surface

Reconstruct the delivered outcome from the conversation, including later corrections and failed or partial attempts. Treat the current workspace, connected systems, and running environment as confirmation of the final state, not as a substitute for the conversation.

Inspect read-only evidence where useful: changed files, `git diff`, repository status, available scripts, project documentation, generated artifacts, external records, and already-running services. Account for every user-visible behaviour, artifact, or external state changed by the completed work. Separate unrelated pre-existing changes.

If no completed or inspectable change exists, say so and identify what must be completed before manual testing is meaningful.

Completion criterion: every change made in this conversation is mapped to something the user can inspect or exercise, or explicitly marked as having no manual test surface.

## 2. Build the shortest valid test path

Start with what the user can see **right now**, before asking them to run anything. Include existing files, rendered artifacts, open applications, running services, URLs, or visible state only when confirmed.

Then provide the minimum prerequisites and commands needed to reach the test surface. Use PowerShell syntax for terminal commands:

- Begin from the correct directory with `Set-Location`.
- Give commands in execution order and make each block directly copyable.
- Use the repository's real scripts, project names, ports, and paths.
- Include dependency installation, build, migration, seed, server, or authentication steps only when required.
- State whether a long-running command occupies the terminal and when a second PowerShell window is needed.
- Prefer the smallest command that exercises the changed area; add broader checks only when they expose materially different risk.
- Mark any value the user must supply, such as `<ApiKey>`, and say where it comes from.

Link directly to local artifacts and external pages when they can be opened from the response. When no terminal step is relevant, give direct inspection actions without manufacturing a PowerShell command.

Do not mutate application data merely to prepare the explanation. When the test itself changes data, warn the user and give a cleanup or reset step when one is available.

Completion criterion: following the commands from a clean PowerShell window reaches every testable changed surface without hidden steps.

## 3. Specify observations

For each test, state:

1. The exact action to perform.
2. The exact result to expect.
3. The detail that proves the new or changed behaviour, including relevant text, controls, values, files, logs, status codes, or before/after differences.
4. A meaningful failure signal.

Cover the happy path first, then changed edge cases and regressions justified by the work. Describe observable behaviour rather than implementation details. Do not invent certainty: label inferred expectations and state any limitation that prevents a complete manual test.

Completion criterion: the user can decide pass or fail for every test without interpreting vague phrases such as “works correctly.”

## Response format

Omit empty sections.

```md
## What you can see now
- <Confirmed immediately visible state, and where to find it>

## Start or prepare
<Copyable PowerShell commands, plus terminal/window guidance>

## Manual tests
### 1. <Behaviour or artifact>
**Action:** <Exact interaction>
**Expected:** <Specific observable result>
**Changed:** <What is new or different>
**Failure:** <Specific sign of failure>

## Cleanup
<PowerShell commands or actions needed to stop services or revert test data>

## Limitations
- <Anything unconfirmed, untestable manually, or requiring unavailable access>
```

Keep the handoff concise, but include every changed test surface. Do not repeat automated test results unless they help the user interpret a manual test.

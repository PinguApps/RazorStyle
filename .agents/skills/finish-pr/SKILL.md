---
name: finish-pr
description: Finish the GitHub pull request attached to the current branch; resolve merge conflicts, diagnose and fix failed CI checks, action review feedback from every source without duplicating replies that are awaiting a reviewer response, push focused commits, and continue through Gitar review until its current-HEAD Core signals show a completed clean review. Use whenever the user asks to finish, complete, ready, resolve, fix, or address feedback/CI/conflicts on the current PR.
---

# Finish PR

Bring the pull request attached to the current branch to a genuinely ready state.

Requires Git 2.38+, GitHub CLI (`gh`) authenticated for the repository, and PowerShell 7 (`pwsh`) for bundled helpers.

## Definition of done

Finish only when all of these are true for the current PR HEAD:

- GitHub and a local merge probe show no merge conflicts.
- No CI check is failing. Pending checks may still be running, but never describe the PR as fully ready while a required check is pending.
- Every unresolved review thread is in one of these states:
  - its latest unaddressed feedback has been actioned and the agent has replied with the result; or
  - the latest relevant comment is the agent's response and no reviewer has replied afterwards, so the thread is awaiting review and needs no duplicate work.
- No reply created by this run remains in a pending GitHub review.
- No review thread's resolution state was changed by this run.
- The exact current HEAD has a successful completed `Gitar` check, Gitar's dashboard Code Review verdict is `Approved`, and no later Gitar or other reviewer feedback is left unaddressed.

This completion rule uses Gitar Core only. Never require Gitar auto-approval, a GitHub approving review, merge blocking, auto-apply, or any other Pro signal. Treat `Approved with Suggestions`, `Changes Requested`, `Blocked`, and `Needs Review` as non-terminal feedback states even if the `Gitar` check itself succeeds.

An unresolved thread is not automatically unfinished. Reviewers own resolution state; the conversation order determines whether the agent currently owes action.

## Operating rules

- Use the conversation history as first-class task context. The skill is often invoked after implementation, so recover the user's intent, earlier decisions, tradeoffs, verification, known limitations, and reasons for the current design before judging PR feedback.
- Read every applicable `AGENTS.md` plus repository-native requirements and design documents implicated by the PR. Discover these from the repository and PR; never assume a particular task directory, branch naming scheme, language, build system, or hosting provider.
- Treat unresolved review threads as the authoritative inline-feedback list. Also inspect PR-level reviews and issue comments for standalone actionable feedback.
- Preserve unrelated worktree changes. Commit only changes made during this run.
- Resolve conflicts before failed checks, and failed checks before review feedback. Later evidence may require revisiting an earlier phase.
- Prefer the smallest correct change. Add focused tests for behavioural or regression-prone fixes.
- Use exactly one focused, unsquashed commit per action-required PR feedback unit whose disposition produces a change: one review thread or one standalone PR-level feedback item. Never combine multiple units into one commit, even when they are related. A justified disagreement requires a reply but no commit.
- Never rebase, force-push, merge the pull request on GitHub, close, approve, or mark the PR ready for review unless the user explicitly requested that separate action. The conflict-resolution workflow may merge the latest base commit into the PR branch.
- Never resolve or unresolve a review thread. Do not call `resolveReviewThread`, `unresolveReviewThread`, or an equivalent.
- Reply directly to review threads, one at a time. Never create replies concurrently.
- Continue autonomously through new feedback from Gitar and every other source after pushes, within the convergence bounds below.
- Never ask Gitar to apply or commit a fix. Do not use `gitar fix`, one-click apply, or `gitar auto-apply:on`. This agent owns every code change.

## 1. Establish state and intent

1. Locate the repository root and read applicable instructions.
2. Inspect:

   ```powershell
   git status --short
   git rev-parse --abbrev-ref HEAD
   git rev-parse HEAD
   gh --version
   gh auth status
   gh pr view --json number,title,url,body,author,headRefName,headRefOid,headRepository,headRepositoryOwner,isCrossRepository,baseRefName,baseRefOid,mergeable,mergeStateStatus,reviews,comments
   ```

3. Record the starting SHA and every existing changed path, separating staged, unstaged, and untracked changes. Never include pre-existing changes in this run's commits.
   A feedback or CI fix must not touch a path that had any pre-existing staged, unstaged, or untracked change. Stop and ask the user before editing that path; partial staging plus `git commit --only` does not preserve same-file hunk isolation.
4. If the current branch has no PR, inspect `gh pr status`. Switch or check out a PR only when the mapping is unambiguous and local changes are safe; otherwise ask the user.
5. Reconstruct the intended change from, in priority order:
   - explicit user instructions and this conversation;
   - applicable repository instructions;
   - PR title, body, commits, and diff;
   - linked issue/spec/design documents;
   - surrounding code, tests, and conventions.
6. Inspect the complete PR diff before evaluating conflicts, CI, or feedback.
7. Identify the Gitar integration from exact case-insensitive `Gitar` check runs whose GitHub App slug is Gitar-marked, plus the Gitar-authored dashboard Code Review comment. Treat the exact-HEAD check as the processing/completion boundary and the dashboard's Code Review verdict as the review result. Never infer completion from reactions or require a GitHub approval review.
8. Resolve the authenticated GitHub viewer login. Treat comments from that login, or another agent login established unambiguously by the conversation/PR history, as agent responses.
9. Resolve the base and head repositories independently from PR metadata:
   - derive the base repository from the PR URL;
   - use `headRepository.nameWithOwner` for the head repository;
   - prefer existing configured remotes that match those exact repositories;
   - otherwise use GitHub-provided clone URLs and configure Git to use the authenticated `gh` credential helper;
   - retain the exact `headRefName`.

   ```powershell
   $pr = gh pr view --json number,url,headRefName,headRefOid,headRepository,headRepositoryOwner |
     ConvertFrom-Json
   $prNumber = [int]$pr.number
   $prUri = [uri]$pr.url
   $pathSegments = $prUri.AbsolutePath.Trim("/").Split("/")
   $githubHostname = $prUri.Authority
   $baseRepositoryName = "$($pathSegments[0])/$($pathSegments[1])"
   $baseRepository = "$githubHostname/$baseRepositoryName"
   $baseMetadata = gh api --hostname $githubHostname "repos/$baseRepositoryName" |
     ConvertFrom-Json
   $headMetadata = gh api --hostname $githubHostname "repos/$($pr.headRepository.nameWithOwner)" |
     ConvertFrom-Json
   gh auth setup-git --hostname $githubHostname
   $baseFetchUrl = [string]$baseMetadata.clone_url
   $headPushUrl = [string]$headMetadata.clone_url
   ```

   GitHub's `clone_url` preserves the server authority, including non-default ports, while `gh auth setup-git` makes HTTPS Git operations use the authenticated CLI identity. A verified matching SSH/configured remote may be used instead. Stop if authentication, repository identity, or the head repository is unavailable or ambiguous. Never assume `origin` points to either side of a fork-based PR.
10. Align the checkout with the exact PR head before making any changes:

   ```powershell
   git fetch $headPushUrl $pr.headRefName
   $remoteHeadSha = (git rev-parse FETCH_HEAD).Trim()
   $lastObservedPrHeadSha = $remoteHeadSha
   $localHeadSha = (git rev-parse HEAD).Trim()
   git merge-base --is-ancestor $remoteHeadSha $localHeadSha
   $localContainsRemoteHead = $LASTEXITCODE -eq 0

   if ($localContainsRemoteHead -and $localHeadSha -ne $remoteHeadSha) {
     $initialLocalAheadCommits = git log --oneline "$remoteHeadSha..$localHeadSha"
     # Validate and record every commit against the recovered user intent and full PR diff.
     # Stop for confirmation if any commit is unrelated, unfinished, or ambiguous.
   }

   if (-not $localContainsRemoteHead) {
     git merge-base --is-ancestor $localHeadSha $remoteHeadSha
     $canFastForward = $LASTEXITCODE -eq 0 -and -not (git status --porcelain)
     if (-not $canFastForward) {
       throw "Local HEAD is stale or diverged from the PR head; stop before mutation."
     }

     git merge --ff-only $remoteHeadSha
   }
   ```

   Continue only when local `HEAD` matches the fetched PR head, is safely fast-forwarded to it, or every initially local-ahead commit was explicitly validated as intended PR work from conversation and repository evidence. Record those commits as pre-existing push scope; stop for confirmation on any uncertainty. Never silently publish local-ahead commits.
11. Create one unique state directory outside the repository and retain it for the full run:

   ```powershell
   $runStateDirectory = Join-Path ([IO.Path]::GetTempPath()) (
     "finish-pr-{0}-{1}" -f $prNumber, [guid]::NewGuid().ToString("N")
   )
   New-Item -ItemType Directory -Path $runStateDirectory | Out-Null
   ```

   Store every baseline, snapshot, and watcher-state file for this run inside this directory so concurrent runs cannot overwrite one another.

## 2. Resolve merge conflicts

Run this before CI or feedback:

1. Refresh PR/base metadata and perform a non-mutating local probe:

   ```powershell
   $pr = gh pr view $prNumber --repo $baseRepository --json number,url,baseRefName,baseRefOid,headRefName,headRefOid,headRepository,headRepositoryOwner,mergeable,mergeStateStatus |
     ConvertFrom-Json
   git fetch $baseFetchUrl $pr.baseRefName
   $baseCommit = (git rev-parse FETCH_HEAD).Trim()
   git merge-tree --write-tree --messages HEAD $baseCommit
   ```

2. Investigate locally when GitHub reports `CONFLICTING`, `DIRTY`, or `UNKNOWN`, or when the probe reports conflicts.
3. If conflicts exist, check the initial worktree baseline before merging. A normal merge requires a clean index: if any pre-existing staged change remains, stop before mutation and tell the user to commit or stash it, or request explicit permission to isolate and restore the index. Do not use `--autostash` on user work without that permission.

   If the index is clean and unrelated local changes are safe, merge the latest base into the PR branch:

   ```powershell
   git merge --no-ff $baseCommit
   ```

4. Resolve each conflict using the recovered task intent, repository requirements, adjacent code, and tests. Do not mechanically prefer either side.
5. Run focused verification, stage only the resolution, and commit it. Record the conflict summary, commit SHA, and checks run.
6. If the merge is clean, keep any Git-created merge commit but do not create an extra empty commit.

## 3. Fix failed CI

1. Inspect all checks:

   ```powershell
   gh pr checks $prNumber --repo $baseRepository --json bucket,completedAt,description,event,link,name,startedAt,state,workflow
   gh pr checks $prNumber --repo $baseRepository --required --json bucket,completedAt,description,event,link,name,startedAt,state,workflow
   ```

   Use the second query to distinguish required checks from optional checks. Treat “no required checks reported” as an empty required set, not a failure.

2. For each failure, retrieve the actual logs before editing:

   ```powershell
   gh run list --repo $baseRepository --branch $pr.headRefName --commit $pr.headRefOid --json databaseId,name,workflowName,status,conclusion,url,headSha,event,createdAt -L 50
   gh run view <run-id> --repo $baseRepository --json name,status,conclusion,jobs,url
   gh run view <run-id> --repo $baseRepository --log-failed
   ```

3. For non-Actions checks, inspect the provider link or available check details.
4. Fix the root cause, not merely the symptom. Work from the clearest upstream failure outward because one failure may cascade into others.
5. Run the closest local equivalent, inspect the initial worktree baseline, and stage only that fix. Commit with an exact pathspec so pre-existing staged changes remain outside the CI commit:

   ```powershell
   git commit --only -m "fix(ci): address <failed check>" -- <exact-ci-fix-paths>
   ```

   If a CI fix path had pre-existing staged, unstaged, or untracked changes, stop before editing it. Record the check, cause, SHA, and verification.
6. If a failure is external, flaky, permission-related, or not repository-fixable, capture evidence. Retry only when safe and supported; do not change code to appease an unrelated failure.

## 4. Fetch and classify feedback

Resolve the loaded skill's directory, then use its bundled helpers by absolute path; do not assume the skill lives inside the target repository.

Capture every thread's read-only resolution baseline outside the repository:

```powershell
$threadBaseline = Join-Path $runStateDirectory "thread-resolution.json"
$threadSnapshot = Join-Path $runStateDirectory "unresolved-threads.json"
pwsh <skill-directory>/scripts/get-unresolved-pr-threads.ps1 -PrNumber $prNumber -Repository $baseRepository -Hostname $githubHostname -All |
  Set-Content -Encoding utf8 $threadBaseline

pwsh <skill-directory>/scripts/get-unresolved-pr-threads.ps1 -PrNumber $prNumber -Repository $baseRepository -Hostname $githubHostname |
  Set-Content -Encoding utf8 $threadSnapshot
```

For each unresolved thread, read all paginated comments in chronological order and classify it:

- **Awaiting reviewer:** the latest relevant comment is an agent response in a submitted review (`pullRequestReview.state != PENDING` with non-null `submittedAt`) and nobody has replied later. Do nothing. Do not post a reminder, repeat the fix, or duplicate the response.
- **Action required:** there is reviewer feedback after the agent's latest submitted response, the agent has never responded, or its latest response exists only in a pending review.
- **Superseded/non-actionable:** the later conversation explicitly withdraws, answers, or supersedes the point. Reply only if the thread still needs an agent acknowledgement; avoid duplicating an existing agent response.

Within action-required threads, identify each distinct feedback item. Judge it against the user's intent, conversation history, repository rules, linked requirements, PR scope, current code, conventions, and tests. The thread is the commit boundary: group all work required by one thread into that thread's single commit, but never include another thread's work.

Classify actionable PR-level review bodies and issue comments with the same rules. Each standalone feedback item is its own commit boundary; never combine it with a review thread or another standalone item. Track it by feedback ID and permalink because it has no review-thread ID.

- Agree when it identifies a real bug, missed requirement, broken invariant, missing test, misleading behaviour, or scoped maintainability problem.
- Disagree when it conflicts with requirements, established intent, repository invariants, or would produce a worse/out-of-scope design.
- When uncertain, make a small scoped correctness fix if evidence supports it. Otherwise explain the uncertainty and why no change was made.

Do not skip outdated unresolved threads; determine whether their feedback still applies to current code.

## 5. Fix and reply

For each action-required review thread or standalone feedback item:

1. Make the smallest complete fix for every actionable item in that thread, with focused tests.
2. Run the narrowest meaningful verification.
3. Inspect and stage only files for that item:

   ```powershell
   git status --short
   git diff
   git add <paths>
   git diff --cached
   ```

   Keep unrelated staged changes in other paths staged. Stop before editing any fix path that had a pre-existing staged, unstaged, or untracked change; never rely on partial staging followed by `git commit --only` for same-file isolation.

4. If the disposition produces a change, create exactly one commit for the feedback unit before moving to the next unit:

   ```powershell
   git commit --only -m "fix(pr): address <thread summary>" -- <exact-thread-fix-paths>
   ```

   Do not amend, squash, or combine feedback commits. If an earlier unit's change completely satisfies a later agreed unit and no distinct file change remains, create an explicit traceability commit with `--allow-empty` for that later unit rather than merging their commit history. Do not create a commit for a justified disagreement.

5. For review-thread feedback, reply directly to the thread after evaluating it and creating any relevant commit, subject to the Gitar deferral below:

   ```powershell
   $body = @"
   Agreed. I fixed this in commit <sha> by <specific change>.

   Verification: <command and result>.
   "@
   pwsh <skill-directory>/scripts/reply-to-review-thread.ps1 -ThreadId "<thread-id>" -Hostname $githubHostname -Body $body
   ```

   For standalone feedback, post a PR-level reply that links the exact feedback permalink:

   ```powershell
   $body = @"
   Regarding [this feedback](<feedback-permalink>): agreed. I fixed this in commit <sha> by <specific change>.

   Verification: <command and result>.
   "@
   gh pr comment $prNumber --repo $baseRepository --body $body
   ```

   Use the same PR-level path for a standalone justified disagreement, retaining the feedback permalink and the disagreement wording below.

   For a justified disagreement:

   ```text
   I don't think this change is correct for this PR.

   Reason: <specific reason grounded in requirements, conversation, or code>.

   No code change made.
   ```

   If any Gitar-authored feedback disposition created a commit that has not been pushed yet, defer its thread or PR-level reply until immediately after the batch push. Gitar cannot verify a local-only SHA; replying before it can see the commit may cause a misleading follow-up. Prefix a PR-level Gitar reply with `Gitar,` so the dashboard feedback is processed; never ask it to apply the fix. A Gitar disagreement has no commit dependency and may be replied to immediately. Keep all replies serial.

6. Record the one-to-one feedback-unit ID → commit SHA mapping for changed dispositions, plus every disposition, verification, and returned comment ID. Use the thread ID for review threads and the feedback ID for standalone items. Record `no commit — disagreement` for justified disagreements.

The reply helper uses GitHub's single-comment reply endpoint so it never submits or modifies a shared pending review. It then verifies `state != PENDING` plus a non-null `submittedAt`. A helper failure is blocking; a returned comment URL alone is not proof of submission.

After all replies:

1. Re-fetch all threads with `-All`, plus PR-level reviews and issue comments.
2. Verify every reply created in this run belongs to a submitted review.
3. Verify the authenticated user has no pending review on the PR, including reviews created before this run.
4. For every thread ID present in the baseline, compare its `isResolved` value with the current value. Baseline resolution states must be unchanged; report external changes and never mutate them back. New thread IDs are expected during review convergence: classify them as additional feedback rather than treating their existence as a resolution mutation.
5. If new action-required review threads or standalone feedback items appeared during the batch, action each in its own commit and repeat the audit. Push only once the currently visible feedback set has been fully actioned or is awaiting reviewer response. Gitar threads with a deferred local-commit reply count as actioned for this pre-push audit, but the reply remains mandatory immediately after push.

## 6. Push and converge with Gitar

1. Confirm the worktree contains no uncommitted changes created by this run. Do not push individual thread commits as they are created; batch-push all unsquashed thread commits only after the feedback audit is clear for the time being.
2. Review commits after the starting SHA and every validated commit that was already local-ahead at invocation, then fetch the exact PR head before pushing:

   ```powershell
   git log --oneline <starting-sha>..HEAD
   git fetch $headPushUrl $pr.headRefName
   $remoteHeadSha = (git rev-parse FETCH_HEAD).Trim()
   $localHeadSha = (git rev-parse HEAD).Trim()
   $currentPrHeadSha = (gh pr view $prNumber --repo $baseRepository --json headRefOid --jq .headRefOid).Trim()
   $pushRequired = $localHeadSha -ne $remoteHeadSha
   git status --short --branch
   ```

   Stop if `$remoteHeadSha` and `$currentPrHeadSha` differ, or if either differs from `$lastObservedPrHeadSha`. Never overwrite `$lastObservedPrHeadSha` with an unexpected remote value. Use `$pushRequired`, not the starting-SHA commit range, to decide whether the PR head needs a push; the range is reporting context only.

3. If `$pushRequired`, increment `$reviewRound` and capture a review baseline immediately before pushing:

   ```powershell
   $reviewRound++
   $reviewState = Join-Path $runStateDirectory ("review-round-{0}.json" -f $reviewRound)
   pwsh <skill-directory>/scripts/wait-for-pr-review.ps1 `
     -CaptureBaseline `
     -StatePath $reviewState `
     -PrNumber $prNumber `
     -Repository $baseRepository `
     -Hostname $githubHostname
   ```

4. Push without force and record the exact HEAD:

   ```powershell
   $reviewRequestedAt = [DateTimeOffset]::UtcNow
   git push $headPushUrl "HEAD:refs/heads/$($pr.headRefName)"
   $expectedHeadSha = git rev-parse HEAD
   $lastObservedPrHeadSha = $expectedHeadSha
   ```

   Immediately after the push, post each deferred Gitar thread or PR-level reply serially with its now-visible commit SHA and verification result. Verify thread replies are submitted, then refresh feedback once before waiting for Gitar.

5. After pushing, allow approximately 60 seconds for the exact-HEAD `Gitar` check to appear:

   ```powershell
   pwsh <skill-directory>/scripts/wait-for-pr-review.ps1 `
     -Wait `
     -StatePath $reviewState `
     -ExpectedHeadSha $expectedHeadSha `
     -ReviewRequestedAt $reviewRequestedAt `
     -ReviewStartGraceSeconds 60 `
     -TimeoutMinutes 25 `
     -PollSeconds 10
   ```

   If this returns `review_not_started`, and only then, request review once for that pushed HEAD:

   ```powershell
   gh pr comment $prNumber --repo $baseRepository --body "gitar review"
   ```

   Do not post the fallback comment when an exact-HEAD Gitar check appeared during the grace period, even if it is still queued or processing.

6. If the grace-period watcher returned `review_not_started`, resume the bundled watcher against the same baseline:

   ```powershell
   pwsh <skill-directory>/scripts/wait-for-pr-review.ps1 `
      -Wait `
      -StatePath $reviewState `
      -ExpectedHeadSha $expectedHeadSha `
      -ReviewRequestedAt $reviewRequestedAt `
      -TimeoutMinutes 25 `
     -PollSeconds 20
   ```

   Run it as a long-lived tool call. While it runs, use only the environment's wait mechanism and remain silent unless the user interrupts. The watcher keeps repeated polling out of model context.

7. Handle its terminal result:
   - `feedback`: fetch all feedback for context, but action only IDs in `newFeedback`. This may include inline threads, PR-level feedback from any reviewer, or Gitar's dashboard when its verdict is `Approved with Suggestions`, `Changes Requested`, `Blocked`, or `Needs Review`. If a new comment extends an old unresolved thread, read the full thread and handle only feedback after the last agent response.
   - `approved`: the exact-HEAD Gitar check completed successfully and the fresh dashboard verdict is `Approved`. Re-fetch checks, dashboard, PR-level feedback, and threads once; finish only if the full definition of done still holds.
   - `gitar_failed`: inspect the Gitar check and dashboard details. Treat provider/integration failure as a blocker unless repository evidence gives a scoped fix; never describe the PR as reviewed successfully.
   - `review_not_started`: post the single fallback `gitar review` comment, then resume step 6.
   - `timeout`: report that Gitar did not reach a terminal state; do not claim readiness.
   - `head_changed`: fetch and inspect the new state. Stop when another actor's push makes continued mutation unsafe.
   - `pr_closed`: stop and report the PR state.

8. For new feedback, repeat fix → verify → commit → serial reply → audit → baseline → push → automatic-review grace period → wait.

If no push is needed, never post `gitar review`: the fallback is only permitted after this run pushes a new HEAD. Capture the current state and wait read-only. A completed successful `Gitar` check on the exact current HEAD plus an `Approved` dashboard verdict is sufficient; no Pro approval signal is required. Otherwise wait for an already-running automatic review and stop on timeout without posting a trigger.

```powershell
$expectedHeadSha = (git rev-parse HEAD).Trim()
$reviewRound++
$reviewState = Join-Path $runStateDirectory ("review-round-{0}.json" -f $reviewRound)
pwsh <skill-directory>/scripts/wait-for-pr-review.ps1 `
  -CaptureBaseline `
  -StatePath $reviewState `
  -PrNumber $prNumber `
  -Repository $baseRepository `
  -Hostname $githubHostname
pwsh <skill-directory>/scripts/wait-for-pr-review.ps1 `
  -Wait `
  -StatePath $reviewState `
  -ExpectedHeadSha $expectedHeadSha `
  -ReviewRequestedAt ([DateTimeOffset]::MinValue) `
  -TimeoutMinutes 25 `
  -PollSeconds 20
```

The watcher verifies that Gitar's check belongs to the exact expected SHA. After a push, it also requires the dashboard comment to have changed after the baseline and in the same processing window before accepting its verdict, because Gitar edits one persistent dashboard comment in place. Never use the fallback comment without a preceding push from this run.

Bound convergence to five pushed review rounds or two hours overall. Stop earlier for a clean Gitar result, timeout, closure, unexpected head movement, or a genuine blocker.

## Final audit and response

Re-fetch rather than relying on cached state:

- PR head, mergeability, and base;
- latest base commit and a fresh local `git merge-tree --write-tree --messages HEAD <base-commit>` conflict probe;
- all checks;
- required checks queried separately with `gh pr checks --required`;
- the exact-HEAD Gitar check and Gitar dashboard Code Review verdict;
- PR-level reviews/comments;
- all review threads and reply submission states;
- local/remote branch state and worktree.

Report concisely:

- PR number and URL;
- conflict and CI outcome, with commits;
- thread counts: actioned, awaiting reviewer, and disagreed;
- fixes, focused verification, and commit SHAs;
- push result and Gitar review rounds;
- terminal Gitar Core status for current HEAD: successful completed check plus `Approved` dashboard verdict;
- pending review replies: `0`;
- review-thread resolution mutations: `0`, with baseline audit result;
- any blocker or required check still pending.

$ErrorActionPreference = "Stop"

. "$PSScriptRoot\..\scripts\wait-for-pr-review.ps1" `
    -Wait `
    -StatePath "unused-test-state.json" `
    -ExpectedHeadSha "unused-test-sha" `
    -ReviewRequestedAt ([DateTimeOffset]::MinValue)

function Assert-Equal {
    param($Expected, $Actual, [string]$Message)

    if ($Expected -ne $Actual) {
        throw "$Message Expected '$Expected', got '$Actual'."
    }
}

function New-Check {
    param(
        [string]$Status = "completed",
        [string]$Conclusion = "success",
        [string]$StartedAt = "2026-08-12T12:00:00Z"
    )

    return [pscustomobject]@{
        id = 123
        status = $Status
        conclusion = $Conclusion
        startedAt = $StartedAt
        completedAt = "2026-08-12T12:01:00Z"
        headSha = "new-sha"
    }
}

function New-Dashboard {
    param(
        [string]$Verdict = "Approved",
        [string]$UpdatedAt = "2026-08-12T12:00:30Z",
        [string]$Id = "dashboard"
    )

    return [pscustomobject]@{
        id = $Id
        kind = "issue_comment"
        authorLogin = "gitar-bot"
        authorType = "Bot"
        body = "<details><summary><b>Code Review</b> <kbd>$Verdict</kbd></summary></details>"
        url = "https://example.test/dashboard"
        createdAt = "2026-08-12T11:00:00Z"
        updatedAt = $UpdatedAt
        isGitarDashboard = $true
    }
}

function New-Snapshot {
    param(
        [string]$HeadSha = "new-sha",
        [string]$State = "OPEN",
        [array]$Checks = @(),
        [AllowNull()]$Dashboard = $null,
        [array]$FeedbackItems = @()
    )

    return [pscustomobject]@{
        pullRequest = [pscustomobject]@{ headSha = $HeadSha; state = $State }
        feedbackItems = $FeedbackItems
        gitarChecks = $Checks
        gitarDashboard = $Dashboard
    }
}

$baseline = [pscustomobject]@{
    agentLogin = "example-agent"
    baselineHeadSha = "old-sha"
    seenFeedbackVersions = @(
        [pscustomobject]@{ id = "old-comment"; body = "Old"; updatedAt = "2026-08-12T11:00:00Z" }
    )
    gitarDashboard = [pscustomobject]@{
        id = "dashboard"
        body = "<b>Code Review</b> Approved"
        updatedAt = "2026-08-12T11:00:00Z"
    }
}

Assert-Equal "gitar-bot" (Normalize-Login "Gitar-Bot[BOT]") "Login normalization should strip bot suffixes."
Assert-Equal $true (Test-GitarActor -Login "gitar-bot" -AuthorType "Bot") "The documented Gitar bot must be recognized."
Assert-Equal $false (Test-GitarActor -Login "gitar-maintainer" -AuthorType "User") "A human with a Gitar-like login must not be trusted as the app."

$pages = '[[{"id":"one"}],[{"id":"two"}]]' | ConvertFrom-Json -Depth 10
$expanded = @(Expand-PaginatedItems $pages)
Assert-Equal 2 $expanded.Count "Paginated REST results should be flattened."
Assert-Equal "two" $expanded[1].id "Later REST pages should be preserved."

Assert-Equal "approved" (Get-GitarVerdict (New-Dashboard -Verdict "✅ Approved").body) "Approved should be terminal."
Assert-Equal "approved_with_suggestions" (Get-GitarVerdict (New-Dashboard -Verdict "Approved with Suggestions").body) "Suggestions must remain actionable."
Assert-Equal "changes_requested" (Get-GitarVerdict (New-Dashboard -Verdict "Changes Requested").body) "Changes requested must remain actionable."
Assert-Equal "needs_review" (Get-GitarVerdict (New-Dashboard -Verdict "Needs Review").body) "Needs Review must remain actionable."
Assert-Equal "blocked" (Get-GitarVerdict (New-Dashboard -Verdict "Blocked").body) "Blocked must remain actionable."
Assert-Equal "processing" (Get-GitarVerdict (New-Dashboard -Verdict "Processing").body) "Processing must remain non-terminal."
$approvedWithMisleadingSummary = (New-Dashboard -Verdict "✅ Approved").body + " Changes Requested and blocked are mentioned in summary prose."
Assert-Equal "approved" (Get-GitarVerdict $approvedWithMisleadingSummary) "Summary prose must not override the explicit verdict badge."
$suggestionsWithMisleadingSummary = (New-Dashboard -Verdict "Approved with Suggestions").body + " The summary also says Approved."
Assert-Equal "approved_with_suggestions" (Get-GitarVerdict $suggestionsWithMisleadingSummary) "Only the explicit verdict badge should control classification."
Assert-Equal "unknown" (Get-GitarVerdict "# Code Review`nApproved") "Unstructured prose must not be accepted as a verdict."

$waiting = Resolve-ReviewOutcome -Baseline $baseline -Snapshot (New-Snapshot) -ExpectedSha "new-sha"
Assert-Equal "waiting" $waiting.status "A missing exact-HEAD Gitar check should wait."

$processing = Resolve-ReviewOutcome -Baseline $baseline -Snapshot (New-Snapshot -Checks @((New-Check -Status "in_progress" -Conclusion ""))) -ExpectedSha "new-sha"
Assert-Equal "processing" $processing.status "An in-progress Gitar check should wait as processing."

$failed = Resolve-ReviewOutcome -Baseline $baseline -Snapshot (New-Snapshot -Checks @((New-Check -Conclusion "failure"))) -ExpectedSha "new-sha"
Assert-Equal "gitar_failed" $failed.status "A failed Gitar check must block completion."

$approvedDashboard = New-Dashboard -Verdict "✅ Approved"
$approved = Resolve-ReviewOutcome -Baseline $baseline -Snapshot (New-Snapshot -Checks @((New-Check)) -Dashboard $approvedDashboard) -ExpectedSha "new-sha"
Assert-Equal "approved" $approved.status "A fresh approved dashboard plus successful exact-HEAD check should complete."
Assert-Equal "approved" $approved.verdict "The terminal result should expose Gitar's verdict."

$suggestionsDashboard = New-Dashboard -Verdict "Approved with Suggestions"
$suggestions = Resolve-ReviewOutcome -Baseline $baseline -Snapshot (New-Snapshot -Checks @((New-Check)) -Dashboard $suggestionsDashboard) -ExpectedSha "new-sha"
Assert-Equal "feedback" $suggestions.status "Approved with Suggestions must wake the feedback loop."
Assert-Equal "gitar_dashboard" $suggestions.newFeedback[0].kind "Dashboard-only findings should be returned explicitly."

$staleDashboard = New-Dashboard -Verdict "Approved" -UpdatedAt "2026-08-12T11:00:00Z"
$staleDashboard.body = $baseline.gitarDashboard.body
$stale = Resolve-ReviewOutcome -Baseline $baseline -Snapshot (New-Snapshot -Checks @((New-Check)) -Dashboard $staleDashboard) -ExpectedSha "new-sha"
Assert-Equal "waiting" $stale.status "An unchanged pre-push dashboard must not approve a new HEAD."

$humanFeedback = [pscustomobject]@{
    id = "human-comment"
    kind = "thread_comment"
    authorLogin = "reviewer"
    authorType = "User"
    body = "Please handle this edge case."
    updatedAt = "2026-08-12T12:00:10Z"
    isGitarDashboard = $false
}
$feedbackBeforeApproval = Resolve-ReviewOutcome -Baseline $baseline -Snapshot (New-Snapshot -Checks @((New-Check)) -Dashboard $approvedDashboard -FeedbackItems @($humanFeedback)) -ExpectedSha "new-sha"
Assert-Equal "feedback" $feedbackBeforeApproval.status "New feedback from any source must take precedence over Gitar approval."
Assert-Equal "human-comment" $feedbackBeforeApproval.newFeedback[0].id "The exact new feedback ID should be returned."

$agentFeedback = [pscustomobject]@{
    id = "agent-comment"
    kind = "issue_comment"
    authorLogin = "example-agent"
    authorType = "User"
    body = "I fixed this."
    updatedAt = "2026-08-12T12:00:10Z"
    isGitarDashboard = $false
}
$agentIgnored = Resolve-ReviewOutcome -Baseline $baseline -Snapshot (New-Snapshot -Checks @((New-Check)) -Dashboard $approvedDashboard -FeedbackItems @($agentFeedback)) -ExpectedSha "new-sha"
Assert-Equal "approved" $agentIgnored.status "The agent's own replies must not be treated as new review feedback."

$emptyCopilotReview = [pscustomobject]@{
    id = "copilot-clean"
    kind = "review"
    authorLogin = "copilot-pull-request-reviewer"
    authorType = "Bot"
    body = "## Pull request overview`n`nCopilot reviewed 26 out of 26 changed files in this pull request and generated no new comments."
    reviewState = "COMMENTED"
    updatedAt = "2026-08-12T12:00:10Z"
    isGitarDashboard = $false
}
$cleanCopilotIgnored = Resolve-ReviewOutcome -Baseline $baseline -Snapshot (New-Snapshot -Checks @((New-Check)) -Dashboard $approvedDashboard -FeedbackItems @($emptyCopilotReview)) -ExpectedSha "new-sha"
Assert-Equal "approved" $cleanCopilotIgnored.status "A reviewer's explicit no-findings summary must not be treated as actionable feedback."

$gitarAutoApproval = [pscustomobject]@{
    id = "gitar-auto-approval"
    kind = "review"
    authorLogin = "gitar-bot[bot]"
    authorType = "Bot"
    body = "Gitar has auto-approved this PR ([configure](https://app.gitar.ai))"
    reviewState = "APPROVED"
    updatedAt = "2026-08-12T12:00:10Z"
    isGitarDashboard = $false
}
$gitarAutoApprovalIgnored = Resolve-ReviewOutcome -Baseline $baseline -Snapshot (New-Snapshot -Checks @((New-Check)) -Dashboard $approvedDashboard -FeedbackItems @($gitarAutoApproval)) -ExpectedSha "new-sha"
Assert-Equal "approved" $gitarAutoApprovalIgnored.status "Gitar's optional Pro auto-approval review must not be treated as feedback."

$editedFeedback = [pscustomobject]@{
    id = "old-comment"
    kind = "issue_comment"
    authorLogin = "reviewer"
    authorType = "User"
    body = "Edited feedback"
    updatedAt = "2026-08-12T12:00:10Z"
    isGitarDashboard = $false
}
$edited = Resolve-ReviewOutcome -Baseline $baseline -Snapshot (New-Snapshot -Checks @((New-Check)) -Dashboard $approvedDashboard -FeedbackItems @($editedFeedback)) -ExpectedSha "new-sha"
Assert-Equal "feedback" $edited.status "Edited feedback should wake the audit even when its ID was in the baseline."

$oldHeadPropagating = Resolve-ReviewOutcome -Baseline $baseline -Snapshot (New-Snapshot -HeadSha "old-sha") -ExpectedSha "new-sha"
Assert-Equal "waiting" $oldHeadPropagating.status "The pre-push HEAD may remain visible briefly while the push propagates."
$changedHead = Resolve-ReviewOutcome -Baseline $baseline -Snapshot (New-Snapshot -HeadSha "other-sha") -ExpectedSha "new-sha"
Assert-Equal "head_changed" $changedHead.status "An unexpected HEAD must stop the watcher."
$closed = Resolve-ReviewOutcome -Baseline $baseline -Snapshot (New-Snapshot -State "MERGED") -ExpectedSha "new-sha"
Assert-Equal "pr_closed" $closed.status "A closed PR must stop the watcher."

$unchangedHeadBaseline = [pscustomobject]@{
    agentLogin = "example-agent"
    baselineHeadSha = "new-sha"
    seenFeedbackVersions = @()
    gitarDashboard = $null
}
$existingCurrentApproval = Resolve-ReviewOutcome -Baseline $unchangedHeadBaseline -Snapshot (New-Snapshot -Checks @((New-Check)) -Dashboard $approvedDashboard) -ExpectedSha "new-sha"
Assert-Equal "approved" $existingCurrentApproval.status "A successful current-HEAD Gitar check should support the no-push path."

$olderCheck = New-Check -StartedAt "2026-08-12T10:00:00Z"
$olderCheck.id = 1
$newerCheck = New-Check -Status "in_progress" -Conclusion "" -StartedAt "2026-08-12T12:00:00Z"
$newerCheck.id = 2
$latestCheck = @(Get-LatestGitarCheck -Snapshot (New-Snapshot -Checks @($olderCheck, $newerCheck)))[0]
Assert-Equal 2 $latestCheck.id "The latest exact-HEAD Gitar run should control convergence."

$routing = Resolve-RepositoryRouting -Repository "ghe.example/owner/repository"
Assert-Equal "ghe.example" $routing.hostname "Host-qualified repositories should preserve their host."
Assert-Equal "owner/repository" $routing.apiRepository "API routing should strip the host."

$baselinePath = Join-Path ([IO.Path]::GetTempPath()) "finish-pr-gitar-baseline-test.json"
function Invoke-GhJson {
    param([string[]]$GhArgs)

    if ($GhArgs[0] -eq "api" -and $GhArgs[-1] -eq "user") {
        return [pscustomobject]@{ login = "example-agent" }
    }
    throw "Unexpected gh call: $($GhArgs -join ' ')"
}
function Get-PrReviewSnapshot {
    return New-Snapshot -Dashboard (New-Dashboard -Verdict "Approved") -FeedbackItems @($humanFeedback)
}

try {
    $CaptureBaseline = $true
    $StatePath = $baselinePath
    $PrNumber = 25
    $Repository = "owner/repository"
    $Hostname = "ghe.example"
    Invoke-PrReviewWatcher | Out-Null

    $captured = Get-Content -Raw -LiteralPath $baselinePath | ConvertFrom-Json -Depth 100
    Assert-Equal "ghe.example/owner/repository" $captured.repository "Baseline capture should retain repository routing."
    Assert-Equal "example-agent" $captured.agentLogin "Baseline capture should record the authenticated agent."
    Assert-Equal "human-comment" $captured.seenFeedbackVersions[0].id "Baseline capture should version feedback IDs."
    Assert-Equal "dashboard" $captured.gitarDashboard.id "Baseline capture should retain the dashboard version."
}
finally {
    Remove-Item -LiteralPath $baselinePath -ErrorAction SilentlyContinue
}

Write-Output "All wait-for-pr-review tests passed."

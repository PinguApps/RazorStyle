[CmdletBinding(DefaultParameterSetName = "Wait")]
param(
    [Parameter(Mandatory = $true, ParameterSetName = "Capture")]
    [switch]$CaptureBaseline,

    [Parameter(Mandatory = $true, ParameterSetName = "Wait")]
    [switch]$Wait,

    [Parameter(Mandatory = $true)]
    [string]$StatePath,

    [int]$PrNumber,

    [Parameter(ParameterSetName = "Capture")]
    [string]$Repository,

    [Parameter(ParameterSetName = "Capture")]
    [string]$Hostname,

    [Parameter(Mandatory = $true, ParameterSetName = "Wait")]
    [string]$ExpectedHeadSha,

    [Parameter(Mandatory = $true, ParameterSetName = "Wait")]
    [DateTimeOffset]$ReviewRequestedAt,

    [Parameter(ParameterSetName = "Wait")]
    [ValidateRange(1, 120)]
    [int]$TimeoutMinutes = 25,

    [Parameter(ParameterSetName = "Wait")]
    [ValidateRange(5, 300)]
    [int]$PollSeconds = 20,

    [Parameter(ParameterSetName = "Wait")]
    [ValidateRange(0, 300)]
    [int]$ReviewStartGraceSeconds = 0
)

$ErrorActionPreference = "Stop"

function Invoke-GhJson {
    param([Parameter(Mandatory = $true)][string[]]$GhArgs)

    $output = & gh @GhArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw ($output -join [Environment]::NewLine)
    }

    $json = $output -join [Environment]::NewLine
    if ([string]::IsNullOrWhiteSpace($json)) {
        return $null
    }

    return $json | ConvertFrom-Json -Depth 100
}

function Normalize-Login {
    param([AllowNull()][string]$Login)

    if ([string]::IsNullOrWhiteSpace($Login)) {
        return ""
    }

    return $Login.Trim().ToLowerInvariant() -replace '\[bot\]$', ''
}

function Test-GitarActor {
    param(
        [AllowNull()][string]$Login,
        [AllowNull()][string]$AuthorType
    )

    $normalizedLogin = Normalize-Login $Login
    return $normalizedLogin -match '(?i)(?:^|[-_])gitar(?:$|[-_])' -and
        $AuthorType -in @("App", "Bot", "Organization")
}

function Expand-PaginatedItems {
    param([AllowNull()]$Pages)

    foreach ($page in @($Pages)) {
        foreach ($item in @($page)) {
            $item
        }
    }
}

function Resolve-RepositoryRouting {
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [string]$Hostname
    )

    $parts = $Repository.Split("/")
    if ($parts.Count -eq 3) {
        if ([string]::IsNullOrWhiteSpace($Hostname)) {
            $Hostname = $parts[0]
        } elseif ($Hostname -ne $parts[0]) {
            throw "-Hostname does not match the hostname in -Repository."
        }
        $apiRepository = "$($parts[1])/$($parts[2])"
    } elseif ($parts.Count -eq 2) {
        $apiRepository = $Repository
    } else {
        throw "-Repository must use the owner/name or hostname/owner/name format."
    }

    $selector = if ([string]::IsNullOrWhiteSpace($Hostname)) {
        $apiRepository
    } else {
        "$Hostname/$apiRepository"
    }

    return [pscustomobject]@{
        hostname = $Hostname
        apiRepository = $apiRepository
        selector = $selector
    }
}

function Test-GitarDashboardBody {
    param([AllowNull()][string]$Body)

    if ([string]::IsNullOrWhiteSpace($Body)) {
        return $false
    }

    return $Body -match '(?is)<b>\s*Code Review\s*</b>|^\s*#{1,6}\s+Code Review\b'
}

function Get-GitarVerdict {
    param([AllowNull()][string]$Body)

    if (-not (Test-GitarDashboardBody $Body)) {
        return "unknown"
    }

    if ($Body -notmatch '(?is)<b>\s*Code Review\s*</b>\s*<kbd>\s*(?<verdict>[^<]+?)\s*</kbd>') {
        return "unknown"
    }

    $verdict = $Matches.verdict

    if ($verdict -match '(?i)Approved\s+with\s+Suggestions') {
        return "approved_with_suggestions"
    }
    if ($verdict -match '(?i)Changes\s+Requested') {
        return "changes_requested"
    }
    if ($verdict -match '(?i)Needs?\s+Review') {
        return "needs_review"
    }
    if ($verdict -match '(?i)Blocked') {
        return "blocked"
    }
    if ($verdict -match '(?i)Approved') {
        return "approved"
    }
    if ($verdict -match '(?i)Processing|In\s+Progress|Reviewing') {
        return "processing"
    }

    return "unknown"
}

function Test-ActionableFeedbackItem {
    param([Parameter(Mandatory = $true)]$Item)

    $body = [string]$Item.body
    if ([string]::IsNullOrWhiteSpace($body)) {
        return $false
    }

    if ($Item.kind -eq "review") {
        if ($Item.reviewState -in @("DISMISSED", "PENDING")) {
            return $false
        }

        if ((Test-GitarActor -Login $Item.authorLogin -AuthorType $Item.authorType) -and
            $body.Trim() -match '(?i)^Gitar has auto-approved this PR\b') {
            return $false
        }

        if ($body -match '(?i)reviewed\s+\d+\s+out\s+of\s+\d+\s+changed files.*generated no new comments') {
            return $false
        }
    }

    return $body.Trim() -notmatch '^(?:lgtm|looks good(?: to me)?|approved|acknowledged|thanks|thank you|done|👍)[.! ]*$'
}

function Get-PrReviewSnapshot {
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][int]$Number,
        [string]$Hostname,
        [ValidateRange(1, 3)][int]$Attempt = 1
    )

    $routing = Resolve-RepositoryRouting -Repository $Repository -Hostname $Hostname
    $pr = Invoke-GhJson @(
        "pr", "view", $Number.ToString(), "--repo", $routing.selector,
        "--json", "number,url,state,headRefOid"
    )

    $apiPrefix = @("api")
    if (-not [string]::IsNullOrWhiteSpace($routing.hostname)) {
        $apiPrefix += @("--hostname", $routing.hostname)
    }

    $commentPages = Invoke-GhJson ($apiPrefix + @(
        "repos/$($routing.apiRepository)/issues/$Number/comments", "--paginate", "--slurp"
    ))
    $comments = @(Expand-PaginatedItems $commentPages)

    $reviewPages = Invoke-GhJson ($apiPrefix + @(
        "repos/$($routing.apiRepository)/pulls/$Number/reviews", "--paginate", "--slurp"
    ))
    $reviews = @(Expand-PaginatedItems $reviewPages)

    $threadScript = Join-Path $PSScriptRoot "get-unresolved-pr-threads.ps1"
    $threadJson = & $threadScript `
        -PrNumber $Number `
        -Repository $routing.selector `
        -Hostname $routing.hostname `
        -All
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to fetch pull request review threads."
    }
    $threadData = ($threadJson -join [Environment]::NewLine) | ConvertFrom-Json -Depth 100

    $checkResponse = Invoke-GhJson ($apiPrefix + @(
        "-H", "Accept: application/vnd.github+json",
        "repos/$($routing.apiRepository)/commits/$($pr.headRefOid)/check-runs?filter=latest&per_page=100"
    ))
    $gitarChecks = @($checkResponse.check_runs | Where-Object {
        $_.name -eq "Gitar" -and
        ([string]$_.app.slug -match '(?i)(?:^|[-_])gitar(?:$|[-_])')
    } | ForEach-Object {
        [pscustomobject]@{
            id = [long]$_.id
            name = [string]$_.name
            appSlug = [string]$_.app.slug
            status = [string]$_.status
            conclusion = [string]$_.conclusion
            startedAt = $_.started_at
            completedAt = $_.completed_at
            url = [string]$_.html_url
            detailsUrl = [string]$_.details_url
            headSha = [string]$_.head_sha
        }
    })

    $feedbackItems = @()
    foreach ($thread in @($threadData.threads)) {
        if ([bool]$thread.isResolved) {
            continue
        }

        foreach ($comment in @($thread.comments.nodes)) {
            $feedbackItems += [pscustomobject]@{
                id = [string]$comment.id
                kind = "thread_comment"
                authorLogin = [string]$comment.author.login
                authorType = [string]$comment.author.__typename
                body = [string]$comment.body
                url = [string]$comment.url
                createdAt = $comment.createdAt
                updatedAt = $comment.updatedAt
                threadId = [string]$thread.id
                headSha = [string]$comment.pullRequestReview.commit.oid
                isGitarDashboard = $false
            }
        }
    }

    foreach ($comment in $comments) {
        $isGitarDashboard = (Test-GitarActor -Login $comment.user.login -AuthorType $comment.user.type) -and
            (Test-GitarDashboardBody -Body $comment.body)
        $feedbackItems += [pscustomobject]@{
            id = [string]$comment.node_id
            kind = "issue_comment"
            authorLogin = [string]$comment.user.login
            authorType = [string]$comment.user.type
            body = [string]$comment.body
            url = [string]$comment.html_url
            createdAt = $comment.created_at
            updatedAt = $comment.updated_at
            threadId = $null
            headSha = $null
            isGitarDashboard = $isGitarDashboard
        }
    }

    foreach ($review in $reviews) {
        $feedbackItems += [pscustomobject]@{
            id = [string]$review.node_id
            kind = "review"
            authorLogin = [string]$review.user.login
            authorType = [string]$review.user.type
            body = [string]$review.body
            url = [string]$review.html_url
            createdAt = $review.submitted_at
            updatedAt = $review.submitted_at
            reviewState = ([string]$review.state).ToUpperInvariant()
            threadId = $null
            headSha = [string]$review.commit_id
            isGitarDashboard = $false
        }
    }

    $dashboard = @($feedbackItems | Where-Object { $_.isGitarDashboard } |
        Sort-Object { [DateTimeOffset]$_.updatedAt } -Descending |
        Select-Object -First 1)
    $selectedDashboard = if ($dashboard.Count -eq 0) { $null } else { $dashboard[0] }

    $verifiedPr = Invoke-GhJson @(
        "pr", "view", $Number.ToString(), "--repo", $routing.selector,
        "--json", "headRefOid"
    )
    if ($verifiedPr.headRefOid -ne $pr.headRefOid) {
        if ($Attempt -ge 3) {
            throw "Pull request HEAD changed repeatedly while collecting a review snapshot."
        }

        return Get-PrReviewSnapshot `
            -Repository $Repository `
            -Number $Number `
            -Hostname $Hostname `
            -Attempt ($Attempt + 1)
    }

    return [pscustomobject]@{
        pullRequest = [pscustomobject]@{
            number = [int]$pr.number
            url = [string]$pr.url
            state = [string]$pr.state
            headSha = [string]$pr.headRefOid
        }
        feedbackItems = @($feedbackItems)
        gitarChecks = @($gitarChecks)
        gitarDashboard = $selectedDashboard
    }
}

function Get-LatestGitarCheck {
    param([Parameter(Mandatory = $true)]$Snapshot)

    return @($Snapshot.gitarChecks |
        Sort-Object { if ($_.startedAt) { [DateTimeOffset]$_.startedAt } else { [DateTimeOffset]::MinValue } } -Descending |
        Select-Object -First 1)
}

function Get-NewFeedback {
    param(
        [Parameter(Mandatory = $true)]$Baseline,
        [Parameter(Mandatory = $true)]$Snapshot
    )

    $agentLogin = Normalize-Login ([string]$Baseline.agentLogin)
    $seenVersions = @($Baseline.seenFeedbackVersions)
    return @($Snapshot.feedbackItems | Where-Object {
        if ($_.isGitarDashboard -or
            (Normalize-Login $_.authorLogin) -eq $agentLogin -or
            -not (Test-ActionableFeedbackItem $_)) {
            return $false
        }

        $seen = $seenVersions | Where-Object id -eq $_.id | Select-Object -First 1
        if ($null -eq $seen) {
            return $true
        }

        return [string]$_.updatedAt -ne [string]$seen.updatedAt -or
            [string]$_.body -ne [string]$seen.body
    })
}

function Test-DashboardFreshForCheck {
    param(
        [Parameter(Mandatory = $true)]$Baseline,
        [AllowNull()]$Dashboard,
        [AllowNull()]$Check,
        [Parameter(Mandatory = $true)][string]$ExpectedSha
    )

    if ($null -eq $Dashboard -or $null -eq $Check) {
        return $false
    }

    if ([string]$Baseline.baselineHeadSha -eq $ExpectedSha) {
        return $true
    }

    $baselineDashboard = $Baseline.gitarDashboard
    $changed = $null -eq $baselineDashboard -or
        [string]$baselineDashboard.id -ne [string]$Dashboard.id -or
        [string]$baselineDashboard.updatedAt -ne [string]$Dashboard.updatedAt -or
        [string]$baselineDashboard.body -ne [string]$Dashboard.body
    if (-not $changed) {
        return $false
    }

    if ([string]::IsNullOrWhiteSpace([string]$Dashboard.updatedAt) -or
        [string]::IsNullOrWhiteSpace([string]$Check.startedAt)) {
        return $true
    }

    return [DateTimeOffset]$Dashboard.updatedAt -ge ([DateTimeOffset]$Check.startedAt).AddSeconds(-5)
}

function Resolve-ReviewOutcome {
    param(
        [Parameter(Mandatory = $true)]$Baseline,
        [Parameter(Mandatory = $true)]$Snapshot,
        [Parameter(Mandatory = $true)][string]$ExpectedSha
    )

    if ($Snapshot.pullRequest.state -ne "OPEN") {
        return [pscustomobject]@{ status = "pr_closed"; verdict = "unknown"; check = $null; newFeedback = @() }
    }

    if ($Snapshot.pullRequest.headSha -ne $ExpectedSha) {
        if ([string]$Snapshot.pullRequest.headSha -eq [string]$Baseline.baselineHeadSha) {
            return [pscustomobject]@{ status = "waiting"; verdict = "unknown"; check = $null; newFeedback = @() }
        }

        return [pscustomobject]@{ status = "head_changed"; verdict = "unknown"; check = $null; newFeedback = @() }
    }

    $newFeedback = @(Get-NewFeedback -Baseline $Baseline -Snapshot $Snapshot)
    if ($newFeedback.Count -gt 0) {
        return [pscustomobject]@{ status = "feedback"; verdict = "unknown"; check = $null; newFeedback = $newFeedback }
    }

    $checkArray = @(Get-LatestGitarCheck -Snapshot $Snapshot)
    $check = if ($checkArray.Count -eq 0) { $null } else { $checkArray[0] }
    if ($null -eq $check) {
        return [pscustomobject]@{ status = "waiting"; verdict = "unknown"; check = $null; newFeedback = @() }
    }

    if ($check.status -ne "completed") {
        return [pscustomobject]@{ status = "processing"; verdict = "processing"; check = $check; newFeedback = @() }
    }

    if ($check.conclusion -ne "success") {
        return [pscustomobject]@{ status = "gitar_failed"; verdict = "unknown"; check = $check; newFeedback = @() }
    }

    if (-not (Test-DashboardFreshForCheck -Baseline $Baseline -Dashboard $Snapshot.gitarDashboard -Check $check -ExpectedSha $ExpectedSha)) {
        return [pscustomobject]@{ status = "waiting"; verdict = "unknown"; check = $check; newFeedback = @() }
    }

    $verdict = Get-GitarVerdict -Body $Snapshot.gitarDashboard.body
    if ($verdict -eq "approved") {
        return [pscustomobject]@{ status = "approved"; verdict = $verdict; check = $check; newFeedback = @() }
    }

    if ($verdict -in @("approved_with_suggestions", "changes_requested", "needs_review", "blocked")) {
        $dashboardFeedback = [pscustomobject]@{
            id = [string]$Snapshot.gitarDashboard.id
            kind = "gitar_dashboard"
            authorLogin = [string]$Snapshot.gitarDashboard.authorLogin
            authorType = [string]$Snapshot.gitarDashboard.authorType
            body = [string]$Snapshot.gitarDashboard.body
            url = [string]$Snapshot.gitarDashboard.url
            createdAt = $Snapshot.gitarDashboard.createdAt
            updatedAt = $Snapshot.gitarDashboard.updatedAt
            threadId = $null
            headSha = $ExpectedSha
            verdict = $verdict
        }
        return [pscustomobject]@{ status = "feedback"; verdict = $verdict; check = $check; newFeedback = @($dashboardFeedback) }
    }

    return [pscustomobject]@{ status = "processing"; verdict = $verdict; check = $check; newFeedback = @() }
}

function Save-ReviewState {
    param(
        [Parameter(Mandatory = $true)]$State,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    $State | ConvertTo-Json -Depth 100 | Set-Content -Encoding utf8 -LiteralPath $Path
}

function Invoke-PrReviewWatcher {
    if ($CaptureBaseline) {
        if ([string]::IsNullOrWhiteSpace($Repository)) {
            $repo = Invoke-GhJson @("repo", "view", "--json", "owner,name,url")
            $Repository = "$($repo.owner.login)/$($repo.name)"
            if ([string]::IsNullOrWhiteSpace($Hostname)) {
                $Hostname = ([uri]$repo.url).Authority
            }
        }

        $routing = Resolve-RepositoryRouting -Repository $Repository -Hostname $Hostname
        if ($PrNumber -le 0) {
            $pr = Invoke-GhJson @(
                "pr", "view", "--repo", $routing.selector, "--json", "number"
            )
            $PrNumber = [int]$pr.number
        }

        $viewerArgs = @("api")
        if (-not [string]::IsNullOrWhiteSpace($routing.hostname)) {
            $viewerArgs += @("--hostname", $routing.hostname)
        }
        $viewerArgs += @("user")
        $viewer = Invoke-GhJson $viewerArgs
        $snapshot = Get-PrReviewSnapshot `
            -Repository $routing.selector `
            -Number $PrNumber `
            -Hostname $routing.hostname

        $dashboardState = if ($null -eq $snapshot.gitarDashboard) {
            $null
        } else {
            [pscustomobject]@{
                id = [string]$snapshot.gitarDashboard.id
                body = [string]$snapshot.gitarDashboard.body
                updatedAt = $snapshot.gitarDashboard.updatedAt
            }
        }
        $state = [pscustomobject]@{
            repository = $routing.selector
            hostname = $routing.hostname
            prNumber = $PrNumber
            agentLogin = Normalize-Login $viewer.login
            baselineHeadSha = [string]$snapshot.pullRequest.headSha
            capturedAt = [DateTimeOffset]::UtcNow
            seenFeedbackVersions = @($snapshot.feedbackItems | ForEach-Object {
                [pscustomobject]@{
                    id = [string]$_.id
                    body = [string]$_.body
                    updatedAt = $_.updatedAt
                }
            })
            gitarDashboard = $dashboardState
        }
        Save-ReviewState -State $state -Path $StatePath

        [pscustomobject]@{
            status = "baseline_captured"
            statePath = $StatePath
            repository = $routing.selector
            prNumber = $PrNumber
            headSha = $state.baselineHeadSha
        } | ConvertTo-Json -Depth 10
        return
    }

    if (-not (Test-Path -LiteralPath $StatePath)) {
        throw "Review baseline state does not exist: $StatePath"
    }

    $state = Get-Content -Raw -LiteralPath $StatePath | ConvertFrom-Json -Depth 100
    $deadline = [DateTimeOffset]::UtcNow.AddMinutes($TimeoutMinutes)
    $reviewStartDeadline = if ($ReviewStartGraceSeconds -gt 0) {
        $start = if ($ReviewRequestedAt -eq [DateTimeOffset]::MinValue) {
            [DateTimeOffset]::UtcNow
        } else {
            $ReviewRequestedAt
        }
        $start.AddSeconds($ReviewStartGraceSeconds)
    } else {
        [DateTimeOffset]::MaxValue
    }

    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $snapshot = Get-PrReviewSnapshot `
            -Repository $state.repository `
            -Number ([int]$state.prNumber) `
            -Hostname ([string]$state.hostname)
        $outcome = Resolve-ReviewOutcome `
            -Baseline $state `
            -Snapshot $snapshot `
            -ExpectedSha $ExpectedHeadSha

        if ($outcome.status -in @("feedback", "approved", "gitar_failed", "head_changed", "pr_closed")) {
            [pscustomobject]@{
                status = $outcome.status
                repository = $state.repository
                prNumber = $state.prNumber
                expectedHeadSha = $ExpectedHeadSha
                actualHeadSha = $snapshot.pullRequest.headSha
                gitarVerdict = $outcome.verdict
                gitarCheck = $outcome.check
                newFeedback = @($outcome.newFeedback)
            } | ConvertTo-Json -Depth 100
            return
        }

        if ($ReviewStartGraceSeconds -gt 0 -and
            [DateTimeOffset]::UtcNow -ge $reviewStartDeadline -and
            $snapshot.pullRequest.headSha -eq $ExpectedHeadSha -and
            $null -eq $outcome.check) {
            [pscustomobject]@{
                status = "review_not_started"
                repository = $state.repository
                prNumber = $state.prNumber
                expectedHeadSha = $ExpectedHeadSha
                actualHeadSha = $snapshot.pullRequest.headSha
                gitarVerdict = "unknown"
                gitarCheck = $null
                newFeedback = @()
            } | ConvertTo-Json -Depth 20
            return
        }

        Start-Sleep -Seconds $PollSeconds
    }

    [pscustomobject]@{
        status = "timeout"
        repository = $state.repository
        prNumber = $state.prNumber
        expectedHeadSha = $ExpectedHeadSha
        timeoutMinutes = $TimeoutMinutes
        newFeedback = @()
    } | ConvertTo-Json -Depth 20
}

if ($MyInvocation.InvocationName -ne ".") {
    Invoke-PrReviewWatcher
}

[CmdletBinding(DefaultParameterSetName = "Body")]
param(
    [Parameter(Mandatory = $true)]
    [string]$ThreadId,

    [string]$Hostname,

    [Parameter(Mandatory = $true, ParameterSetName = "Body")]
    [string]$Body,

    [Parameter(Mandatory = $true, ParameterSetName = "File")]
    [string]$BodyFile
)

$ErrorActionPreference = "Stop"

if ($PSCmdlet.ParameterSetName -eq "File") {
    $Body = Get-Content -Raw -LiteralPath $BodyFile
}

function Invoke-GhJson {
    param([Parameter(Mandatory = $true)][string[]]$GhArgs)

    $args = @("api")
    if (-not [string]::IsNullOrWhiteSpace($Hostname)) {
        $args += @("--hostname", $Hostname)
    }
    $args += $GhArgs

    $output = & gh @args 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw ($output -join [Environment]::NewLine)
    }

    $json = $output -join [Environment]::NewLine
    if ([string]::IsNullOrWhiteSpace($json)) {
        throw "GitHub returned an empty JSON response."
    }

    return $json | ConvertFrom-Json -Depth 100
}

function Invoke-GhGraphQl {
    param(
        [Parameter(Mandatory = $true)][string]$Query,
        [Parameter(Mandatory = $true)][hashtable]$Variables
    )

    $ghArgs = @("graphql", "-f", "query=$Query")
    foreach ($entry in $Variables.GetEnumerator()) {
        $ghArgs += @("-f", "$($entry.Key)=$($entry.Value)")
    }

    return Invoke-GhJson -GhArgs $ghArgs
}

$threadContextQuery = @'
query($threadId:ID!) {
  node(id:$threadId) {
    ... on PullRequestReviewThread {
      pullRequest {
        number
        repository {
          nameWithOwner
        }
      }
      comments(first:1) {
        nodes {
          databaseId
        }
      }
    }
  }
}
'@

$threadContext = Invoke-GhGraphQl -Query $threadContextQuery -Variables @{
    threadId = $ThreadId
}
$thread = $threadContext.data.node
$pullRequest = $thread.pullRequest
$rootComment = @($thread.comments.nodes) | Select-Object -First 1

if ($null -eq $pullRequest -or
    [string]::IsNullOrWhiteSpace([string]$pullRequest.repository.nameWithOwner) -or
    [int]$pullRequest.number -le 0 -or
    [long]$rootComment.databaseId -le 0) {
    throw "GitHub returned no verifiable pull request context for review thread $ThreadId."
}

$reply = Invoke-GhJson -GhArgs @(
    "-X", "POST",
    "repos/$($pullRequest.repository.nameWithOwner)/pulls/$($pullRequest.number)/comments/$($rootComment.databaseId)/replies",
    "-f", "body=$Body"
)

if ([string]::IsNullOrWhiteSpace([string]$reply.node_id)) {
    throw "GitHub created no verifiable pull request review comment."
}

$verifyQuery = @'
query($commentId:ID!) {
  node(id:$commentId) {
    ... on PullRequestReviewComment {
      id
      url
      pullRequestReview {
        id
        state
        submittedAt
      }
    }
  }
}
'@

$verification = Invoke-GhGraphQl -Query $verifyQuery -Variables @{
    commentId = $reply.node_id
}
$verifiedComment = $verification.data.node

if ($null -eq $verifiedComment -or
    $null -eq $verifiedComment.pullRequestReview -or
    $verifiedComment.pullRequestReview.state -eq "PENDING" -or
    $null -eq $verifiedComment.pullRequestReview.submittedAt) {
    throw "Review reply $($reply.node_id) is still pending or could not be verified as submitted."
}

[pscustomobject]@{
    comment = [pscustomobject]@{
        id = $verifiedComment.id
        url = $verifiedComment.url
    }
    review = $verifiedComment.pullRequestReview
    submittedPendingReview = $false
    verifiedSubmitted = $true
} | ConvertTo-Json -Depth 10

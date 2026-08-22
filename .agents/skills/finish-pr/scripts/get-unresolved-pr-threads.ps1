[CmdletBinding()]
param(
    [int]$PrNumber,
    [string]$Repository,
    [string]$Hostname,
    [switch]$All
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

if ([string]::IsNullOrWhiteSpace($Repository)) {
    $repo = Invoke-GhJson @("repo", "view", "--json", "owner,name,url")
    $owner = [string]$repo.owner.login
    $name = [string]$repo.name
    $Repository = "$owner/$name"
    if ([string]::IsNullOrWhiteSpace($Hostname)) {
        $Hostname = ([uri]$repo.url).Authority
    }
} else {
    $repositoryParts = $Repository.Split("/")
    if ($repositoryParts.Count -eq 3) {
        if ([string]::IsNullOrWhiteSpace($Hostname)) {
            $Hostname = $repositoryParts[0]
        } elseif ($Hostname -ne $repositoryParts[0]) {
            throw "-Hostname does not match the hostname in -Repository."
        }
        $owner = $repositoryParts[1]
        $name = $repositoryParts[2]
    } elseif ($repositoryParts.Count -eq 2) {
        $owner = $repositoryParts[0]
        $name = $repositoryParts[1]
    } else {
        throw "-Repository must use the owner/name or hostname/owner/name format."
    }

    if ([string]::IsNullOrWhiteSpace($owner) -or [string]::IsNullOrWhiteSpace($name)) {
        throw "-Repository contains an empty owner or repository name."
    }
}

$repositorySelector = if ([string]::IsNullOrWhiteSpace($Hostname) -or
    $Repository.StartsWith("$Hostname/", [StringComparison]::OrdinalIgnoreCase)) {
    $Repository
} else {
    "$Hostname/$Repository"
}

if ($PrNumber -gt 0) {
    $pr = Invoke-GhJson @("pr", "view", $PrNumber.ToString(), "--repo", $repositorySelector, "--json", "number,title,url,headRefName,baseRefName")
} else {
    $pr = Invoke-GhJson @("pr", "view", "--repo", $repositorySelector, "--json", "number,title,url,headRefName,baseRefName")
    $PrNumber = [int]$pr.number
}

$query = @'
query($owner:String!, $name:String!, $number:Int!, $after:String) {
  repository(owner:$owner, name:$name) {
    pullRequest(number:$number) {
      reviewThreads(first:100, after:$after) {
        pageInfo {
          hasNextPage
          endCursor
        }
        nodes {
          id
          isResolved
          isOutdated
          path
          line
          startLine
          originalLine
          originalStartLine
          diffSide
          comments(first:100) {
            pageInfo {
              hasNextPage
              endCursor
            }
            nodes {
              id
              databaseId
              body
              author {
                login
                __typename
              }
              createdAt
              updatedAt
              url
              path
              diffHunk
              outdated
              pullRequestReview {
                id
                state
                submittedAt
                commit {
                  oid
                }
              }
            }
          }
        }
      }
    }
  }
}
'@

$threads = @()
$after = $null

do {
    $ghArgs = @(
        "api"
    )
    if (-not [string]::IsNullOrWhiteSpace($Hostname)) {
        $ghArgs += @("--hostname", $Hostname)
    }
    $ghArgs += @(
        "graphql",
        "-f", "owner=$owner",
        "-f", "name=$name",
        "-F", "number=$PrNumber",
        "-f", "query=$query"
    )

    if ($after) {
        $ghArgs += @("-f", "after=$after")
    }

    $result = Invoke-GhJson $ghArgs
    $page = $result.data.repository.pullRequest.reviewThreads
    if ($page.nodes) {
        $threads += $page.nodes
    }

    $after = $page.pageInfo.endCursor
} while ($page.pageInfo.hasNextPage)

$commentQuery = @'
query($threadId:ID!, $after:String) {
  node(id:$threadId) {
    ... on PullRequestReviewThread {
      comments(first:100, after:$after) {
        pageInfo {
          hasNextPage
          endCursor
        }
        nodes {
          id
          databaseId
          body
          author {
            login
            __typename
          }
          createdAt
          updatedAt
          url
          path
          diffHunk
          outdated
          pullRequestReview {
            id
            state
            submittedAt
            commit {
              oid
            }
          }
        }
      }
    }
  }
}
'@

foreach ($thread in $threads) {
    $commentAfter = $thread.comments.pageInfo.endCursor
    while ($thread.comments.pageInfo.hasNextPage) {
        $commentArgs = @(
            "api"
        )
        if (-not [string]::IsNullOrWhiteSpace($Hostname)) {
            $commentArgs += @("--hostname", $Hostname)
        }
        $commentArgs += @(
            "graphql",
            "-f", "threadId=$($thread.id)",
            "-f", "query=$commentQuery"
        )
        if ($commentAfter) {
            $commentArgs += @("-f", "after=$commentAfter")
        }

        $commentResult = Invoke-GhJson $commentArgs
        $commentPage = $commentResult.data.node.comments
        if ($commentPage.nodes) {
            $thread.comments.nodes = @($thread.comments.nodes) + @($commentPage.nodes)
        }

        $thread.comments.pageInfo = $commentPage.pageInfo
        $commentAfter = $commentPage.pageInfo.endCursor
    }
}

$unresolvedThreads = @($threads | Where-Object { -not $_.isResolved })
$selectedThreads = if ($All) { @($threads) } else { $unresolvedThreads }

[pscustomobject]@{
    repository = $Repository
    pullRequest = $pr
    unresolvedCount = $unresolvedThreads.Count
    threads = $selectedThreads
} | ConvertTo-Json -Depth 100

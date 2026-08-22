$ErrorActionPreference = "Stop"

$global:GhMockCalls = @()
function global:gh {
    $global:GhMockCalls += ,@($args)
    $global:LASTEXITCODE = 0

    switch ($global:GhMockCalls.Count) {
        1 {
            return '{"data":{"node":{"pullRequest":{"number":25,"repository":{"nameWithOwner":"example-owner/example-repository"}},"comments":{"nodes":[{"databaseId":101}]}}}}'
        }
        2 {
            return '{"node_id":"reply-1","html_url":"https://example.test/reply-1"}'
        }
        3 {
            return '{"data":{"node":{"id":"reply-1","url":"https://example.test/reply-1","pullRequestReview":{"id":"review-1","state":"COMMENTED","submittedAt":"2026-01-01T00:00:00Z"}}}}'
        }
        default {
            throw "Unexpected gh call."
        }
    }
}

try {
    $result = & "$PSScriptRoot\..\scripts\reply-to-review-thread.ps1" `
        -ThreadId "thread-1" `
        -Hostname "github.example" `
        -Body "test reply" |
      ConvertFrom-Json -Depth 20

    if (-not $result.verifiedSubmitted -or $result.review.state -ne "COMMENTED") {
        throw "Expected a verified submitted reply."
    }

    if ($global:GhMockCalls.Count -ne 3) {
        throw "Expected context, reply, and verification calls."
    }

    $replyCall = $global:GhMockCalls[1] -join " "
    if ($replyCall -notmatch '--hostname github\.example' -or
        $replyCall -notmatch '-X POST' -or
        $replyCall -notmatch 'repos/example-owner/example-repository/pulls/25/comments/101/replies' -or
        $replyCall -notmatch 'body=test reply') {
        throw "The helper did not use the atomic single-comment reply endpoint: $replyCall"
    }

    $allCalls = $global:GhMockCalls | ForEach-Object { $_ -join " " }
    if (($allCalls -join [Environment]::NewLine) -match 'submitPullRequestReview') {
        throw "The helper must not submit a shared pending review."
    }
}
finally {
    Remove-Item Function:\global:gh -ErrorAction SilentlyContinue
    Remove-Variable GhMockCalls -Scope Global -ErrorAction SilentlyContinue
}

Write-Output "All reply-to-review-thread tests passed."

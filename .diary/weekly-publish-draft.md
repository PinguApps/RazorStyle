## Rolling state
- Goal: Mirror Aspire.Hosting.Upstash.Redis commit dafedf4, publishing release drafts weekly when main advances.
- Current plan: Open PR and complete CI/review via finish-pr.
- Open questions/risks: Workflow has not been executed on GitHub; publishing a draft with GITHUB_TOKEN requires the direct reusable publish call.
- Next actions: Commit, push, open PR, and audit review.
- Key paths: `.github/workflows/publish-release-draft.yml`, `.github/workflows/publish.yml`

## Session log
### 2026-09-28 14:23 +01:00 (agent/weekly-publish-draft)
- Add weekly release draft publisher [build] (impact: med)
  - Why: Mirror upstream commit dafedf4 for RazorStyle releases.
  - Change: Copied the upstream scheduled/manual workflow and made package publishing reusable with safe tag passing (files: `.github/workflows/publish-release-draft.yml`, `.github/workflows/publish.yml`).
  - Notes: Restore, build, 20 tests, pack, and actionlint passed; actionlint ignored the repo's existing Blacksmith runner label.

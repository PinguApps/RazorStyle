## Rolling state
- Goal: Add approved package icons and embed them in NuGet artifacts.
- State: Icon metadata and PNGs added; packed icons match approved SHA-256 hashes.
- Release: Preserve existing Sunday publishing; no manual release.
- Next: Commit, push, and verify the weekly publishing configuration.

## Session log
### 2026-10-08 (feature/nuget-package-icons)
- Add approved original penguin package icons [build] (impact: low).
  - Change: PackageIcon metadata and packed icon.png assets.
  - Verify: Release pack and embedded PNG hash checks passed.

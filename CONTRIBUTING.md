# Contributing to kwcmux

## Collaboration model

- Public repository with protected `master`
- No direct push to `master` for collaborators
- All changes go through Pull Requests
- At least 1 approval is required before merge
- Code owner review is required

## Permission policy (read/write granularity)

- `Read/Triage`: bug triage, issue labeling, discussion, reproduction
- `Write`: push to feature branches, open PRs, update docs/tests
- `Maintainer/Owner`: branch protection updates, release tagging, merge decisions

If you need elevated permissions, open an issue describing why.

## Development flow

1. Create a branch from `master`
2. Make focused changes
3. Run checks:

```powershell
dotnet build Cmux.sln -c Debug
dotnet test Cmux.sln -c Debug
```

4. Open PR with:
   - What changed
   - Why it changed
   - How it was tested

## PR expectations

- Keep PR scope focused and small
- Include screenshots for UI changes
- Link related issues
- Update docs if user-facing behavior changed

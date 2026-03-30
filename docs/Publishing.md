# Publishing

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [GitHub CLI (`gh`)](https://cli.github.com) — for creating GitHub releases

### One-time GitHub authentication

```
gh auth login
```

This opens a browser, authenticates you, and stores credentials in the OS keyring. You will not need to log in again unless you explicitly log out.

---

## Before publishing: bump the version

Edit `changelog.md` and add a new section at the top:

```markdown
# 1.8.0
- describe what changed
```

The version number is automatically read from the first `# X.Y.Z` heading in `changelog.md`.

---

## Local test build

Build all three platform ZIPs without touching git or GitHub. Useful for checking the release artifacts before creating an actual release.

**Windows:**
```powershell
.\publish.ps1 -LocalOnly
```

**Linux/macOS:**
```bash
./publish.sh --local-only
```

The ZIPs are created in the repo root: `AzgaarToCK3_{version}_{rid}.zip`.

---

## Full release

Creates a git tag, pushes it, builds all platforms, and creates a GitHub release with the ZIPs attached as assets.

**Windows:**
```powershell
.\publish.ps1
```

**Linux/macOS** (make executable once with `chmod +x publish.sh`):
```bash
./publish.sh
```

---

## What the scripts do

1. Check prerequisites (`dotnet`, `gh`, auth)
2. Extract version from `changelog.md`
3. Abort if the git tag already exists
4. Abort if the GitHub release already exists
5. Create and push the git tag
6. `dotnet publish` for `win-x64` (AOT), `osx-x64`, and `linux-x64`
7. Create ZIP archives including `Readme.md`
8. Create the GitHub release with the ZIPs as assets

---

## Safety rules

The scripts will fail with an error and abort **without making any changes** if:

- The git tag for the current version already exists → bump the version in `changelog.md`
- The GitHub release for the current version already exists → same resolution
- `gh auth login` has not been run → run it once

Tags are never force-deleted or overwritten.

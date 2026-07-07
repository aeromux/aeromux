---
name: version-bump
description: Bump the Aeromux version number across the repository. Use when the user gives a new version (e.g. "bump to 0.7.5", "/version-bump 0.7.5") and wants Directory.Build.props, the CHANGELOG heading, and the illustrative version strings in README/docs updated consistently.
allowed-tools: Read, Edit, Grep, Glob, Bash
---

# Version Bump Skill

Sets a new project version number in one pass, keeping the single source of
truth and every illustrative version string in the docs consistent. Follows the
versioning workflow in `CLAUDE.md`, [Semantic Versioning](https://semver.org/),
and [Keep a Changelog](https://keepachangelog.com/).

## Input

The new version, given as an argument (`/version-bump 0.7.5`) or in the message.

1. It must be a SemVer `MAJOR.MINOR.PATCH` string (optionally a pre-release
   suffix like `0.8.0-rc1`). If it is missing or malformed, **stop and ask** for
   a valid version — do not guess.
2. Read the current version from `src/Directory.Build.props` for reference and
   sanity: warn (but continue if the user insists) if the new version is not
   strictly greater than the current one.

## What a *bump* is (and is not)

A bump only advances the version number and prepares the changelog heading. It
does **not** cut a release: it keeps `— Unreleased` in the changelog and does
**not** add a release date or the `[x.y.z]: …/releases/tag/vX.Y.Z` footnote link.
Those are a separate *release* step. Never commit — the user reviews the diff and
commits manually (project git rule in `CLAUDE.md`).

## Steps

### 1. Source of truth (always)

- `src/Directory.Build.props` — set `<Version>…</Version>` to the new version.
  All projects inherit it; the packaging/docker scripts read it at build time.

### 2. Changelog (always)

Edit `CHANGELOG.md`:

- If the **topmost** section heading is still `## [X.Y.Z] — Unreleased`, change
  `X.Y.Z` to the new version (the accumulated unreleased work becomes this
  version). Leave `— Unreleased` in place.
- If the topmost section is already **released** (dated, e.g.
  `## [X.Y.Z] — 2026-06-26`), insert a new
  `## [<new>] — Unreleased` section directly below the `# Changelog` intro and
  above that section. Leave its body for real entries; do not invent changes.
- Do **not** touch already-released sections or the footnote links at the bottom
  of the file (those exist only for released versions and are added at release
  time).

### 3. Illustrative version strings in docs (option A — keep everything coherent)

Replace the example version number wherever it appears in these files with the
new version, **regardless of its current value** (the docs may lag
`Directory.Build.props`). Preserve any Debian revision suffix (`-1`) and file
naming around the number.

- `README.md` — the `.deb` and `.pkg` install-command examples
  (`aeromux_<ver>-1_arm64.deb`, `aeromux_<ver>_macos_arm64.pkg`).
- `docs/PACKAGING-DEB.md` — `<ver>-1` package examples and the version table.
- `docs/PACKAGING-PKG.md` — `<ver>` package/notarization examples and the table.
- `docs/DOCKER.md` — the version-specific tag examples, tarball names, and table.
- `docs/API.md` — the `"Version": "<ver>"` field in the `GET /api/v1/stats`
  example response.

> These are illustrative "e.g." strings, not functional config. Update them so a
> reader copying a command gets the current version. If a grep turns up the
> version in a doc spot not listed here, use judgment: update it if it is an
> example of *this project's* release; leave it if it refers to a dependency
> version or an unrelated number.

### 4. Do NOT touch

- `packaging/package-deb.sh`, `packaging/package-pkg.sh`,
  `docker/build-image.sh` — they derive the version from `Directory.Build.props`
  at build time (a hardcoded number there would be a bug).
- `package-lock.json`, `**/obj/`, `**/bin/`, `artifacts/`, `node_modules/`.
- Dependency/badge versions (`.NET-10.0`, library versions, GHSA references).

### 5. Verify and report

- Grep the repo for the **previous** version numbers to confirm no intended
  target was missed and that the only remaining occurrences are deliberate
  (released-version changelog footnotes, dependency versions, historical
  changelog entries):
  ```bash
  grep -rnE "<old-props-ver>|<old-doc-ver>" README.md docs/ src/Directory.Build.props CHANGELOG.md
  ```
- Present a concise summary: the old → new version, the files changed, and any
  version occurrences you intentionally left. **Do not run any git
  state-changing command** and do not commit.

## Example

`/version-bump 0.7.5` →

- `src/Directory.Build.props`: `0.7.1` → `0.7.5`
- `CHANGELOG.md`: `## [0.7.1] — Unreleased` → `## [0.7.5] — Unreleased`
- `README.md` / `docs/PACKAGING-DEB.md` / `docs/PACKAGING-PKG.md` /
  `docs/DOCKER.md` / `docs/API.md`: example version strings `0.7.0` → `0.7.5`
- Report changed files; leave the commit to the user.

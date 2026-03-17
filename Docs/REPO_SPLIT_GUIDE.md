# Repo Split Guide

This repository currently mixes two different workflows:

1. Runtime and packaging work for the Everest mod.
2. Map authoring work for Loenn, Maple starters, and `loenn-mcp`.

That is workable in one repo, but it is not a good fit if you want lighter Git history, less storage pressure from maps and previews, and the ability to focus on one area at a time.

## Recommended Split

Use two sibling repositories:

### 1. `MaggyHelper-Code`

Keep the runtime and release side here:

- `Source/`
- `Code/`
- `Graphics/`
- `Dialog/`
- `Docs/`
- `.github/`
- root build and mod metadata files such as `everest.yaml`, `MaggyHelper.sln`, and the module entry files

This repo is the one you build, package, and release from.

### 2. `MaggyHelper-Maps`

Keep the map-authoring side here:

- `Maps/`
- `Mountain/`
- `Loenn/`
- `MapleStarters/`
- `loenn-mcp/`

This repo is the one you use for mapping, procedural generation, previewing `.bin` files, and Loenn-side authoring.

## Why This Layout

This split keeps the high-churn and large map-side content out of the runtime repo while still preserving a clean path to combine everything when you need a full playable mod.

The important coupling points are still clear:

- `Loenn/` definitions in the map repo must stay aligned with runtime entities and triggers in `Source/`.
- map SIDs and metadata in `Maps/` must stay aligned with runtime systems that reference them.
- release builds still need both repos combined into one mod folder before packaging.

## Automation Added Here

This repo now includes `Tools/Split-MaggyHelperRepos.ps1`.

What it does:

- creates two sibling folders from the current monorepo
- writes repo-specific `.gitignore` files
- writes repo-specific VS Code task files
- generates a multi-root workspace file so both repos can be opened together
- generates a sync script inside the map repo so map-side content can be copied back into the code repo when needed

For the GitHub-side setup after the local split, see `Docs/GITHUB_SPLIT_REPO_SETUP.md`.

## Normal Workflow After The Split

1. Run `Tools/Split-MaggyHelperRepos.ps1` from the current monorepo.
2. Open the generated `MaggyHelper-Split.code-workspace` file.
3. Do runtime work in `MaggyHelper-Code`.
4. Do map and Loenn MCP work in `MaggyHelper-Maps`.
5. When you need a full playable mod folder in the code repo, run `MaggyHelper-Maps/Tools/Sync-ToCodeRepo.ps1`.

## Important Limitations

- This does not create remote GitHub repositories for you.
- This does not rewrite the current repository history.
- This avoids moving files in-place inside the current repo, because that would be disruptive and risky while you still have active work here.

If you later want a full history-preserving split, do that as a dedicated Git migration step after the local workflow is stable.

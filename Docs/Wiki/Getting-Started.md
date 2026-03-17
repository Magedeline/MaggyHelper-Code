# Getting Started

This page is the shortest path to being productive in the repository without breaking project workflow.

## Prerequisites

- Celeste with Everest installed
- A .NET SDK compatible with the project build
- Loenn for map editing
- The helper mod dependencies declared in `everest.yaml`

## First-Time Setup

1. Clone the repository.
2. Open it in VS Code.
3. Build the project with `dotnet build Source/MaggyHelper.csproj`.
4. Confirm your target map or feature loads in the relevant toolchain.

## Branch Workflow

Use the team ruleset as the default workflow baseline.

1. Create a branch from `main` using `feature/<short-name>`.
2. Keep the PR focused on one logical change.
3. Use a draft PR early if the work will take time.
4. Re-request review after significant updates.

## Before You Change Anything

Understand which area you are touching:

- `Source/` for runtime code
- `Loenn/` for editor-facing entities and metadata
- `Maps/` for chapter and content maps
- `Dialog/` for localization and story text
- `Graphics/` for portraits, tiles, sprites, and effects

If your change spans multiple areas, split the work when practical so review stays tractable.

## Minimum Validation

Before opening or updating a PR:

1. Run `dotnet build Source/MaggyHelper.csproj`.
2. Validate the changed gameplay area in-game if behavior or content changed.
3. Check for new warnings in edited files and justify any that remain.
4. Update docs when the workflow, structure, or contributor expectations changed.

## When To Use Issues, PRs, and Discussions

- Use Issues for concrete bugs and actionable tasks.
- Use PRs for implementation and review.
- Use Discussions for setup questions, design exploration, and early proposals.

## Common Contributor Paths

### Code contributor

Start with `Source/`, then inspect the matching Loenn entity or map usage if the system is editor-visible.

### Map contributor

Start with the target map in `Maps/`, then check entity definitions in `Loenn/` and any supporting runtime logic in `Source/`.

### Dialog contributor

Start in `Dialog/English.txt`, verify key naming consistency, and validate the in-game scene flow where possible.

## First Questions To Answer

Before you start work, you should know:

1. What chapter, side, or feature is affected?
2. Which files define the runtime logic?
3. Which maps or dialog keys depend on that logic?
4. What proof will show the change is safe?

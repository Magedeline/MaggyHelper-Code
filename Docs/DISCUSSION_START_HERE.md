# Start Here: Contributing To Desolo Zantas

This file is a draft for a pinned GitHub Discussion post.

## Suggested Title

Start Here: Contributing to Desolo Zantas

## Suggested Body

Welcome. This repository contains a large Celeste mod and campaign with custom gameplay systems, maps, bosses, cutscenes, dialog, and editor integrations.

If you want to contribute, use this thread as the first stop for workflow and routing.

### Where to start

1. Read the README for the project overview.
2. Read `TEAM_RULESET.txt` for branch, review, and quality expectations.
3. Check the wiki pages for setup and mapping guidance.

### Use the right channel

- Use **Discussions** for setup questions, design proposals, contributor coordination, and documentation gaps.
- Use **Issues** for confirmed bugs, concrete tasks, and follow-up work.
- Use **Pull Requests** for implementation and code review.

### Branch workflow

1. Branch from `main` using `feature/<short-name>`.
2. Keep changes focused on one logical concern.
3. Use draft PRs early if the change is still in progress.
4. Re-request review after major updates.

### Minimum validation before PR review

1. Run `dotnet build Source/MaggyHelper.csproj`.
2. Validate the changed gameplay area in-game if behavior or content changed.
3. Note any risky map, dialog, or progression changes in the PR description.

### Good discussion topics

- Build setup questions
- Mapping workflow questions
- Chapter or boss design proposals
- Documentation gaps
- Playtest observations that still need triage

### When to open an Issue instead

Open an Issue when you have:

- A reproducible bug
- A concrete implementation task
- A follow-up item that should be tracked to completion

### Maintainer note

If the same question appears repeatedly, move the durable answer into the wiki and link it here.

## Suggested Links To Include Once Live

- README
- Wiki Home
- Getting Started
- Mapping Guide
- Issue tracker

# Desolo Zantas Wiki Starter

This file is a starter draft for the repository Wiki. It is written so the content can be copied into the GitHub wiki with minimal cleanup.

## Suggested Page Structure

1. Home
2. Getting Started
3. Installation and Dependencies
4. Development Workflow
5. Project Structure
6. Campaign Structure
7. Mapping Guide
8. Entity and Trigger Reference
9. Dialogue and Localization
10. Art, Audio, and Credits
11. Testing and Validation
12. Roadmap
13. FAQ

## Home

Welcome to the **Desolo Zantas / MaggyHelper** wiki.

This repository contains a large Celeste mod and campaign with custom gameplay systems, chapters, bosses, cutscenes, visuals, audio, and editor integrations. The wiki exists to make the project easier to navigate for contributors, playtesters, and map builders.

### What this project includes

- A multi-chapter campaign with A/B/C/D and DX-side content
- Custom entities, triggers, bosses, and cutscene logic
- Kirby player support with custom mechanics and abilities
- Loenn integration for custom editor entities
- Large dialog and asset pipelines spanning maps, portraits, sprites, and music

### Who this wiki is for

- Contributors working on code, maps, dialog, or assets
- Playtesters validating behavior and progression
- Maintainers reviewing process, ownership, and release readiness

### Recommended first pages

- Getting Started
- Development Workflow
- Campaign Structure
- Mapping Guide
- Testing and Validation

## Getting Started

### Prerequisites

- Celeste with Everest installed
- .NET SDK compatible with the project
- Loenn for map editing
- The helper mod dependencies declared in `everest.yaml`

### First local checks

1. Open the repository in VS Code.
2. Build the project with `dotnet build Source/MaggyHelper.csproj`.
3. Confirm the target map or feature opens in Loenn.
4. Validate any changed gameplay area in-game before opening a PR.

## Installation and Dependencies

Document here:

- Supported Everest version
- Required helper mods
- Optional tooling such as Loenn MCP scripts
- Any editor-only or playtest-only setup notes

## Development Workflow

Base this page on the team ruleset.

### Branching

- Create branches from `main` using `feature/<short-name>`
- Keep PRs small when practical
- Use draft PRs early for visibility

### Quality gates

- `dotnet build` must pass before merge
- Avoid introducing new warnings in edited files unless justified
- Add or update tests when behavior changes and tests are feasible

### Review expectations

- At least one reviewer approval before merge
- Resolve all review conversations
- Request re-review after significant changes

## Project Structure

Suggested overview:

- `Source/` for C# gameplay, entities, cutscenes, and systems
- `Loenn/` for editor plugins and metadata
- `Maps/` for chapter and content map files
- `Dialog/` for localization text
- `Graphics/` for sprites, portraits, atlases, and tiles
- `Docs/` for internal design and validation notes
- `.github/` for repository automation and policy

## Campaign Structure

Summarize the chapter layout and progression model.

- Chapters 0 through 20 form the main story arc
- Chapters 10 through 14 use the lobby and sub-map system
- Chapters 16, 19, and 20 use restart-gated progression
- Side content includes B, C, D, and DX variants for supported chapters

For the detailed campaign writeup, derive this page from `Docs/MOD_DESCRIPTION.md`.

## Mapping Guide

Include:

- Naming conventions for map files and rooms
- Main chapter versus lobby versus sub-map conventions
- Required metadata expectations
- Map validation checklist before merge
- Guidance for Loenn custom entities and triggers

## Entity and Trigger Reference

Start with the entities contributors touch most often.

- Boss and encounter systems
- Progression and chapter triggers
- Lobby and sub-map entities
- Cutscene helpers
- Character-specific helper entities

Each page section should answer:

- What it does
- Where it is defined
- How to place or configure it
- Common failure modes

## Dialogue and Localization

Document:

- Naming patterns used in `Dialog/English.txt`
- Cutscene dialog key conventions
- How to validate line flow and speaker tags
- Review expectations for typos and consistency

## Art, Audio, and Credits

This page should track:

- Asset ownership and sourcing rules
- Attribution requirements
- Chapter-specific credit obligations
- Release-time audit checklist for third-party assets

## Testing and Validation

Suggested sections:

- Build validation
- In-game smoke test checklist
- Chapter progression checks
- Map/content verification for edited rooms
- Playtest reporting format

## Roadmap

Useful headings:

- Current sprint
- Near-term polish items
- Backlog
- Release blockers

## FAQ

Seed questions:

- Which branch should new work start from?
- Where do chapter maps live?
- How do lobby chapters 10 to 14 differ from standard chapters?
- Where do I register a new Loenn entity?
- What must be validated before merging gameplay changes?

## Wiki Launch Checklist

1. Enable the GitHub wiki in repository settings.
2. Create the `Home` page using the content above.
3. Add sidebar links for the suggested page structure.
4. Move detailed campaign content from `Docs/MOD_DESCRIPTION.md` into dedicated wiki pages.
5. Keep rules and process aligned with `TEAM_RULESET.txt`.

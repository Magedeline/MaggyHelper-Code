# Mapping Guide

This page is the first-pass guide for contributors changing maps, lobbies, sub-maps, or editor-facing content.

## Repository Areas Relevant To Mapping

- `Maps/` contains chapter, side, and content map files
- `Loenn/` contains editor entity definitions, triggers, and supporting metadata
- `Source/` contains the runtime behavior behind custom entities and systems
- `Dialog/` contains text that often needs to stay aligned with map or cutscene changes

## Chapter Layout Model

The project has several mapping patterns.

### Standard chapter flow

Most chapters use a direct chapter map structure with side variants.

### Lobby and sub-map flow

Chapters 10 through 14 use a custom lobby system with:

- A main chapter entry
- A lobby map
- Multiple small maps
- Optional EX and boss maps

This is a custom project system, not Collab Utils. For deeper structure, use `Docs/MOD_DESCRIPTION.md` as the source of truth.

## Before Editing A Map

Confirm all of the following:

1. Which chapter and side you are modifying
2. Whether the map is a main entry, lobby, sub-map, EX map, or boss map
3. Whether the map depends on a custom entity or trigger
4. Whether the change affects dialog, progression flags, or cutscenes

## Mapping Conventions

Use these rules unless the chapter has an established exception.

- Preserve existing chapter naming patterns
- Keep room names descriptive and stable when they are referenced by code or cutscenes
- Avoid hidden logic inside maps without a matching note in the relevant doc or PR description
- Keep risky structural changes separate from cosmetic cleanup when practical

## Working With Custom Entities

When placing or editing a custom entity:

1. Check the Loenn definition under `Loenn/`.
2. Find the runtime implementation in `Source/`.
3. Identify the required attributes, flags, and intended room context.
4. Validate behavior in-game, not just in the editor.

## Lobby And Sub-Map Changes

For chapters 10 to 14, verify all transition logic after a map edit:

- Entry into the lobby
- Unlock flow for sub-maps
- Return flow back to the lobby
- EX and boss unlock conditions
- Any cutscene or completion trigger tied to the exit

## Dialog And Cutscene Coupling

Map changes frequently affect story flow. Recheck dialog or cutscene dependencies when you modify:

- Spawn positions
- Room names used by scripts
- Trigger placements
- Chapter completion flow

## Minimum Mapping Validation Checklist

1. Open the changed map in Loenn and verify entity placement.
2. Run the project build if runtime code changed.
3. Enter the edited area in-game.
4. Confirm transitions, flags, and cutscene entry points still behave correctly.
5. Confirm there are no obvious art, dialog, or progression regressions.

## PR Notes For Mapping Work

When opening a PR for mapping changes, include:

- The affected chapter and side
- Whether progression or triggers changed
- Whether custom entities were added or reconfigured
- What validation you performed
- Any rollback concerns if the change is high impact

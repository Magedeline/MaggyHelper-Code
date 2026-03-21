# Kirby Restart Plan

## Decision

The restart will proceed on two tracks:

1. Branch the current repository for analysis and selective extraction
2. Rebuild Kirby in a separate clean workspace so the new implementation does not inherit MaggyHelper's monolithic entrypoint and mixed responsibilities

## Current Kirby Surface Area

The existing repo spreads Kirby logic across these areas:

1. Runtime/player systems under Source/Extensions/Kirby and Source/Entities/Kirby
2. Mode and transformation triggers under Source/Triggers
3. NPC and cutscene paths under Source/KirbyNPC.cs and Source/NPCs
4. Loenn plugins under Loenn/entities, Loenn/triggers, and Loenn/scripts
5. Graphics and sprite banks under Graphics
6. Session/save state in the root module data types

## Keep

1. Asset references and art direction
2. Known compat lifecycle notes
3. Everest deployment pattern to Code/net8.0

## Rewrite

1. Module entry and hook registration boundaries
2. Kirby player state ownership
3. Ability implementation and activation flow
4. Trigger contracts
5. NPC and boss dependencies on the player core

## Ignore For The First Pass

1. Boss logic
2. Analytics or predictor systems
3. Secondary NPC variants
4. Procedural map generation scripts
5. Nonessential cross-mod integration

## First Milestone

1. Standalone module compiles in a separate workspace
2. Minimal session/save data exists
3. One safe player hook is added and removed cleanly
4. One tiny test path proves Kirby mode can be toggled without touching the rest of MaggyHelper

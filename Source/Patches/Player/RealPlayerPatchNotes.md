# Real Player.cs Migration Notes

## Upstream Reference

- **Repository**: `NoelFB/Celeste`
- **File**: `Source/Player/Player.cs`
- **Reference commit**: `1b0ce45c75e05649ae91b44a8bb6b196684e4352`
- **URL**: https://github.com/NoelFB/Celeste/blob/master/Source/Player/Player.cs

## Goal

Establish a patched/forked real-player architecture inside `MaggyHelper-Code` that:

1. Uses the upstream `Player.cs` behavioral structure as the authoritative source of truth.
2. Routes both **normal player** and **Kirby player** behavior through a shared migration core.
3. Moves core player logic ownership away from the old extension-only path.
4. Keeps the existing `Source/Extensions/KirbyPlayer.cs` as a backwards-compatibility shim only.

## Non-Goals

- **Not** a blind full-copy replacement of `Celeste.Player`. The vanilla Player.cs cannot be dropped
  directly into an Everest mod without compile conflicts and architectural mismatches.
- **Not** another extension-only wrapper. The old extension-only path must not remain the
  implementation center for player behavior.

## Architecture

### New files (this migration pass)

| File | Role |
|------|------|
| `ForkedPlayerCore.cs` | Behavioral core. Mirrors upstream Player state machine. Owns On.Celeste hooks. Dispatches per-state logic for normal and Kirby paths. |
| `KirbyPlayerPatch.cs` | Kirby-specific behavior layer. Bridges ForkedPlayerCore with KirbyPlayerExtension. Inhale, hover, copy abilities, mode switching. |
| `PlayerModeRouter.cs` | Routing decisions. Determines active player mode (Normal / Kirby). Provides the handoff point for state dispatch. |

### Compatibility layer (existing, demoted)

| File | Role after migration |
|------|----------------------|
| `Source/Extensions/KirbyPlayer.cs` | Legacy compatibility shim only. Must NOT own core behavior. |
| `Source/Extensions/Kirby/KirbyPlayerExtension.cs` | Primary Kirby runtime entity. Now a downstream target of KirbyPlayerPatch rather than the sole behavior owner. |

## Upstream State Constants (from real Player.cs)

Preserved in `PlayerCharacterStates` and also in `ForkedPlayerCore`:

```
StNormal        = 0   // Normal movement
StClimb         = 1   // Wall climbing
StDash          = 2   // Dash / dash attack
StSwim          = 3   // Swimming
StBoost         = 4   // Bubble boost
StRedDash       = 5   // Red boost dash
StHitSquash     = 6   // Hit squash
StLaunch        = 7   // Launch (e.g. bumpers)
StPickup        = 8   // Picking up holdables
StDreamDash     = 9   // Dream block dash
StSummitLaunch  = 10  // Summit launch sequence
StDummy         = 11  // Cutscene / dummy state
StIntroWalk     = 12  // Intro walk
StIntroJump     = 13  // Intro jump
StIntroRespawn  = 14  // Respawn intro
StIntroWakeUp   = 15  // Wake-up intro
StBirdDashTut   = 16  // Bird dash tutorial
StFrozen        = 17  // Frozen (Oshiro etc.)
StReflectionFall= 18  // Reflection fall
StStarFly       = 19  // Feather / star fly
StTempleFall    = 20  // Temple fall
StCassetteFly   = 21  // Cassette fly
StAttract       = 22  // Attract (core crystal)
```

## Migration Phases

### Phase 1 — Core movement parity (THIS PR)
- [x] Establish ForkedPlayerCore with real state constants
- [x] Hook into Player.Update and Player.Die via MonoMod
- [x] Route per-state dispatch through PlayerModeRouter
- [x] Connect KirbyPlayerPatch to KirbyPlayerExtension
- [x] Wire ForkedPlayerCore into PlayerExtensionCore lifecycle

### Phase 2 — Kirby core (next pass)
- [ ] Port inhale/float/hover state logic into KirbyPlayerPatch
- [ ] Implement Kirby-specific normal/dash/climb overrides
- [ ] Wire copy-ability hooks into state transitions
- [ ] Kirby/normal mode switching with proper side effects

### Phase 3 — Dependent systems (later)
- [ ] Copy abilities full migration
- [ ] Boss interaction hooks
- [ ] NPC/player sync
- [ ] Special triggers and tutorial systems

## Known Risks

- The vanilla `Player.cs` has ~4000 lines and many internal fields/methods. Full state mirroring
  requires careful incremental porting to avoid breaking the vanilla-player path.
- MonoMod hook stacking must not interfere with Everest's own hooks on `Player`.
- State routing must stay correct across room transitions, cutscenes, and respawns.

## Remaining Work After This PR

- Port `NormalUpdate` physics (gravity, run, jump, wall-jump, climb check) into `ForkedPlayerCore`
- Port `DashUpdate`/`DashCoroutine` into dash state dispatch
- Add Kirby-specific dash/float behavior in `KirbyPlayerPatch`
- Migrate ability hooks fully away from extension-only path
- Gradually reduce the role of `Source/Extensions/KirbyPlayer.cs`

# Player.cs Patch Architecture

**Repository:** `Magedeline/MaggyHelper-Code`  
**Upstream reference:** <https://github.com/NoelFB/Celeste/blob/master/Source/Player/Player.cs>  
**Last updated:** 2026-03

---

## Overview

This document describes the **real-Player.cs patched/forked architecture** for the
MaggyHelper mod. All modifications to Celeste's player behavior — for the normal
Madeline player and for the Kirby character system — are routed through this layer
rather than as post-Update extension side-effects.

The goal is to align mod behavior with the upstream Celeste `Player.cs` structure:
the same state machine, the same physics method boundaries, and the same transition
patterns that vanilla Celeste uses internally.

---

## Why This Approach

Celeste's `Player.cs` uses a `StateMachine` with 23 built-in states (IDs 0–22).
Each state has `update`, `begin`, `end`, and optional `coroutine` callbacks.
Player physics (gravity, horizontal movement, jumps) happen inside the state
update methods, and `Player.Update()` calls `MoveH/MoveV` after the state update.

### Previous approach (extension-only)
The old `KirbyPlayerExtension` actor ran its ability updates *after*
`On.Celeste.Player.Update` returned. This means:
- Hover physics were applied **after** `MoveH/MoveV` had already run — fighting vanilla physics.
- Inhale movement suppression was a post-facto clamp, not a proper state.
- `StKirbySlide = 100` was a placeholder constant for a state that never existed in the StateMachine.

### New approach (real-Player.cs patch)
The `PlayerPatchCore` layer integrates directly with the real Player state machine:
- Kirby states are registered via `StateMachine.AddState()` — same method vanilla uses.
- State updates run *inside* Player's update loop, before `MoveH/MoveV`.
- Physics modifications happen at the correct method boundary.
- `On.Celeste.Player.NormalUpdate` redirects to custom states exactly as vanilla redirects to `StDash`, `StClimb`, etc.

---

## File Structure

```
Source/Player/
  PlayerPatchCore.cs          — Central coordinator; hooks Player.Added
  KirbyPlayerStatePatch.cs    — Registers Kirby states in Player.StateMachine
  KirbyPhysicsPatch.cs        — Physics hooks aligned with Player.cs method boundaries
  NormalPlayerPatch.cs        — Normal player (Madeline) patch scaffold
```

Supporting files (modified for integration):
```
Source/Extensions/Core/PlayerCharacterStates.cs  — Runtime state IDs (was const 100, now dynamic)
Source/Extensions/Kirby/KirbyHoverAbility.cs     — Triggers StKirbyHover state transition
Source/Extensions/Kirby/KirbyInhaleAbility.cs    — Triggers StKirbyInhale state transition
Source/Extensions/Core/PlayerExtensionCore.cs    — Initializes PlayerPatchCore
```

---

## State Machine Integration

### Upstream vanilla states (Player.cs)
```
StateMachine = new StateMachine(23);   // 23 states, IDs 0-22
StNormal       = 0    StClimb        = 1    StDash         = 2
StSwim         = 3    StBoost        = 4    StRedDash      = 5
StHitSquash    = 6    StLaunch       = 7    StPickup       = 8
StDreamDash    = 9    StSummitLaunch = 10   StDummy        = 11
StIntroWalk    = 12   StIntroJump    = 13   StIntroRespawn = 14
StIntroWakeUp  = 15   StBirdDashTutorial=16 StFrozen       = 17
StReflectionFall=18   StStarFly      = 19   StTempleFall   = 20
StCassetteFly  = 21   StAttract      = 22
```

### Mod-registered custom states (set at runtime)
```
StKirbyHover  = 23   (1st AddState call, registered in Player.Added)
StKirbyInhale = 24   (2nd AddState call)
StKirbySlide  = 25   (3rd AddState call — was placeholder const 100)
```

State IDs are stored in `PlayerCharacterStates` after the first `Player.Added` event
fires. They are reset to -1 on module unload.

---

## How State Transitions Work

### Hover state (`StKirbyHover`)

```
Frame N-1:
  KirbyHoverAbility.OnUpdate() runs (after Player.Update, via extension)
  → detects hover button held, Stamina > 0, airborne
  → calls BeginHover()
    → sets IsHovering = true
    → sets Player.StateMachine.State = StKirbyHover  ← real state machine transition

Frame N:
  Player.Update() → StateMachine.Update() → KirbyHoverUpdate(player)
    → [UPSTREAM-REF mirrors NormalUpdate horizontal movement]
    → applies hover gravity (HoverGravity toward HoverFallSpeed)
    → returns StKirbyHover to stay / StNormal to exit

  Player.Update() → MoveH(Speed.X * dt), MoveY(Speed.Y * dt)  ← vanilla movement

  KirbyHoverAbility.OnUpdate() runs
    → checks IsHovering flag
    → manages stamina drain, flap input
    → sets IsHovering = false if hover ended
    → EndHover() → Player.StateMachine.State = StNormal
```

Physics double-application guard: when `Player.StateMachine.State == StKirbyHover`,
the slow-fall section in `KirbyHoverAbility.OnUpdate` is skipped. The state patch
already handles gravity.

### Inhale state (`StKirbyInhale`)

```
KirbyInhaleAbility.StartInhale()
  → sets IsInhaling = true
  → Player.StateMachine.State = StKirbyInhale

KirbyInhaleUpdate(player)  [called by StateMachine.Update inside Player.Update]
  → walk-only horizontal movement (50% MaxRun) — [UPSTREAM-REF NormalUpdate]
  → vanilla gravity (Player.cs Gravity = 900f toward MaxFall = 160f)
  → exits to StNormal when IsInhaling = false

KirbyInhaleAbility.StopInhale()
  → sets IsInhaling = false
  → Player.StateMachine.State = StNormal
```

### Slide state (`StKirbySlide`)

```
Triggered externally (e.g., KirbySlide trigger entity or combat system)
  → Player.StateMachine.State = StKirbySlide

KirbySlideBegin(player)
  → records slide direction (player.Facing) and resets timer

KirbySlideUpdate(player)  [called by StateMachine.Update inside Player.Update]
  → Speed.X = slideDir * SlideSpeed * SlideSpeedMultiplier  [UPSTREAM-REF DashUpdate pattern]
  → gravity still applies to Speed.Y (unlike vanilla dash)
  → exits to StNormal after SlideDuration (0.25s)

Collision: KirbyPlayerExtensionCore.PlayerOnCollideH
  → only fires when StateMachine.State == StKirbySlide (now real, not const 100)
  → bridge point for enemy contact damage
```

---

## Physics Constants Reference

All constants below are mirrored from upstream `Player.cs` and used by
`KirbyPlayerStatePatch` and `KirbyPhysicsPatch`.

| Constant | Upstream Value | Location in Player.cs |
|---|---|---|
| `MaxRun` | 90f | `public const` |
| `RunAccel` | 1000f | `public const` |
| `AirMult` | 0.65f | `private const` |
| `Gravity` | 900f | `private const` |
| `HalfGravThreshold` | 40f | `private const` |
| `MaxFall` | 160f | `public const` |
| `FastMaxFall` | 240f | `private const` |
| `JumpSpeed` | -105f | `private const` |
| `JumpHBoost` | 40f | `private const` |
| `DashSpeed` | 240f | `private const` |
| `ClimbMaxStamina` | 110f | `public const` |

Kirby-specific overrides (from `KirbySettings`):

| Setting | Default | Purpose |
|---|---|---|
| `HoverFallSpeed` | 60f | Max fall speed during hover (vs MaxFall 160f) |
| `HoverGravity` | 240f | Gravity toward HoverFallSpeed (vs Gravity 900f) |
| `HoverFlapSpeed` | -140f | Upward speed per flap (≈ JumpSpeed -105f) |
| `SlideSpeedMultiplier` | 1.0f | Multiplies SlideSpeed (200f base) |

---

## Data Flow

```
MaggyHelperModule.Load()
  └─ PlayerExtensionCore.Hook()
       ├─ PlayerPatchCore.Initialize()
       │    ├─ On.Celeste.Player.Added += OnPlayerAdded
       │    ├─ KirbyPhysicsPatch.Hook()   (On.Celeste.Player.NormalUpdate)
       │    └─ NormalPlayerPatch.Hook()   (scaffold — future hooks here)
       └─ (other subsystems...)

In-game: Player entity spawned
  └─ Player.Added(scene)
       └─ PlayerPatchCore.OnPlayerAdded(orig, self, scene)
            └─ KirbyPlayerStatePatch.RegisterStates(player)
                 ├─ player.StateMachine.AddState(KirbyHoverUpdate, ...)  → ID 23
                 ├─ player.StateMachine.AddState(KirbyInhaleUpdate, ...) → ID 24
                 ├─ player.StateMachine.AddState(KirbySlideUpdate, ...)  → ID 25
                 └─ PlayerCharacterStates.SetKirbyStateIds(23, 24, 25)

In-game: Each frame with Kirby mode active
  Player.Update()
    ├─ StateMachine.Update()  (dispatches to KirbyHoverUpdate / KirbyInhaleUpdate / ...)
    └─ MoveH(Speed.X * dt), MoveY(Speed.Y * dt)

  KirbyPlayerExtension.Update()  (via On.Celeste.Player.Update post-orig)
    └─ KirbyAbilityManager.Update()
         ├─ KirbyHoverAbility.OnUpdate()   — manages stamina/flaps, triggers state transitions
         ├─ KirbyInhaleAbility.OnUpdate()  — manages pull cone, triggers state transitions
         └─ (other abilities...)
```

---

## Compatibility Notes

### Existing extension system
`KirbyPlayerExtension` and `KirbyAbilityManager` remain as-is. They continue to:
- manage ability logic, animation, health, power states
- run after `Player.Update` via `On.Celeste.Player.Update`
- integrate with `ModCompat` bridges (CommunalHelper, BossesHelper, etc.)

The new patch layer does not replace them — it adds the state machine integration
that was previously missing.

### `StKirbySlide = 100` replacement
All code that previously referenced the `const int StKirbySlide = 100` now
references `PlayerCharacterStates.StKirbySlide` (a static property). The value
changes from the unreachable placeholder 100 to the real registered ID 25.
**The `KirbyPlayerExtensionCore.PlayerOnCollideH` check now actually fires**
when the player is in slide state.

### Session/save compatibility
No session or save data format changes. `PlayerCharacterStates` state IDs are
runtime-only and not persisted.

---

## Next Migration Steps

### Near-term
- [ ] **Hover: full NormalUpdate parity** — The current hover state handles basic
  horizontal movement and gravity. Consider porting additional NormalUpdate sections
  (wall slide detection, variable jump support, jump grace period) for full parity.
  See `KirbyPlayerStatePatch.KirbyHoverUpdate`.
- [ ] **Slide: trigger integration** — Add `Player.StateMachine.State = StKirbySlide`
  call to the slide-trigger entity (or combat system) so the state actually activates.
- [ ] **StKirbyKnight / StKirbyStarFly** — Register additional Kirby states following
  the same pattern as StKirbyHover.

### Medium-term
- [ ] **IL physics hooks** — Replace the `On.Celeste.Player.NormalUpdate` state
  redirect with `IL.Celeste.Player.NormalUpdate` gravity injection to modify physics
  constants directly in vanilla code. See `KirbyPhysicsPatch` §IL hook stubs.
- [ ] **NormalPlayerPatch** — Migrate `MadelineCombatSystem` hooks into `NormalPlayerPatch`
  for centralized normal-player patch management.

### Long-term
- [ ] **Full hover state parity** — Port wall-slide, LiftBoost, and variable jump
  sections from `NormalUpdate` into `KirbyHoverUpdate` for a fully self-contained hover.
- [ ] **Kirby climb** — Disable/modify vanilla StClimb in Kirby mode (Kirby doesn't
  wall-climb in source games). Override via `On.Celeste.Player.ClimbBegin`.
- [ ] **Kirby swim** — Custom StSwim variant via a registered custom state.
- [ ] **Power-specific states** — Each copy power (Fire, Ice, Sword, etc.) may warrant
  its own registered state for ability execution.

---

## Testing Checklist

After changes to the patch layer, verify:
- [ ] Game loads without exceptions
- [ ] Kirby hover activates (hold Hover button airborne) and enters StKirbyHover
- [ ] Hover gravity is slower than normal fall (HoverFallSpeed 60f vs MaxFall 160f)
- [ ] Flap jumps work (StKirbyHover stays active, Speed.Y = HoverFlapSpeed)
- [ ] Hover exits cleanly on button release or landing (StNormal)
- [ ] Inhale activates and enters StKirbyInhale (walk-only movement)
- [ ] Inhale exits and returns to StNormal
- [ ] Slide collision handler fires (OnCollideH with StKirbySlide = 25)
- [ ] Normal player (Madeline mode) unaffected when Kirby mode off
- [ ] Level transitions preserve state correctly
- [ ] Module unload/reload does not leave stale state IDs

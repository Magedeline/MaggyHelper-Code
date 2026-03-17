# MaggyHelper — Desolo Zantas: Complete Mod Description for Map Builders

> **Version:** 3.0.0  
> **Target:** Celeste via Everest ≥ 1.5848  
> **Author:** Maggy  

---

## 1. What Is This Mod?

**Desolo Zantas** is a standalone story campaign built inside Celeste. It has **21 main chapters** (numbered 0–20), most with **four difficulty sides** (A/B/C/D), plus a **DX-Side** system for remixed post-game content. On top of traditional Celeste platforming, it adds:

- A **playable Kirby character** with float, inhale, and 20 copy abilities.
- A **boss battle system** (full-phase bosses, mid-bosses, and mini-bosses).
- A **sub-map / lobby system** for chapters 10–14 that works similarly to a collab lobby.
- **Crossover characters** from Undertale, EarthBound, and Kirby franchises alongside original characters.
- **Restart-gated progression** for late-game story chapters (16, 19, 20).

---

## 2. Campaign Structure Overview

### 2.1 Main Chapters (A-Sides)

All main chapters live under `Maps/Maggy/Main/`. Each `.bin` file has a matching `.maggyhelper.meta.yaml`.

| Ch | Code | Official Name | Theme | Has B/C/D? | Notes |
|----|------|---------------|-------|------------|-------|
| 0 | `00_Prologue` | Prologue | Tutorial | No | Interlude-style, no hearts |
| 1 | `01_City` | Forbidden Metropolis | Urban | Yes | |
| 2 | `02_Nightmare` | Veil of Shadows | Dark/horror | Yes | |
| 3 | `03_Stars` | Arrival | Celestial | Yes | |
| 4 | `04_Legend` | Chronicles of Destiny | Myth/legend | Yes | |
| 5 | `05_Restore` | Fractured Memories | Memory/nostalgia | Yes | |
| 6 | `06_Stronghold` | Fortress of Solitude | Fortress | Yes | |
| 7 | `07_Hell` | Infernal Reflections | Lava/mirrors | Yes | Mountain state 1 (dark) |
| 8 | `08_Truth` | Revelation's Edge | Revelation | Yes | |
| 9 | `09_Summit` | Apex of Reality | Summit ascent | Yes | |
| 10 | `10_Ruins` | Echoes of the Past | Ancient ruins | Yes | **Has lobby + submaps** |
| 11 | `11_Snow` | Frozen Sanctuary | Ice/snow | Yes | **Has lobby + submaps** |
| 12 | `12_Water` | Cascading Depths | Water/underwater | Yes | **Has lobby + submaps** |
| 13 | `13_Fire` | Blazing Territories | Fire/volcano | Yes | **Has lobby + submaps** |
| 14 | `14_Digital` | Cyber Nexus | Digital/cyber | Yes | **Has lobby + submaps** |
| 15 | `15_Castle` | Ethereal Citadel | Castle/ethereal | Yes | Completing → queues Ch16 |
| 16 | `16_Corruption` | Organ Garden of Despair | Corruption/body horror | No | Unlocks on game restart |
| 17 | `17_Epilogue` | Epilogue | Closure | No | Interlude-style |
| 18 | `18_Heart` | Core of Existence | Heart/core | Yes | Outro → queues Ch19 |
| 19 | `19_Space` | Farewell to Stars | Space/farewell | No | Unlocks on restart; completing → queues Ch20 |
| 20 | `20_TheEnd` | The Last Push (The End) | Final/void | No | Unlocks on restart |

### 2.2 Side System (B/C/D/DX)

Chapters 1–15 and 18 each have four sides. Files are named with suffixes:

- **A-Side** (`_A.bin`) — Normal difficulty, story content. Mode index 0.
- **B-Side** (`_B.bin`) — Harder remix. Mode index 1.
- **C-Side** (`_C.bin`) — Short but brutal. Mode index 2.
- **D-Side** (`_D.bin`) — Modded extra difficulty. Mode index 3.
- **DX-Side** — Post-game remixed content. Mode index 4.

Heart gem colors per side: **Blue** (A), **Red** (B), **Gold** (C), **Rainbow** (D), **Void** (DX).

### 2.3 Late-Game Story Gates (Restart Unlocks)

Three chapters use a **close-and-restart** gating mechanic for narrative effect:

1. Complete **Ch15 (Castle)** → game sets a pending flag → quit → relaunch → **Ch16 (Corruption) unlocks**.
2. Watch **Ch18 (Heart) outro** → game sets a pending flag → quit → relaunch → **Ch19 (Space) unlocks**.
3. Complete **Ch19 (Space)** → game sets a pending flag → quit → relaunch → **Ch20 (TheEnd) unlocks**.

This is handled by `ChapterProgressionManager.cs`. Pending flags are stored in save data and processed during `Overworld.Begin`.

---

## 3. The Lobby & Sub-Map System (Chapters 10–14)

**This is the collab-like structure you're asking about.** Chapters 10 through 14 each have a **lobby map** and multiple **small maps (submaps)** that the player selects from the lobby. This is a custom system (not Celeste Collab Utils) built entirely within MaggyHelper.

### 3.1 How It Works

```
Chapter 10 (Echoes of the Past)
├── 10_Ruins_A.bin          ← Main A-Side (appears in chapter select)
├── 10_Ruins_B.bin          ← B-Side
├── 10_Ruins_C.bin          ← C-Side
├── 10_Ruins_D.bin          ← D-Side
│
├── 10_Ruins_Lobby.bin      ← Lobby hub map (does NOT appear in chapter select)
│   ├── Contains SubMapLobby entity (interactive portal selector)
│   ├── Contains SubMapManager entity (singleton controller)
│   └── Has sidMapEnterTrigger instances pointing to each submap
│
├── 10_Ruins_SM1.bin        ← Small Map 1 ("Fragment I")
├── 10_Ruins_SM2.bin        ← Small Map 2 ("Fragment II")
├── 10_Ruins_SM3.bin        ← Small Map 3 ("Fragment III")
├── 10_Ruins_SM4.bin        ← Small Map 4 ("Fragment IV")
├── 10_Ruins_SM5.bin        ← Small Map 5 ("Fragment V")
├── 10_Ruins_SM6.bin        ← Small Map 6 ("Fragment VI")
├── 10_Ruins_EX.bin         ← Extra/Hidden Relic map (unlocks after completing all 6 SMs)
└── 10_Ruins_Boss.bin       ← Boss arena (unlocks after completing at least 1 SM)
```

Chapters 11–14 follow the same pattern (Snowdin, Water, Fire, Digital lobbies).

### 3.2 Map Categories & Meta YAML

Every map file has a `.maggyhelper.meta.yaml` using the `maggyhelper-map-meta-v1` schema. Key fields:

#### Main Chapter Entry (e.g. `01_City_A`)
```yaml
schema: maggyhelper-map-meta-v1
sid: Maggy/Main/01_City_A
baseKey: 01_City
side: A
modeIndex: 0
isMainChapterEntry: true
mainChapterSid: Maggy/Main/01_City_A
showInChapterSelect: true
```

#### Lobby (e.g. `10_Ruins_Lobby`)
```yaml
schema: maggyhelper-map-meta-v1
sid: Maggy/Lobby/10_Ruins_Lobby
baseKey: 10_Ruins_Lobby
side: Lobby
modeIndex: 99              # Special: lobby maps use mode 99
isMainChapterEntry: false
mainChapterSid: Maggy/Main/10_Ruins_A
showInChapterSelect: false  # Hidden from chapter select
lobbyFor: 10                # Which chapter number this serves
startRoom: lvl_lobby_hub
submapSids:                 # All child submaps
  sm1: Maggy/SmallMaps/10_Ruins_SM1
  sm2: Maggy/SmallMaps/10_Ruins_SM2
  sm3: Maggy/SmallMaps/10_Ruins_SM3
  sm4: Maggy/SmallMaps/10_Ruins_SM4
  sm5: Maggy/SmallMaps/10_Ruins_SM5
  sm6: Maggy/SmallMaps/10_Ruins_SM6
  ex:  Maggy/SmallMaps/10_Ruins_EX
  boss: Maggy/SmallMaps/10_Ruins_Boss
```

#### Small Map (e.g. `10_Ruins_SM1`)
```yaml
schema: maggyhelper-map-meta-v1
sid: Maggy/SmallMaps/10_Ruins_SM1
baseKey: 10_Ruins_SM1
side: SM1
modeIndex: 1
isMainChapterEntry: false
mainChapterSid: Maggy/Main/10_Ruins_A
showInChapterSelect: false
lobbySid: Maggy/Lobby/10_Ruins_Lobby
chapterNumber: 10
smallMapNumber: 1
mapType: small
startRoom: lvl_sm1_start
```

#### Boss Map (e.g. `10_Ruins_Boss`)
```yaml
schema: maggyhelper-map-meta-v1
sid: Maggy/SmallMaps/10_Ruins_Boss
baseKey: 10_Ruins_Boss
side: Boss
modeIndex: 8
isMainChapterEntry: false
mainChapterSid: Maggy/Main/10_Ruins_A
showInChapterSelect: false
lobbySid: Maggy/Lobby/10_Ruins_Lobby
chapterNumber: 10
mapType: boss
startRoom: lvl_boss_arena
unlockRequirement: complete_any_sm
```

### 3.3 Lobby Entities (What to Place in Lönn)

| Entity / Trigger | Where | Purpose |
|-----------------|-------|---------|
| **SubMapManager** | Lobby map (one per lobby) | Singleton controller. Tracks which submaps are unlocked/completed. Handles transitions. |
| **SubMapLobby** | Lobby map | Interactive portal selector with visual portals for each submap. Player walks into it and picks a destination. |
| **sidMapEnterTrigger** | Lobby map | Trigger region that, when entered, teleports the player into a specific submap SID. Has an `unlockCondition` property. |
| **sidMapReturnTrigger** | Inside each submap | Trigger at the end of a submap that sends the player back to the lobby. |
| **chapterLobbyEnterTrigger** | Main chapter A-Side | Trigger in the main chapter that sends the player into the lobby hub. Requires chapter completion check. |
| **WarpStar** | Inside submaps | Kirby-themed collectible star for navigation within submaps. Variants: one-use, shielded, respawning. |
| **SubMapCompletionTrigger** | Inside each submap | Placed at the submap's exit. Records completion, checks gem requirements, can trigger cutscenes. |

### 3.4 Submap Unlock Flow

1. Player enters a chapter's A-Side and reaches the `chapterLobbyEnterTrigger`.
2. Teleports to the lobby (`10_Ruins_Lobby.bin`).
3. In the lobby, the `SubMapManager` auto-unlocks SM1. Subsequent submaps unlock when the previous one is completed (SM1 done → SM2 unlocks, etc.).
4. Player selects a submap via `SubMapLobby` portals or `sidMapEnterTrigger`.
5. Player completes the submap, hitting `SubMapCompletionTrigger` → records progress.
6. Player returns to lobby via `sidMapReturnTrigger`.
7. **EX map** unlocks after all 6 regular submaps are complete.
8. **Boss map** unlocks after at least 1 submap is complete.

### 3.5 Why This Exists (Not Collab Utils)

This is a **custom system** that mimics collab-style lobby selection but is tightly integrated with MaggyHelper's:
- Chapter-specific progression tracking (save data per submap)
- Theming (each lobby has unique portal colors matching the chapter)
- Boss gating (boss arena tied to submap completion)
- Kirby ability integration (submaps can grant/restrict specific abilities)

It does **not** use Celeste Collab Utils. All lobby/submap logic is in `SubMapManager.cs`, `SubMapLobby.cs`, `SubMapTriggers.cs`, and `SubMapCompletionTrigger.cs`.

---

## 4. Playable Character System

### 4.1 Two Player Modes

| Mode | Sprite ID | Description |
|------|-----------|-------------|
| **Madeline (Default)** | `maggy_player` | Standard Celeste gameplay. All vanilla moves. |
| **Kirby** | `kirby_player` | Unique moveset: float, inhale, copy abilities. Toggle via `EnableKirbyPlayer` setting. |

### 4.2 Kirby Abilities

Kirby has **20 copy abilities** acquired through AbilityStar collectibles placed in maps:

> Fire, Ice, Spark, Sword, Cutter, Beam, Stone, Needle, Parasol, Wheel, Bomb, Fighter, Suplex, Ninja, Mirror, Hammer, Wing, UFO, Sleep, (none)

**Kirby colors** (palette swaps): Pink, Yellow, Blue, Red, Green, White, Orange, Purple.

### 4.3 Character Roster (NPCs / Story)

Characters from four franchises appear in cutscenes and as NPCs:

- **Celeste:** Madeline, Badeline, Theo, Oshiro, Granny
- **Kirby:** Kirby, Meta Knight, King Dedede, Waddle Dee, Magolor
- **Undertale:** Frisk, Chara, Asriel, Toriel, Ralsei
- **EarthBound:** Ness ("Chara" in some contexts is dual-referenced)

### 4.4 Skin System

Via SkinModHelper, the mod registers:
- **KirbySkin** — Kirby with backpack
- **KirbySkin_NB** — Kirby without backpack

---

## 5. Boss System

### 5.1 Boss Tiers

| Tier | Behavior | Example |
|------|----------|---------|
| **FullBoss** | Multi-phase, unique arena, health bar, dialogue, music | Asriel Angel, ELS Flowey, Axis Terminator |
| **MidBoss** | 3 attack patterns, locked room | King DDD, Whispy Woods, Oshiro Boss |
| **MiniBoss** | Patrol guards, simple AI | Scarfy, charge enemies |

### 5.2 Named Bosses in the Mod

- `asriel_angel_boss`, `asriel_god_boss` (v1 & v2)
- `axis_terminator_boss`
- `chara_boss`
- `dededeBoss`
- `dx_asriel_transcendence_boss`, `dx_darkmatter_boss`, `dx_flowey_omega_boss`
- `els_flowey_boss`, `els_true_final_boss`
- `inferno_eye`, `inferno_mirror`
- `kirbyBoss`
- `oshiro_boss`
- `tenna_tv_boss`
- `whispy_woods_boss`

### 5.3 Boss Entities for Map Building

Place these in Lönn:
- **Boss entity** (e.g. `asriel_angel_boss`) — the boss actor itself.
- **BossArenaTrigger** — locks the room boundaries during the fight.
- **bossFightTrigger** / **bossIntroTrigger** — starts the encounter.
- **DamageTrigger** / **HealTrigger** — damage/heal zones.
- **HealthSystemTrigger** — activates the health bar HUD.
- **invincibilityTrigger** / **oneHitTrigger** — special conditions.
- **enemySpawnTrigger** / **enemyWaveTrigger** — for spawning adds.

Boss difficulty scales with the `BossDifficultyMultiplier` setting (1×–5×).

---

## 6. Progression & Save Data

### 6.1 What Gets Saved

- **Completed chapters** (by SID)
- **Unlocked chapters** (including restart-gated ones)
- **Heart gems** and **cassettes** collected (per chapter, per side)
- **Boss defeats** (individual boss names tracked)
- **Submap progress** (per lobby, per submap)
- **Kirby unlockables** (colors, abilities)
- **Statistics** (float time, objects inhaled, ability usage counts)
- **Achievements**

### 6.2 Dash Inventory Progression

Players gain more dashes as the story progresses:

| Tier | Dashes | When |
|------|--------|------|
| Standard | 1 | Start |
| TwoDash | 2 | After Ch1 |
| Solar | 3 | Mid-game unlock |
| Lunar | 4 | Late-game unlock |
| BlackHole | 5 | Post-Ch15 |
| SaveStar | 10 | Kirby mode endgame |

---

## 7. Visual Effects Available

### 7.1 Custom Backdrops (Loenn Effects)

| Effect | Usage |
|--------|-------|
| `ancient_runes` | Floating rune particles (ruins chapters) |
| `asriel_god_backdrop` | Ch20 finale sky |
| `colorgrade_effect` | LUT-based color grading |
| `distortion_effect` | Water/displacement |
| `els_true_final_backdrop` | ELS true final boss arena |
| `giygas_backdrop` | EarthBound-inspired horror |
| `glitch_effect` | Corruption/digital glitch |
| `heaven_gates_backdrop` | Ethereal light gates |
| `magical_aura` | Magic particles |
| `popstar_bg` | Kirby Pop Star sky |
| `rainbow_blackhole_bg` | Void/blackhole spiral |
| `tesseractholebg` | Dimensional rift |

### 7.2 Shader Effects

Defined in `Loenn/metadata/shader_effects.yaml`:
- `distortion` — chromatic aberration (strength, speed, offset)
- `glitch` — RGB offset + pixelation (intensity, pixelSize, rgbOffset)
- `colorgrade` — LUT reference (lutName, blend)
- `gaussianblur` — configurable blur (radius, sigma, taps: 9/5/3)
- `dither` — 4×4 Bayer dithering (threshold, scale)
- `dust` — edge-detected noise (density, speed, edgeThreshold)
- `border` — pixel edge outline (color, thickness, edgeThreshold)

---

## 8. Custom Triggers Reference (For Map Builders)

### Movement & Ability
| Trigger | Purpose |
|---------|---------|
| `abilitySwapTrigger` | Swap Kirby's current copy ability |
| `kirbyAbilityTrigger` | Grant or remove a specific copy ability |
| `kirby_mode_toggle_trigger` | Toggle Kirby mode on/off |
| `disableAbilityTrigger` | Temporarily disable abilities |
| `dashRefreshTrigger` | Refresh dash count |

### Boss & Combat
| Trigger | Purpose |
|---------|---------|
| `bossIntroTrigger` | Play boss intro cutscene, start music |
| `bossFightTrigger` | Actually start the boss fight |
| `BossArenaTrigger` | Lock arena boundaries |
| `DamageTrigger` | Deal damage to player |
| `HealTrigger` | Heal player |
| `HealthSystemTrigger` | Show/hide health bar |
| `invincibilityTrigger` | Grant invincibility frames |
| `oneHitTrigger` | Enable one-hit-kill mode |
| `enemySpawnTrigger` | Spawn enemies at a position |
| `enemyWaveTrigger` | Spawn a wave of enemies |

### Level Flow
| Trigger | Purpose |
|---------|---------|
| `checkpointTrigger` | Set a respawn checkpoint |
| `teleportTrigger` | Teleport player to a room/position |
| `sidMapEnterTrigger` | Enter a submap from a lobby |
| `sidMapReturnTrigger` | Return from submap to lobby |
| `chapterLobbyEnterTrigger` | Enter a chapter lobby from A-Side |
| `countdownEscapeTrigger` | Start a timed escape sequence |

### Character & Interaction
| Trigger | Purpose |
|---------|---------|
| `characterSwapTrigger` | Swap player character mid-level |
| `KirbyPlayerTrigger` | Force Kirby mode for a section |
| `dualCharacterTrigger` | Enable two-character mode |
| `CompanionSummonTrigger` | Summon an NPC companion |
| `InteractTrigger` | Generic interaction zone |

### Visual & Audio  
| Trigger | Purpose |
|---------|---------|
| `colorShiftTrigger` | Shift color palette |
| `cameraShakeTrigger` | Camera shake effect |
| `musicLayerTrigger` | Enable/disable music layers |
| `screenFlashTrigger` | Flash the screen |
| `pixelationTrigger` | Pixelate the screen |
| `vignetteTrigger` | Show vignette overlay |
| `weatherChangeTrigger` | Change weather effect |
| `zoomTrigger` | Camera zoom |
| `parallaxShiftTrigger` | Shift parallax layers |

### Special / Story
| Trigger | Purpose |
|---------|---------|
| `els_glitch_rainbow_blackhole_trigger` | ELS boss visual |
| `giygas_glitch_background` | Giygas EarthBound effect |
| `rainbow_blackhole_trigger` | Void visual effect |
| `CS09_EndingTrigger` | Ch9 ending sequence |
| `CreditsTriggerPart1` / `Part2` | Roll credits |

---

## 9. Map File Organization Rules

### Folder Structure
```
Maps/Maggy/
├── Main/         ← All 21 A/B/C/D-side chapter maps
├── Lobby/        ← Lobby hub maps for chapters 10–14
├── SmallMaps/    ← Submap levels for chapters 10–14 (SM1–SM6, EX, Boss)
├── PCG/          ← Procedurally generated test maps
└── WIP/          ← Work-in-progress test maps
```

### Naming Convention
```
{ChapterNumber}_{ThemeName}_{Side}.bin
{ChapterNumber}_{ThemeName}_{Side}.maggyhelper.meta.yaml

Examples:
  01_City_A.bin                  ← Chapter 1 A-Side
  01_City_B.bin                  ← Chapter 1 B-Side
  10_Ruins_Lobby.bin             ← Chapter 10 Lobby
  10_Ruins_SM3.bin               ← Chapter 10 Small Map 3
  10_Ruins_Boss.bin              ← Chapter 10 Boss Arena
  10_Ruins_EX.bin                ← Chapter 10 Extra/Hidden map
```

### SID Convention
```
Maggy/Main/{filename_without_extension}       ← Main chapters
Maggy/Lobby/{filename_without_extension}      ← Lobbies
Maggy/SmallMaps/{filename_without_extension}  ← Submaps
```

---

## 10. Building a New Lobby Chapter (Step-by-Step)

If you're building a new chapter with the lobby system (like chapters 10–14), here's the full procedure:

### Step 1: Create the Main Chapter Maps
Create `XX_Theme_A.bin` through `XX_Theme_D.bin` in `Maps/Maggy/Main/`. Each needs a meta YAML with `isMainChapterEntry: true` (for the A-Side) and `showInChapterSelect: true`.

### Step 2: Create the Lobby Map
Create `XX_Theme_Lobby.bin` in `Maps/Maggy/Lobby/`:
1. Design a hub room (`lvl_lobby_hub`).
2. Place one **SubMapManager** entity (singleton, only one per lobby).
3. Place one **SubMapLobby** entity (the interactive portal selector).
4. Place **sidMapEnterTrigger** instances for each destination (SM1–SM6, EX, Boss).
5. Create the meta YAML with `modeIndex: 99`, `lobbyFor: XX`, and `submapSids` listing all child maps.

### Step 3: Create the Submaps
Create `XX_Theme_SM1.bin` through `XX_Theme_SM6.bin` in `Maps/Maggy/SmallMaps/`:
1. Each starts with a `startRoom` (e.g. `lvl_sm1_start`).
2. Place a **sidMapReturnTrigger** at the exit to send the player back to lobby.
3. Place a **SubMapCompletionTrigger** at the end to record completion.
4. Create meta YAML with `mapType: small`, `lobbySid`, and `chapterNumber`.

### Step 4: Create the EX Map (Optional)
Create `XX_Theme_EX.bin` — unlocks after all 6 SMs are complete. Same structure as a submap but harder/secret content.

### Step 5: Create the Boss Map
Create `XX_Theme_Boss.bin` — unlocks after at least 1 SM is complete:
1. Place the boss entity.
2. Place **BossArenaTrigger** to lock the room.
3. Place **bossFightTrigger** to start the encounter.
4. Place **HealthSystemTrigger** for the HP bar.
5. Create meta YAML with `mapType: boss` and `unlockRequirement: complete_any_sm`.

### Step 6: Connect from the Main Chapter
In the A-Side map, place a **chapterLobbyEnterTrigger** at the appropriate location to send the player from the main chapter into the lobby.

---

## 11. Dependencies Summary

The mod requires **35+ helper mods**. Key ones for map building:

| Dependency | Used For |
|-----------|----------|
| **LuaCutscenes** | All dialogue, story scenes, cutscene scripting |
| **CommunalHelper** | Connected entities, dream blocks, synced zip movers |
| **FrostHelper** | Custom spinners, lightning, arbitrary shapes |
| **ExtendedVariantMode** | Variant triggers (gravity, speed, etc.) |
| **MaxHelpingHand** | Multi-room strawberries, flag triggers, parallax fade |
| **CherryHelper** | Falling blocks, order-based switches |
| **JungleHelper** | Rope mechanics, jungle-themed entities |
| **SkinModHelper** + **Plus** | Kirby skin registration |
| **MaggyHelperAudio** | Custom soundtrack and SFX |
| **VidPlayer** | In-game video playback |
| **DJMapHelper** | Temple eye blocks, colored triggers |
| **BounceHelper** | Bounce pads, bounce blocks |
| **EeveeHelper** | Flag-controlled entities, holo entities |
| **AdventureHelper** | Overworld map integration |

---

## 12. Key Technical Notes

1. **No AltSideHelper dependency:** The mod implements its own 5-mode system (`AreaModeExtender.cs`). D-Side and DX-Side are natively supported without AltSideHelper.

2. **Mountain overworld:** Custom 3D mountain model (`DesoloZantas_Mountain.blend`). Each chapter has camera positioning data. Mountain state changes at Ch7 and Ch13 (state 1 = darker theme).

3. **Room naming:** Use `lvl_` prefix for all rooms. Lobby hubs should be `lvl_lobby_hub`. Submap starts should be `lvl_sm{N}_start`. Boss arenas should be `lvl_boss_arena`.

4. **Debug commands:**
   - `maggy_chapter_test status` — show unlock state
   - `maggy_chapter_test queue16/queue19/queue20` — queue a pending unlock
   - `maggy_chapter_test unlock16/unlock19/unlock20` — force unlock
   - `maggy_chapter_test apply` — process pending unlocks

5. **Dialog keys:** All mod text uses keys prefixed with chapter context. Lobby text follows `{THEME}_LOBBY_*` patterns. Memorial text uses `MAGGY_MEMORIAL_*`.

6. **Cutscenes:** Story cutscenes are scripted in Lua via LuaCutscenes. Boss intros and submap completion events are C# cutscene entities.

---

## 13. Quick Reference: What Goes Where

| I want to... | File/Folder | Entity/Trigger |
|--------------|-------------|----------------|
| Add a main chapter | `Maps/Maggy/Main/` + meta YAML + register in `AreaMapData.cs` | — |
| Add a lobby | `Maps/Maggy/Lobby/` + meta YAML | SubMapManager, SubMapLobby, sidMapEnterTrigger |
| Add a submap | `Maps/Maggy/SmallMaps/` + meta YAML | sidMapReturnTrigger, SubMapCompletionTrigger |
| Add a boss fight | Place boss entity in a room | Boss entity + BossArenaTrigger + bossFightTrigger + HealthSystemTrigger |
| Add Kirby abilities | Place in any map | abilityStar, kirbyAbilityTrigger |
| Add NPCs | Place in any map | dialog_npc, enhanced_dialog_npc, specific NPC entities |
| Add enemies | Place in any map | customEnemy, waddleDee, etc. + enemySpawnTrigger |
| Add story cutscene | Write Lua script + place trigger | LuaCutscenes trigger |
| Change visual mood | Room styleground settings | Effects from Section 7 + visual triggers from Section 8 |
| Gate progression | Configure in code | ChapterProgressionManager (restart gates) or SubMapManager (submap gates) |

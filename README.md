# MaggyHelper

A comprehensive Celeste mod and map pack featuring the **Desolo Zantas** campaign - an ambitious multi-chapter adventure with custom gameplay mechanics, characters, and story.

## Overview

MaggyHelper is an Everest mod for Celeste that provides:

- **Desolo Zantas Map Pack** - A full campaign with A-Side, B-Side, C-Side, and D-Side chapters
- **Custom Player Skins** - Including Kirby with unique abilities (floating, inhale)
- **Boss Battles** - Custom boss encounters including themed battles
- **Custom NPCs & Cutscenes** - Rich storytelling with character animations and dialogue
- **Visual Effects** - Custom backdrops, stylegrounds, and particle effects
- **Audio** - Original music and sound effects

## Chapters

### A-Sides
| Chapter | Name |
|---------|------|
| Prologue | Awakening Dreams |
| 1 | Shattered Metropolis |
| 2 | Veil of Shadows |
| 3 | Celestial Awakening |
| 4 | Chronicles of Destiny |
| 5 | Fractured Memories |
| 6 | Fortress of Solitude |
| 7 | Infernal Reflections |
| 8 | Revelation's Edge |
| 9 | Apex of Reality |
| 10 | Echoes of the Past |
| 11 | Frozen Sanctuary |
| 12 | Cascading Depths |
| 13 | Blazing Territories |
| 14 | Cyber Nexus |
| 15 | Ethereal Citadel |
| 16 | Organ Garden of Despair |
| Epilogue | Final Resonance |
| 18 | Core of Existence |
| 19 | Farewell to Stars |
| 20 | The Last Push |

Plus B-Sides, C-Sides, and D-Sides with additional challenges!

## Features

### Kirby Player Mode
- **Float Ability** - Hold jump to float through the air
- **Inhale Ability** - Unique interaction mechanics
- Custom sprite animations

### Custom Entities
- Boss encounters with unique AI patterns
- Custom projectiles and hazards
- Interactive NPCs with dialogue systems
- Special collectibles (Pink Platinum Berries, Popstarberries)

### Visual & Audio
- Custom color grading effects
- Animated background stylegrounds
- Original soundtrack and sound effects
- Character portraits for cutscenes

## Installation

1. Install [Everest](https://everestapi.github.io/) mod loader for Celeste
2. Download MaggyHelper from the mod browser or place in your `Mods` folder
3. Dependencies will be automatically downloaded

### Dependencies
This mod requires many helper mods including:
- AdventureHelper, CommunalHelper, CherryHelper
- ExtendedVariantMode, FemtoHelper
- And many more (see `everest.yaml` for full list)

## Development

### Building
```bash
msbuild /property:GenerateFullPaths=true /t:build
```

### Split Repos
If you want to keep runtime code and map authoring in separate repositories, use `Tools/Split-MaggyHelperRepos.ps1` and follow `Docs/REPO_SPLIT_GUIDE.md`.

### Project Structure
```
MaggyHelper/
├── Audio/           # Custom sound banks
├── Code/            # Compiled DLL output
├── Dialog/          # Localization files
├── Entities/        # Entity definitions
├── Graphics/        # Sprites, tilesets, effects
├── Loenn/           # Level editor plugins
├── Maps/            # Map files
├── Source/          # C# source code
└── Triggers/        # Custom triggers
```

## Team Tasks

### Current Sprint

- [ ] **Map Design** - Complete remaining D-Side chapters
- [ ] **Boss Polish** - Finalize boss AI patterns and hitboxes
- [ ] **Playtesting** - Full campaign playthrough for bug hunting
- [ ] **Audio** - Implement missing sound effects for new entities
- [ ] **Localization** - Complete dialogue translations
- [ ] **Art Assets** - Finalize character portrait sprites
- [ ] **Documentation** - Update entity docs for Loenn plugins

### Backlog

- [ ] Implement additional Kirby copy abilities
- [ ] Add optional hard mode modifiers
- [ ] Create achievement system
- [ ] Design bonus content/secrets
- [ ] Optimize performance for complex rooms
- [ ] Beta tester feedback integration

### Completed

- [x] Core mod framework and hooks
- [x] Kirby player skin implementation
- [x] A-Side chapter layouts
- [x] Custom cutscene system
- [x] Loenn entity plugins
- [x] Base audio integration

## Community

- Wiki starter draft: [Docs/WIKI_STARTER.md](Docs/WIKI_STARTER.md)
- Wiki home draft: [Docs/Wiki/Home.md](Docs/Wiki/Home.md)
- Wiki getting started draft: [Docs/Wiki/Getting-Started.md](Docs/Wiki/Getting-Started.md)
- Wiki mapping guide draft: [Docs/Wiki/Mapping-Guide.md](Docs/Wiki/Mapping-Guide.md)
- Discussions category plan: [Docs/DISCUSSIONS_PLAN.md](Docs/DISCUSSIONS_PLAN.md)
- Pinned start-here discussion draft: [Docs/DISCUSSION_START_HERE.md](Docs/DISCUSSION_START_HERE.md)

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is proprietary. All rights reserved by Maggy Studio.

## Credits

**Maggy Studio Team**

Built with love for the Celeste modding community.

### Environment Asset Credits (Ch. 10-15 Draft)

> Source: Celeste Community Asset Drive. Verify each asset "More info" page for exact credit/notify terms before release.

#### Chapter 10 — Echoes of the Past
- `stone_brick_crumbled.png` — mintberry, LegS
- `Misc Vanilla Edits/yellowBrickRuinedBG.png` — Starl1ght
- `SJ GM + lava layer/brick_rubble_b.png` — tobyaaa
- `catalyst + ruined edits/circuit_broken_c.png` — ABuffZucchini (give credit)

#### Chapter 11 — Frozen Sanctuary
- `iceSnow.png` — Anilmky Abicus (Notify - Credit)
- `frozenGrassBG.png` — Anilmky Abicus (Notify - Credit)
- `Extra Forsaken City sprites/frozenSignNoDown.png` — xolimono (give credit)
- `Superfecta/Downturned Uptown/city02.png` — ricky06 (give credit)

#### Chapter 12 — Cascading Depths
- `thirdLakeTemple.png` — third (give credit)
- `baba is you (advanced tileset tech)/FallWater.png` — tobyaaa (give credit)
- `Individual/Waterfall/WaterfallLoop02.png` — ThySirSlime (give credit)
- `Garden of Khu_tara/HLD_02.png` — DanTKO (give credit)

#### Chapter 13 — Blazing Territories
- `magmaRock.png` — Anilmky Abicus (Notify - Credit)
- `Misc Vanilla Edits/lavaTempleBG.png` — Starl1ght
- `Skyline Usurper/GearC02.png` — j0nas (give credit)
- `Skyline Usurper/city_skyline.png` — Earthwise (give credit)

#### Chapter 14 — Cyber Nexus
- `neon_temple.png` — kyfex
- `ABuffZucchini_bgDigital.png` — ABuffZucchini (give credit)
- `Celeste Edits/recolors/rainbow_big_crystal_a.png` — snolls (give credit)
- `CrossoverCollab/Terraria/RainbowOverlayHorizontal.png` — KoseiDiamond (give credit)

#### Chapter 15 — Ethereal Citadel
- `advCloudPurple.png` — nerferd_, Juno
- `castleBG.png` — tobyaaa (give credit)
- `Anarchy Collab/darkcloudC.png` — KoseiDiamond
- `Loopy Lagoon/rainbowMadness.png` — DanTKO (give credit)

---

*Powered by [Everest](https://everestapi.github.io/) - The Celeste Mod Loader*

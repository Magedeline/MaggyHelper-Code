# Logo Validation Checklist
**Target:** `Graphics/Atlases/Gui/logo/logo.png` (2000×2000)  
**Reference:** `Graphics/Atlases/Gui/logo/logo_large.png` (480×480)  
**Style:** Desolo Zantas mountain, Celeste atlas

---

## 1. Canvas & Export

- [ ] Canvas size is exactly **2000×2000 px**
- [ ] Exported as **PNG with transparency** (no white background)
- [ ] No ICC colour profile embedded (uncheck "Embed ICC Profile" on export)
- [ ] No JPEG compression artefacts or blurring introduced at any stage

---

## 2. Palette Accuracy

Open both files side-by-side at 100% zoom and verify all key areas use only sampled colours:

| Area | Expected Colour |
|---|---|
| Sky top | `#9CD8FE` |
| Sky mid-to-deep | `#5E68DA` |
| Sky twilight haze | `#6767C1` |
| Mountain back (base) | `#675C83` |
| Mountain shadow planes | `#473E58` |
| Rock neutral shadow | `#635D6E` |
| Snow highlight | `#FEFEFF` |
| Snow mid-tone | `#C8C8C8` |
| Mountain rim light | `#FEDD86` |
| Warm shadow gold | `#B99842` |
| Glow accent (magenta) | `#CC6DD8` |
| Glow accent (pink) | `#D496D0` |
| Red accent (glyph) | `#FE1100` |
| Primary outline | `#000006` |
| Deep corner outline | `#000019` |

- [ ] No stray colours outside this set (use Edit → Find Edge Colour or eyedrop spot-check)
- [ ] Transparency areas are fully transparent (alpha = 0, not near-white pixels)

---

## 3. Composition Match

Compare shapes between the two files:

- [ ] Mountain silhouette angle and relative proportions match large logo
- [ ] Sky gradient direction is top-light → bottom-deep (not reversed)
- [ ] Logo/glyph sits in **same relative position** within the frame
- [ ] Mountain mid-ground is slightly warmer/lighter than mountain back
- [ ] Peak rim light (`#FEDD86`) is visible on at least one mountain edge
- [ ] Glow behind logo does not bleed into mountain edges

---

## 4. Line Quality

Zoom to 400% and inspect edges:

- [ ] Outlines are hard-edged (no anti-alias blur thicker than 1 px)
- [ ] No fringe pixels along transparent edges
- [ ] Internal shape fills have clean, closed borders (no gaps or leaks)
- [ ] Glow layer is soft but contained; no glow spills into transparent area

---

## 5. Value (Light/Dark) Structure

Each solid shape should use exactly **3 values**:

- [ ] Dark (shadow plane)
- [ ] Mid (base/fill)
- [ ] Light (highlight ridge or rim)

No shape should have more than one highlight and one shadow band.

---

## 6. Zoom Readability

- [ ] At **25% zoom** (≈500px display): logo shape and mountain silhouette are readable
- [ ] At **100% zoom**: crisp and clean, no muddy mid-tones
- [ ] At **400% zoom**: no stray pixels, no colour bleed, outlines are 1-2 px wide

---

## 7. Small Variant (`logo_small.png` — 72×72)

If updating small logo at the same time:

- [ ] Downscaled from final 2000×2000 using **nearest-neighbour** (no bicubic/bilinear)
- [ ] Text/glyph strokes are still at least **2 px wide** at 72 px
- [ ] Mountain silhouette is simplified to 2–3 angular shapes (not full detail)
- [ ] Re-outline strokes manually if anything disappeared in downscale

---

## 8. Final Commit Check

- [ ] Original `logo.png` backed up before overwriting (or tracked by Git)
- [ ] Run the mod in-game (or via Everest reload) and check the logo renders in the title/chapter select screen
- [ ] Confirm no white box appears around the logo (transparency leak)
- [ ] Compare screenshot of new logo against reference screenshot of `logo_large.png` at similar viewport size

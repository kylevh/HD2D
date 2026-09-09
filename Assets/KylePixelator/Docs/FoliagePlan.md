# Foliage / grass — high-level plan (t3ssel8r-style)

**Status:** F1–F8 locked on PixelLab (F8 = discrete darker tufts for noon visibility). Draw path is a **baked MeshRenderer** (`GrassTuftDraw`), not GPU instancing. Foundation locked.

**Goal:** Grass and small plants that read as **hand-pixelled tufts** under our low-res snap pipeline — not AAA geo blades, not a second render stack.

---

## What peers actually do

The “t3ssel8r grass” look is a **recipe**, not one magic shader. Across t3ssel8r’s logs, David Holland’s writeup, and Unity ports ([bababuyyy / Leng isometric pixel pipeline](https://github.com/bababuyyy/unity-isometric-pixel-pipeline)):

| Ingredient | Typical approach | Why it matters at 640×360 |
|---|---|---|
| **Blade mesh** | Billboard **quads** (baked / instanced), not dense 3D blades | Cheap; silhouette stays chunky |
| **Shape** | Alpha cutout / soft-cut sprite atlas | Pixels define the tuft, not geometry |
| **Color** | Shape + lawn albedo; shade like terrain | Tufts blend into terrain instead of floating stickers |
| **Lighting** | **Even per-tuft** lighting (up-normal / root sample) | Avoids self-banding under snap |
| **Shadows receive** | Sample directional shadow at **blade root** | Matches “lit like the ground under it” |
| **Outlines** | **Exclude** grass from outline passes | Otherwise every sprite gets a 1px outline |
| **Wind** | World-space noise; tip weighted by UV.y | Organic sway without breaking texel lock too hard |
| **Cloud shadows** | Global scrolling noise (shared with ground) | Big readable dark patches |
| **Accents** | Random instance swap → flower / weed sprites | Cheap variety |
| **Darker tones** | Sparse blades × mild mul + pull toward lawn `_ShadowTint` | Readable at noon without sticker-dark mismatch |

**What we already decided not to chase for foundation** (see KylePixelator README “Locked out”): Bayer dither, Point forests, grazing-sun shadow drama. Grass uses **cel bands + VolumeLights + key/fill**, not a new light model.

---

## How this fits HD2D / KylePixelator

```text
Existing:
  CameraRig snap → low-res ToonLit world → transparent grass → OutlineFeature → present

Grass:
  GrassTuftField (scatter bake)
    → GrassTuftDraw MeshRenderer + GrassBlade shader
    → transparent + ZWrite Off (no depth → no tuft outline hair)
    → shared wind globals (optional ground shade on ToonLit)
    → shared CloudShadow globals (ToonLit opt-in + GrassBlade)
```

Outline runs **after** transparents so grass cannot wipe silhouette ink on objects in front of the field.
---

## Done on PixelLab

| Step | Result |
|---|---|
| **F1** | Single billboard tuft, alpha cutout |
| **F5** | Transparent queue + ZWrite Off → no outline hair |
| **F2** | Jittered patch via `GrassTuftField` (Size / Offset / spacing) |
| **F3** | Tip-weighted wind + optional ground wind shade |
| **F4** | Shape from alpha; lawn `_BaseColor` + palette via **runtime mat instance** (not MPB — SRP Batcher ignores blocks) |
| **F6** | Shared cloud-shadow noise on Grass.mat + tufts |
| **F7** | Atlas accents (tuft / tall / weed / flower) baked per cell |
| **F8** | ~4% darker blades + min cell gap (no clumps); mild mul toward `_ShadowTint` |

**Draw lesson:** `Graphics.RenderMeshInstanced` was dropping the tuft texture (always white). Stay on MeshRenderer bake until a proven instancing path keeps `_BaseMap`.

**Color lesson:** Most tufts still bind to lawn green (F4). F8 is a mild, sparser darker sprinkle in the same ShadowTint family — not a hard mul that fights the lawn.

---

## F6 — Cloud shadows (verify)

**Where:** `CloudShadowDriver` on PixelLab World (or any active object). `Grass.mat` has **Receive Cloud Shadow** on.

**What “good” looks like**

1. Play noon: soft dark blobs crawl across the **lawn and tuft patch together**.
2. Strength 0 → flat again; tufts still match plane color.
3. Day/night scrub still works; wind tips still sway; no outline hair.
4. Cloud direction does **not** flip with the sun.

**Fail:** clouds only on ground or only on tufts; hitching; sun-locked direction.

---

## F7 — Accents (verify)

**Where:** same field; atlas on `GrassTuft_Proto` material. Rare non-base cells (~8%).

**What “good” looks like**

1. Most blades = base tuft silhouette.
2. Occasional tall / weed / flower readable at 640×360.
3. Clouds still shared; color still lawn-bound.

---

## F8 — Darker tones (verify)

**Where:** `GrassTuftField` **Dark Chance** (~0.08) / **Dark Mul** (~0.88); dark albedo leans toward lawn `_ShadowTint`. UV1.z = 0 or 1.

**Lighting:** Real sun shadows sample at the **blade root** (even whole-tuft dim via ShadowTint/HighlightTint). **Clouds multiply final color** the same way as ToonLit lawn — not a second tint path.

**What “good” looks like**

1. Play noon: sparse darker tufts, close to lawn green; **plane stays one color**.
2. Walk a caster over the field: tufts darken with the lawn (not stuck on highlight green).
3. Clouds dim normal and dark blades the same way.
4. Accents rare (~8%).

**Fail:** highlight-green tufts under character shadows; clouds only on light blades; accents everywhere.

---

## Explicitly not next

- GPU instancing / BRG revisit
- Painted density masks, player grass interaction, trees
- Reopening OutlineFeature, VolumeLights, or cel band doctrine

---

## Risks / gotchas

| Risk | Mitigation |
|---|---|
| Instanced draw loses `_BaseMap` | Baked MeshRenderer (current) |
| Outline hair | Transparent queue + ZWrite Off (F5) |
| Outline wiped by grass | OutlineFeature after transparents |
| MPB tint ignored | Runtime material instance for ground bind |
| Cloud dir tied to sun | Fixed scroll dir on `CloudShadowDriver` |

---

## References

- [David Holland — 3D Pixel Art Rendering](https://www.davidhol.land/articles/3d-pixel-art-rendering/)
- [t3ssel8r](https://www.youtube.com/@t3ssel8r)
- [bababuyyy/unity-isometric-pixel-pipeline](https://github.com/bababuyyy/unity-isometric-pixel-pipeline)

---

## One-line summary

**Billboard alpha tufts, baked field, lit like the lawn, discrete darker sprinkle, no outlines, wind + shared cloud shadows, atlas accents.**

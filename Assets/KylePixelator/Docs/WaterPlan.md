# Water / pond — plan (pixel present)

**Status:** Circular pond shipped (radial depth + foam + WaveCells hops + float foam + skim). Planar mirrors still parked.

**Where we are**
| Done | Result |
|---|---|
| W1 depth color | **Radial** bands (round lake, not Chebyshev square) |
| W1.5 rim | ZWrite On + thin lip |
| W2 shore foam | Ortho depth + radial shore fallback |
| W3 hop waves | Packed `WaveCells.png` (Holland-style) + float foam patches |
| W4 skim / night | Stronger honey skim + `_KVH_DayAmount` |
| W5 prefab | Circular `Pond.prefab` (disc water + cylinder basin) |
| W6 Field | Circular grass hole + pond at (-12,12) |
| Playground | `PondLab` — FoamTestCrate stays lab-only |

**Peer recipe (order that actually ships the look)**
1. ~~Depth / body color~~ (radial)
2. ~~Shore foam~~
3. ~~Surface waves~~ (packed cells + float foam)
4. ~~Skim / night grade~~
5. Planar mirrors only if a map needs them



**Goal:** A small outdoor pond that reads as **hand-pixelled water** under KylePixelator — banded depth, chunky shore foam, hoppy surface lines. Real planar mirrors are a **later** phase only if a map needs them.

---

## What peers actually do

| Source | What they ship | Takeaway for us |
|---|---|---|
| [David Holland](https://www.davidhol.land/articles/3d-pixel-art-rendering/) | Depth foam + quantized wave texture + **planar** reflection (second cam, oblique clip). SSR rejected. Outline/depth ordering was a major fight in Godot. | Planar = real pipeline work. Wave *look* can be a texture, not simulation. |
| [Roystan — Toon Water](https://roystan.net/articles/toon-water.html) | Depth color gradient, shoreline foam from depth, noise/distort for surface | Best **v1 recipe** (minus perspective-only depth math). |
| [Cyanilux — Depth / Ortho](https://www.cyanilux.com/tutorials/depth/) | Ortho depth decode ≠ perspective `LinearEyeDepth` | **Must** use ortho-correct depth or foam breaks on PixelCamera. |
| [bgolus / Unity forums](https://discussions.unity.com/t/water-shader-not-working-in-orthographic-camera/773992) | Same: Shader Graph “Scene Depth” tutorials assume perspective | Copy ortho path, not Brackeys/Roystan eye-depth verbatim. |
| [Catlike — Looking Through Water](https://catlikecoding.com/unity/tutorials/flow/looking-through-water/) | Refraction via grab/opaque texture | Optional later; keep distortion **quantized** or it melts under snap. |
| ProPixelizer / t3ssel8r camera lineage | Snap + compensate for stability | Water must not reintroduce sub-texel crawl (continuous UV scroll OK only if stepped / large cells). |
| Godot pixel-water kits / DanTrz planar reflector | Separate reflection viewport at game res | Confirms planar = second low-res pass, not a mat checkbox. |
| HD-2D / Octopath peers | Readable shore + palette water; reflections sparingly | Atmosphere > accuracy. |

**Consensus:** cheap toon pond = **depth color + foam + stylized surface**. Fancy = **planar RT**. Almost nobody in this niche relies on SSR for the hero look.

---

## How this fits KylePixelator

```text
Existing:
  CameraRig snap → low-res ToonLit (+ grass transparent)
    → OutlineFeature on low-res RT → present

Water (planned):
  Pond mesh (World/Geometry)
    → KylePixelatorWater (Transparent, after opaque, with grass)
    → reads camera depth (ortho-correct) for shallow/deep + foam
    → surface: atlas / stepped lines (not continuous normal maps)
    → optional sky/tint fake reflect
    → later: planar reflection RT (second ortho cam, same res family)

Outlines:
  Water should not grow 1px hair on every ripple.
  Prefer Transparent + controlled ZWrite (likely Off, like grass) OR exclude from outline.
  Foam edges should come from the water shader, not OutlineFeature.
```

**Ownership:** shader + present knobs in `KVH.KylePixelator`. Scene placement / pond prefab wiring in `KVH.Game` or editor menus only as thin glue. No combat/UI in Pixelator.

**Palette:** Lichen Copper — water should sit in **indigo / slate / honey skim**, not neon cyan. Shallow = lighter sage-blue; deep = ink-indigo; foam = near-white / warm cream.

---

## Constraints (non-negotiable)

1. **Ortho depth math** — PixelCamera is orthographic. Do not ship perspective-only foam.
2. **No SSR as the plan** — peers discard it; we stay planar-or-fake.
3. **No foundation rewrite** — no new present stack, no skybox clear, no Point-light water caustics forest.
4. **Snap-safe motion** — surface animation hops or uses coarse UV cells; avoid melting scroll.
5. **Grass coexistence** — pond under/near tufts must not wipe outlines or fight cloud shadows oddly.
6. **One pond first** — `PondLab` playground, not a global water system.

---

## Phased plan

### W0 — Spec lock (doc only)
- [x] This plan + README backlog pointer
- Confirm PixelCamera already provides depth for outlines (reuse; don’t invent a second depth path)
- Pick palette swatches against Day profile / Grass.mat

### W1 — Dead pond (color + depth) — **PondLab slice**

**Playground scene:** `Assets/Scenes/PondLab.unity`  
**Menu:** `KVH / Scenes / Build PondLab`

**Layout**
```
── Systems ──  (shared EnsurePlayableSystems, outdoor noon)
PixelCamera / GameInput / GameFlow / GameUI / DayNightLighting
── Scene ──
Player
World
  Geometry
    FieldFloor_*     grass-colored walkable ring around the hole
    PondBasin        stepped bowl under the water (depth geo)
    Pond             water plane (KylePixelatorWater)
  Interactables / _Lab / …
Gameplay / _Dynamic
```

**Why a hole:** depth color needs opaque geo *below* the water. A full grass plane under the pond = zero depth. Center opening ~8u, field ~40u, player spawns south of the shore.

**Ship**
- [x] `KylePixelatorWater` shader + `Assets/Art/Materials/Water.mat`
- [x] Shallow / deep colors + **world-XZ radial depth** from pond center (stable under ortho; scene-depth foam deferred to W2)
- [x] Transparent + ZWrite Off + Cull Off
- [x] Stepped basin (stone) under the hole for silhouette / future foam
- [x] Builder: `PondLabSceneBuilder`

**Note:** First pass tried camera depth reconstruct (VolumeLight-style); under the low-res ortho RT it read flat. W1 ships radial bands so the lab is playable; W2 reintroduces scene-depth for shore foam with a dedicated verify.

**Verify**
1. Play PondLab noon — walk the ring; pond center reads darker indigo, shores lighter sage-blue
2. Camera pan — bands stay put (no shimmer crawl on static water)
3. Esc pause / HUD still work (shared systems)

**Fail:** black pond (depth broken under ortho); neon cyan; walking on invisible floor over water; outline hair on the plane

**Not in W1:** foam, waves, reflections, grass tufts, swimming


### W1.5 — Rim outline cleanup (do this next)

**Why first:** the rings look fine; the distracting bug is outline ink on the hole lip reading *through* the water.

**Peers**
- Grass recipe already: **no outline hair** on transparent foliage (ZWrite Off + outline after transparents still ink *opaque* depth cliffs).
- Holland / pixel-water shaders: shore edge is **foam or packed outline in the water texture**, not the global silhouette pass.
- Godot “Pixel Art Water”: R channel = shore outline, G = foam, B = depth — water owns the rim.

**Root cause:** `OutlineFeature` runs **after** transparents. Transparent water with ZWrite Off leaves the floor→basin depth cliff in the buffer, so silhouette ink composites *on top of* the pond.

**Ship**
- [x] Water **ZWrite On** — flat pond fills depth; basin/hole cliffs no longer outline through the surface
- [x] Larger shore overlap + cream **rim band** in shader (water-owned edge)
- [x] Rebuild PondLab

**Verify:** PondLab — walk the shore; no black “grass outline” ring / basin step ink floating inside the water. Depth rings + cream lip still readable.

**Fail:** killing all floor outlines project-wide; soft blur on the rim.

---

### W2 — Shore foam — **shipped on PondLab**

**Goal:** Cream/white **chunky foam** where water is shallow / meets shore & props — the thing that makes a pond look wet instead of painted.

**Shipped**
- [x] Ortho scene-depth foam (`SampleSceneDepth` + world-Y gap)
- [x] Chebyshev shore fallback (`_ShoreFoamWidth`) so the rim reads even if depth is soft
- [x] Quantized bands (`_FoamBands`) + `_FoamColor` / `_FoamDistance` / `_DebugFoam`
- [x] `FoamTestCrate` half in water — cream ring at contact
- [x] HLSL note: do not name locals `line` (reserved)

**Verify:** cream lip + crate ring; open water still shows depth bands; no full-pond frost.

---

### W3 — Surface “pixel waves” — **packed + float foam shipped**

- [x] `Art/Textures/Water/WaveCells.png` (RG dir / B phase / A radius)
- [x] World-locked hop dashes from packed sample
- [x] Sparse float foam islands (hash blobs, denser near shore)
- Procedural stripe path retired

### W4 — Fake skim / day-night — **shipped**
- [x] `_SkimColor` / `_SkimStrength` (+ boosted skim)
- [x] `_NightDeepColor` + global `_KVH_DayAmount`

### W5–W6 — Prefab + Field — **circular shipped**
- [x] Disc water mesh asset + cylinder basin steps
- [x] Radial depth/foam in shader; `_PondCenter`/`_PondRadius` outside UnityPerMaterial for MPB
- [x] Field/PondLab circular grass hole (`BuildBoxWithCircleHole`)
- [x] Tuft exclude circle

### W7 — Planar reflections
Only with a concrete “this map needs mirrors” ask. **Parked.**

---

## Suggested file layout (when implementing)

```
Assets/KylePixelator/
  Shaders/KylePixelatorWater.shader
  Docs/WaterPlan.md          (this file)
Assets/Art/Materials/Water.mat
Assets/Art/Textures/Water/   (foam noise, wave atlas — tiny)
Assets/Prefabs/World/Pond.prefab
```

Runtime C# only if needed (planar cam driver in W7). W1–W6 should be **shader + mat + prefab**.

---

## Explicitly not in this plan

- Underwater post / full screen wet lens
- Caustics from many lights
- Swimming / boat physics (gameplay later)
- River flow fields / splines
- Reflective wet stone floors (different feature)
- Reopening cel / VolumeLight doctrine for water caustics

---

## Risks / gotchas

| Risk | Mitigation |
|---|---|
| Outline hair on ripples / basin | Water ZWrite On (shipped); foam must stay stepped |
| Ortho depth foam flat | Debug red mask first; fallback Chebyshev shore foam |
| Shimmering shore | No sub-texel foam scroll; quantize bands |
| Melting refraction / waves | Quantize UV + time; Holland cell tex not continuous noise |
| Planar too early | Gate W7; fake skim in W4 |
| Night unreadability | Palette test hour 0 and 12 |

---

## Implementation order

1. ~~W1–W6 + circular spruce (waves / float foam / skim)~~
2. Art-direction stop ← **here**
3. Optional later: planar mirrors (W7)

---

## References

- [David Holland — 3D Pixel Art Rendering](https://www.davidhol.land/articles/3d-pixel-art-rendering/) (water + planar; outline/depth ordering)
- [Recreating Holland’s water waves](https://gamedev.stackexchange.com/questions/213251/recreating-a-3d-pixel-art-water-effect) (packed RG/B/A wave tex)
- [Roystan — Toon Water](https://roystan.net/articles/toon-water.html) (depth foam)
- [Cyanilux — Depth](https://www.cyanilux.com/tutorials/depth/) / [Orthographic Depth](https://cyangamedev.wordpress.com/2020/03/05/orthographic-depth/)
- [Godot Pixel Art Water](https://godotshaders.com/shader/pixel-art-water/) (R=outline G=foam B=depth)
- Internal: [`FoliagePlan.md`](FoliagePlan.md), [`README.md`](README.md)

---

## One-line summary

**Round lake with radial depth, packed hop waves, float foam, and skim is in Field; planar mirrors stay parked.**

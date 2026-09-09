# KylePixelator

**Status: foundation locked.** Presentation + cel lighting + day/night grade are the shipped baseline. Content/gameplay comes next — not more pipeline architecture.

In-game **3D → pixel art presentation layer** (not a package).

**Real draw order**

```text
GameCamera (pivot aim)
  → CameraRig (snap child cam XY)
  → low-res ortho render (ToonLit / cel during draw)
  → OutlineFeature on the low-res RT
  → LowResOutput present (dest-slide compensate + sharp/nearest upscale)
```

**Asmdef:** `KVH.KylePixelator` (+ `.Editor`)

Game aim lives in `KVH.Game` as `GameCamera`. Day/night grade is game-side (`LightingDirector` + `LightingProfile` + `SolarClock`). This folder owns the pixel render contract and toon model — not combat/cutscene/HUD. JRPG layout: [`Assets/Docs/Architecture.md`](../../Docs/Architecture.md).

**Scenes:** `CastleYard` is the primary overworld. `PixelLab` / `MovementLab` / `PondLab` are labs, not content. `RoomDemo` is the interior bootstrap template.

**Reusable scene roots (MovementLab convention):**

| GameObject | Scripts | Role |
|---|---|---|
| `PixelCamera` | `CameraRig` + `GameCamera` + `CameraOcclusionFade` | Iso pivot aim + snap/low-res present; fade cover in front of hero |
| `DayNightLighting` | `LightingDirector` + `KeyLight` / `FillLight` | Day/night grade + sun arc |
| `GameInput` | `InputReader` | Player actions |
| `GameFlow` | `GameFlow` | Exploration / Menu / Battle mode |
| `GameUI` | `UiRoot` | uGUI overlay (HUD/menus) — not on the pixel cam |
| `Hero` | `PlayerMotor` + Visual `VisualSnap` | Controllable pawn (stays fully opaque) |

Class names stay as-is (`GameCamera`, `LightingDirector`, …) — only hierarchy labels were cleaned up for reuse.

**Camera occlusion:** props between the pixel camera and the player get a Bayer see-through via ToonLit `_OcclusionFade` (driven by `CameraOcclusionFade`). No player ghost mesh.

**Scene systems:** playable scenes share fixed roots (`PixelCamera`, `GameInput`, `GameFlow`, `GameUI`, `Player`, lighting, `World` / `Gameplay` / `_Dynamic`). Menu: `KVH / Scenes / Ensure Scene Systems`.

**Palette experiment — Lichen Copper:** course mats + Player + Day/Night use an earthy set (sage stone, copper wood, oxblood clay, moss accents, saffron player, indigo ink, honey day / copper night). Shared assets — also affects other scenes using the same mats/profiles.

---

## Architecture

```text
GameCamera                 (game — follow + shake on pivot)
        ↓
CameraRig                  (KylePixelator — snap child camera XY + runtime clear)
        ↓
Pixel Camera + LowResOutput
        ↓
OutlineFeature (on low-res RT) → present blit / sharp upscale
```

**Contract**

1. Game code writes the **pivot** only (`GameCamera`).
2. Only `CameraRig` writes the **pixel camera’s local XY**.
3. Never move the pixel `Camera` transform from gameplay (breaks compensate).
4. `VisualSnap` on mesh children only — never the CharacterController root.
5. Day/night clear uses `CameraRig.SetClearColor` — **never** mutate shared `DefaultPixelSettings.skyColor`.

---

## Layout

```
Assets/KylePixelator/
  Runtime/   Camera, Present (LowResOutput, CelLightingGlobals), Snap, Settings, URP
  Editor/    setup menus
  Shaders/   ToonLit, VolumeLight (+ Source), Hidden outline + sharp upscale
  Prefabs/   PixelCamera.prefab
  Settings/  DefaultPixelSettings.asset
  Docs/
```

Game lighting: `Assets/Scripts/Runtime/Lighting/` (`LightingDirector`, `SolarClock`, `LightingProfile`, `VolumeLight`).

---

## Setup

1. `KVH → KylePixelator → Create Settings Asset` / use `DefaultPixelSettings`
2. `KVH → KylePixelator → Create Camera In Scene` (or Prefab) — pivot + `CameraRig` + `LowResOutput` (**no** follow script)
3. Add **`GameCamera`** on the pivot (follow target + optional `AddImpulse`)
4. Outline feature on URP renderer (`Add Outline Feature To Selected Renderer` / `PC_Renderer`)
5. Mats: `KVH/KylePixelatorToonLit`
6. Movers: `VisualSnap` on Visual child → assign `rig` (optional yaw snap, default 45°)
7. Lighting: `LightingDirector` + Day/Night under `Assets/Settings/Lighting/`

### New scene checklist

Reference: `Assets/Scenes/RoomDemo.unity` (`KVH → Debug → Scenes → Build RoomDemo`). Don't run rebuild menus on authored maps.

1. URP `PC_Renderer` already has OutlineFeature
2. PixelCamera / pivot + `LowResOutput` + `DefaultPixelSettings`
3. `GameCamera` on pivot → Player
4. Key + fill directionals; `LightingDirector`
5. Mats = `KylePixelatorToonLit`
6. `VisualSnap` on visual child only
7. Add to build as needed

### Builds

Add these to **Always Included Shaders** (player builds; Editor often finds them via `Shader.Find`):

- `Hidden/KVH/KylePixelatorOutline`
- `Hidden/KVH/KylePixelatorSharpUpscale`
- `KVH/KylePixelatorVolumeLight`
- `KVH/KylePixelatorVolumeLightSource`

---

## Present (M3) — compensate

When snap + compensate are on:

- RT is **larger** than display by `2 × rtMarginTexels`
- Ortho scales so **display** world-per-pixel stays fixed
- Smooth pan comes from **moving the present destination rect** (dest-slide), not UV pan / nearest UV crawl
- Present Y sign is intentional (view-up vs Y-down present matrix) — don’t “fix” it casually

`LowResOutput.integerScale`: whole-number letterbox vs stretch-full Game view.

**Present bloom:** optional URP Bloom on `PixelPresentCamera` after the upscale blit (`enablePresentBloom` on Settings). The low-res RT stays HDR-off / no post. Glow lives on the 1080p image; HUD overlay is after that so it stays sharp.

---

## Follow pawn jitter (lab, 2026-09)

**Current stack (keep):** pixel snap on, dest-slide **off**, independent camera `Round` (no king-step cell), follow height quantized to whole texels along `camera.up`, ViewGlue live `Round`. Analog camera-relative move. Character should look locked in all directions. World pan is 1px steps (noticeable going sideways).

**Remaining:** environment stepping. Dest-slide hides it but slides the hero; user asked dest-slide off again.

Play-mode outline debug: `[` / `]` on `LowResOutput` (session only). Snap A/B (`SnapAbDriver` / F8) is removed.

**Tried, do not re-explore first:**

| Thing | Why not |
|---|---|
| 8-way / king-step **camera** snap | Stored cell lags; diagonal → axis-aligned desyncs and compounds |
| Dest-slide on | Hides world texel steps; slides the follow pawn. Off again. |
| ViewGlue **pin**, dest-slide to whole screen pixels, 8-way motor + instant facing | Tried to shave the diagonal tad. Made **L/R/U/D** worse. Reverted. |
| SnapOnly / CallusClassic / SkipHero / FollowPlane / PointSlide / PinLockstep / NoOutlines / SoftGround | 2026-09 F8 A/B. Only FreeCam stopped the old smear, and it crawled the world |
| ToonLit vertex snap, extra hysteresis, F8 Off/Snap/Callus as a fresh pass | Same bucket |

**If asked to shave diagonal again:** not king-step / pin / dest-pixel / 8-way motor. Dest-slide is the world fix but it moves the hero. Next is a new design (ProPixelizer pixel expansion, or hero not in that present).




---

## Outlines (M5)

`OutlineFeature` runs only on cameras with `LowResOutput` when outlines (or a debug mode) are active. Knobs are **shader globals** (Blit.hlsl owns `UnityPerMaterial` — material floats alone stay stuck at 0).

| Knob | Role |
|---|---|
| `silhouetteStepTexels` | Depth step size (texel-world) |
| `silhouetteSide` | Prefer `NearerColor` (ink the occluder) |
| `outlineLineDarken` | Silhouette ink strength |
| `creaseLow` / `creaseHigh` / `creaseBrighten` | Form edges (top↔front) |
| `enableCreases` | Off = silhouette-only |
| `outlineDebugMode` | Inspector default |

Play Mode `[` / `]` cycles debug (Composite / ColorOnly / Silhouette / Depth) as a **session override** — does not dirty the Settings asset.

---

## Lighting (doctrine)

t3ssel8r-style stacks do **not** build the look from many realtime point/spot lights.

| Layer | What to do | Why |
|---|---|---|
| **Key** | One **directional**, toon-banded N·L | Readable form; stable under snap |
| **Fill** | Second dim directional, **shadows off** | Lift darks without falloff rings |
| **Palette** | Per-mat Shadow / Highlight tints | Artistic shade vs lit |
| **Local** | `VolumeLight` — surface (world) + screen halo | Surface bands; halo = circular screen glow |
| **Night** | Retint key/fill/sky; VolumeLight `nightBoost` | Global grade + lamps |
| **Stock Point/Spot** | Avoid for default look | Smooth atten × bands × low-res = onion rings |

**PixelLab day recipe:** warm Hard key + cool Fill + pastel mats + day profile sky.

### Day / night

`LightingDirector` + `SolarClock`:

| Field | Role |
|---|---|
| Mode | `Cycle` or `Manual` |
| Time Of Day Hours | 0–24 scrub (Edit Mode works) |
| Twilight / Sunrise / Sunset | Grade + sun share one clock |
| Sun Min Pitch (~28°) | Short contact shadows; never grazing |
| Yaw sweep | Continuous dusk→dawn (no sunset face-flip) |
| Night `keyShadowStrength` | Keep on (moon contact) — cel can’t soft-fade URP maps |

Clear color → `CameraRig.SetClearColor` only.

### VolumeLight

Prefab: [`VolumeLight.prefab`](../../Prefabs/Lighting/VolumeLight.prefab). Drop into a scene (`KVH/Lighting/Create Volume Light`, or GameObject → Light → Volume Light). Defaults are a warm lamp; place the root, don't parent a Unity Point light.

| Knob | Role |
|---|---|
| Surface radius / quantization | World-distance bands on depth geometry |
| Halo size (low-res **pixels**) | Perfect screen-space disc at the lamp |
| Corner brightness | N·L on surface only |
| Night boost | Scales with `1 − DayAmount` |
| Screen Space Shadows | Experimental; **surface only**, off by default |

Source bulb: `KylePixelatorVolumeLightSource` (no depth write — avoids SS self-holes). Mesh cover = `max(surface, haloWorld)`. Halo sizing uses `CameraRig.UnitsPerPixel`.

### Settings knobs (M6)

- `enableCelLighting`, `lightBands`, `bandSoftness`, `lightWrap`
- `additionalLightsMode` — Banded / TintOnly / Off
- `stepPunctualAttenuation` + `punctualAttenSteps` — rare stock punctuals only

ToonLit mats: `_BaseColor`, `_ShadowTint`, `_HighlightTint`, `_EmissionColor`.

---

## Runtime pieces

| Type | Role |
|---|---|
| `Settings` | Res / snap / outline / cel defaults (not mutated by day/night) |
| `CameraRig` | Ortho, euler, texel snap; runtime clear; `UnitsPerPixel` |
| `LowResOutput` | RT + present; session outline debug |
| `CelLightingGlobals` | ToonLit globals from Settings |
| `CloudShadowGlobals` / `CloudShadowDriver` | Shared scrolling cloud shade (fixed dir; ToonLit opt-in + grass) |
| `OutlineFeature` | 1px depth/normal outlines on low-res RT |
| `VisualSnap` | Child mesh snap + optional yaw quantize |
| `GameCamera` | Pivot follow + shake |

Game: `LightingProfile`, `LightingDirector`, `SolarClock`, `VolumeLight`.

---

## Locked out / preferred other methods

Things we tried, dropped, or deliberately skipped. Keep as memory — **do not reintroduce** without a new design pass.

| Topic | What we preferred instead |
|---|---|
| **Vertex / PS1 wobble snap** | Stable texel grid (t3ssel8r framebuffer), not jitter aesthetic |
| **Follow-pawn VisualSnap / dest-slide A/B (2026-09)** | Old smear: only FreeCam. Current: snap, dest-slide **off**, height quantize, ViewGlue Round. Character locked all directions; world 1px-steps. Dest-slide / pin / dest-pixel / 8-way motor / king-step camera made the hero worse. See [Follow pawn jitter](#follow-pawn-jitter-lab-2026-09) |
| **8-way king-step camera texel cell** | Independent `Round` + dest-slide. King-step desyncs on direction change |
| **Bayer dither** | Flat cel bands + mat palettes |
| **Snapping CharacterController / physics root** | `VisualSnap` on Visual child only |
| **OnGUI / HUD in present path** | uGUI overlay on `GameUI` / `UiRoot` — never inside `LowResOutput` or `PixelPresentCamera` |
| **Outline knobs on UnityPerMaterial** | Shader **globals** (Blit conflict) |
| **Mutating `DefaultPixelSettings.skyColor` from director** | `CameraRig.SetClearColor` runtime override |
| **Lab `CameraFollow` on PixelCamera** | Game-owned `GameCamera` only |
| **`LocalLight` + Point stepped disks** | `VolumeLight` surface + screen halo |
| **Soft-fading key `shadowStrength` through twilight** | Prefer a hard cut: Night profile `keyShadowStrength = 0` → shadows `None` (Day stays Hard @ 1) |
| **Sun pitch below horizon / long dusk shadows** | `sunMinPitch` floor (~28°) |
| **Snap yaw to noon at night** | Continuous dusk→dawn yaw (avoids face-lit blip at hour 18) |
| **Separate Moon Light GameObject** | Retint same key as moon + VolumeLights |
| **Cloud shadows / grass / water / film grain** | Grass + shared cloud shadows: [`FoliagePlan.md`](FoliagePlan.md). Water plan: [`WaterPlan.md`](WaterPlan.md) — cheap ortho pond first (depth/foam/hop waves/fake skim); planar mirrors = W7 later. Film grain still backlog. |
| **Dual cosine vs rise/set clocks** | One `SolarClock` for grade + arc |
| **Full LowResOutput → many classes split** | Extracted `CelLightingGlobals` only; further split deferred |
| **VolumeLight outline-boost / SS shadows as default** | Off / stubbed; experimental SS stays optional |

### Do not reintroduce (short list)

Bayer dither, root render-snap, OnGUI HUD, snapping the physics root, outline knobs in `UnityPerMaterial`, Point-forest lighting, grazing-sun shadow blips.

---

## References

- [David Holland — 3D Pixel Art Rendering](https://www.davidhol.land/articles/3d-pixel-art-rendering/) — outlines, snap+compensate camera, grass, **water + planar reflections**, volumetrics
- [t3ssel8r — Dynamic Particle Lighting](https://www.youtube.com/watch?v=0xJqzUHJ2fI)
- [aarthificial / Legacy — smooth pixel camera (2D origin of snap+offset)](https://www.youtube.com/watch?v=jguyR4yJb1M)
- [ProPixelizer — Eliminating Pixel Creep](https://propixelizer.github.io/docs/usage/eliminate-pixel-creep/)
- [Unity Volume Lights System](https://github.com/Unity-Technologies/Volume_Lights_System)
- [bababuyyy/unity-isometric-pixel-pipeline](https://github.com/bababuyyy/unity-isometric-pixel-pipeline) (reference peer; we did not adopt cloud-shadow manager / dual sun-moon)

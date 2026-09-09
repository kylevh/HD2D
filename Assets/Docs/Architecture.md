# Architecture (KVH / HD2D)

**Genre:** JRPG. Combat will be **turn-based or strategy** (not real-time action in the overworld). Overworld is exploration + interact; battle is a **mode**, not a branch inside `PlayerMotor`.

Cursor rule (always on): `.cursor/rules/architecture.mdc`. Presentation contract: [`KylePixelator/Docs/README.md`](../KylePixelator/Docs/README.md).

## Assemblies

| Asmdef | Owns | Must not |
|---|---|---|
| `KVH.KylePixelator` | Low-res present, snap, toon, outlines, highlight silhouette | Gameplay, HUD, inventory, combat |
| `KVH.Game` | Player, input, lighting grade, interaction, flow, UI, and future JRPG features | Writing pixel-cam local XY; putting HUD on the present blit |
| `KVH.Game.Editor` / `KVH.KylePixelator.Editor` | Scene builders, setup menus | Runtime logic |

**Deps:** Game → Pixelator → URP. Never the reverse. Do **not** split Game into more asmdefs until a circular reference actually appears.

## Feature folders

`Assets/Scripts/Runtime/<Feature>/` maps to `KVH.Game.<Feature>`. Add a folder when the feature has (or is about to have) real scripts — not empty manager classes.

| Folder | Status | First real type |
|---|---|---|
| `Player/` | now | `PlayerMotor`, `PlayerBusy`, `HeroAnimDriver`, `PlayerVitals` |
| `Camera/` | now | `GameCamera` (follow + 8-way yaw), `CameraOcclusionFade` |
| `Input/` | now | `InputReader` (Move / Attack / Interact / CameraLeft / CameraRight / Menu) |
| `Interaction/` | now | `Interactable` + `InteractionScanner` + `InspectText` |
| `Lighting/` | now | `LightingDirector`, profiles, `VolumeLight` |
| `Flow/` | now | `GameMode` + `GameFlow` (Exploration / Menu / Battle) |
| `UI/` | now | `UiRoot`, `PixelUiTheme` / `PixelUiBuild`, `ExplorationHud`, `PauseMenu`, `DialogueBox`, `ItemToast` |
| `Dialogue/` | now | `DialogueRunner` + `DialogueSpeaker` (lines + optional portrait) |
| `Inventory/` | now | `ItemDef`, `PlayerInventory`, `ItemPickup` (bag + toast; no menu yet) |
| `Combat/` | when first encounter | Turn-based / strategy loop. Enter via `GameMode.Battle` |
| `Party/` | when a second character exists | Character defs + runtime HP |
| `Flags/` | when a door needs a key | `HashSet` of string ids is enough |

Data assets (`*Def`, `*Profile`) live under `Assets/Settings/` or `Assets/ScriptableObjects/` when we have any — not beside every MonoBehaviour “for later.”

## Scene hierarchy

Every playable scene uses the same **flat system roots** (do not nest `PixelCamera` / `Player` under a Managers parent). Separators are empty identity objects for scanning only.

```
── Systems ──
PixelCamera
GameInput
GameFlow
GameUI
DayNightLighting       (or RoomLighting in interiors)

── Scene ──
Player                 (instance name always Player; prefab may be Hero)
World
  Geometry
  Interactables
  _Lab                 (optional disposable lab clutter)
Gameplay
  Npcs
  Spawns
_Dynamic               (runtime spawns; leave empty in edit)
```

| Root | Owns | Edit freely? |
|---|---|---|
| `── Systems ──` | Visual label only | No |
| `PixelCamera` | `CameraRig`, `GameCamera`, `CameraOcclusionFade` | Pipeline only |
| `GameInput` | `InputReader` | Input asset wiring |
| `GameFlow` | mode + `DialogueRunner` | Mode / dialogue |
| `GameUI` | `UiRoot` + HUD / pause / dialogue; children `Hud` / `Menus` | UI layout |
| `DayNightLighting` | `LightingDirector` | Lighting |
| `── Scene ──` | Visual label | — |
| `Player` | motor, scanner, vitals, bag | Prefab instance |
| `World` | authored level geo + interact props | Yes (content) |
| `Gameplay` | NPCs, spawn points, triggers | Yes (content) |
| `_Dynamic` | runtime instantiations | Code parents here |

**Playable scenes**

| Scene | Role |
|---|---|
| `CastleYard` | **Primary overworld** — brick yard explore. Author content here. |
| `RoomDemo` | Interior bootstrap template |
| `SceneSandbox` | Bare systems + small floor (`Create Empty Playable Scene`) |
| `Field` | Outdoor grass + pond (do not regenerate from PixelLab) |
| `MovementLab` | Checker movement sandbox (lab, not content) |
| `PondLab` | Water playground |
| `PixelLab` | Raw shader / foliage lab |

**Rules:** separators / folders stay at identity transform. Systems keep fixed names across scenes. Content lives under `World` / `Gameplay`, never as loose roots next to systems.

**New scene workflow:**
1. `KVH / Scenes / Create Empty Playable Scene` → pick a path (default `SceneSandbox`).
2. Or open any empty scene and run `KVH / Scenes / Ensure Scene Systems`.
3. Authored content goes under `World` / `Gameplay`.

Rebuild menus (`Build Field`, `Build CastleYard`, pond prefab, …) live under **`KVH / Debug / …`**. Do not use them on authored maps.

**System prefabs** (instantiate, do not rebuild by hand):

| Prefab | Path |
|---|---|
| PixelCamera (+ `GameCamera` / occlusion) | `Prefabs/Systems/PixelCamera.prefab` |
| DayNightLighting | `Prefabs/Systems/DayNightLighting.prefab` |
| GameInput | `Prefabs/Systems/Input.prefab` |
| GameFlow | `Prefabs/Systems/GameFlow.prefab` |
| GameUI | `Prefabs/UI/GameUI.prefab` |
| Player | `Prefabs/Characters/Hero.prefab` |

Interiors use a `RoomLighting` root (manual night) instead of `DayNightLighting`. `EnsurePlayableSystems` is the shared hierarchy path.

**Later (not now):** additive bootstrap scene + content scenes when the first real door / battle transition exists. Until then, per-scene prefab copies of Flow / UI / Input.

## Naming

- Job, not pattern: `InteractionScanner`, `LightingProfile`, `UiRoot`. Avoid `XxxManager` / `XxxSystem` unless it really is one.
- PascalCase, no spaces. Scene objects keep the hierarchy labels above.
- Menus: `KVH / Scenes / …` (Ensure / Create Empty), `KVH / Debug / …` (rebuild labs), `KVH / Lighting / …`, `KVH / KylePixelator / …`.

## Overlap (who owns what)

| Fact | Owner | Others |
|---|---|---|
| Can the pawn walk? | `PlayerMotor.Busy` | Dialogue / UI / cutscene / battle **set** busy; they do not `Move()` |
| Game mode | `GameFlow.Mode` | Battle folder runs only in `Battle`; menus in `Menu` |
| What is in range? | `InteractionScanner.Current` | Talk / pickup / inspect read this |
| Pixel look | KylePixelator | Game calls `HighlightOutline`, `CameraRig.SetClearColor`, `GameCamera` on the **pivot** |
| Items | `PlayerInventory` | UI toast / later menus draw; chests write via `ItemPickup` |

## UI (uGUI)

Best fit for this project: **Unity uGUI**. One overlay canvas:

1. `GameUI` + `UiRoot` — `Screen Space Overlay`, high `sortingOrder`, **not** parented under `PixelCamera`. Hidden from Scene view (hierarchy eye) so the overlay doesn't cover the world; Game view still shows it. Unhide to edit HUD layout.
2. HUD (HP, names, item toast) under `Hud`; dialogue / pause under `Menus`.
3. **UI kit** — code-built chrome via `PixelUiTheme` + `PixelUiArt` + `PixelUiBuild`. **Dark indie/pixel placeholder**: opaque deep-slate panels (`#1A171F`), white / near-white mono type, soft lilac rim, pastel red/blue bars. Layouts locked; swap art later via theme. Default: `Assets/Settings/UI/DefaultPixelUiTheme.asset` (mirrored under `Resources/UI/`).
4. **Locked live layouts** (everywhere for now):
   - Exploration — **ClassicParty** (`ExplorationHud`): top-left party plate. Hidden while `GameMode.Menu`.
   - Pause — **DualPause** (`PauseMenu`): left party column, right command stubs. Esc / Start (`InputReader.MenuPressed`). Sets `PlayerBusy.Menu` + `GameMode.Menu`. Resume row is visual; Esc closes. Items / Equipment / Status / Options / Save / Quit are stubs until those screens exist.
   - Talk / inspect — **portrait dialogue** (`DialogueBox`): bottom plate + left portrait viewport. `DialogueSpeaker.portrait` optional; missing sprite → solid placeholder fill.
5. `PixelPresentCamera` has `cullingMask = 0` and only blits the low-res RT — world-space canvases on that camera will not show. Overlay is the right path.
6. **Do not** add UI Toolkit until a menu is actually painful in uGUI. **Do not** draw HUD in `LowResOutput` / OnGUI. No menu stack / DontDestroyOnLoad UI yet. Theme refresh: `KVH / UI / Ensure Pixel UI Themes`.

Interact prompts in the world stay presentation-side (`HighlightOutline`). Menu chrome stays uGUI. UI components **display only** — they do not own inventory, save, or dialogue line state.

## Combat (when we start it)

- Own folder. Own scene **or** mode swap — disable overworld motor via `PlayerBusy.Battle` + `GameMode.Battle`.
- Do not grow `PlayerMotor` into attacks, HP, or turn order.
- Strategy vs classic turn-based can share party/stats; do not scaffold both encounter UIs until we pick one fight.

## Pitfalls

- God pawn / god `GameManager`
- `FindObjectsByType` every frame (wire in the inspector; one-shot find in `Awake` is OK)
- Interfaces / event bus / DI before a second consumer
- Item or dialogue logic in Button `OnClick`
- Putting JRPG content into `KylePixelator/`
- Extra asmdefs “for cleanliness”
- Nesting moving pawns under organizational parents that might move

## Next gameplay (not architecture)

Thin talk / inspect / pickup + pause are in. Next **game** steps: inventory *menu*, battle mode, flags — not all in one pass.

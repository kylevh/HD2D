# HD2D / KVH

JRPG (overworld exploration; combat will be turn-based or strategy). Pixel present is `KylePixelator`; gameplay is `KVH.Game`.

- Architecture: [`Assets/Docs/Architecture.md`](Assets/Docs/Architecture.md)
- Pixel pipeline: [`Assets/KylePixelator/Docs/README.md`](Assets/KylePixelator/Docs/README.md) (follow pawn: snap, dest-slide **off**. Character locked; world 1px-steps. Do not king-step / pin / dest-slide / 8-way motor first.)
- Cursor rules: `.cursor/rules/` (`architecture`, `keep-it-simple`, `unity-workflow`, `code-comments`, `follow-pawn-snap`)

Do not put HUD in `LowResOutput`. Game UI is uGUI overlay (`UiRoot` / `GameUI`).

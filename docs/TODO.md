# TODO

Task list derived from `plan.md` §9. One checkbox = one task = one `devlog.html` entry.
Work top to bottom unless the user says otherwise.

## M0. Planning

- [x] Write `plan.md` (spec) and `plan.html` (Korean visual summary)
- [x] Create `TODO.md`, `devlog.html`, and project `CLAUDE.md`

## M1. Project setup

- [x] Create Unity 6 project (URP blank template) at `Unity/`
- [x] Install Input System package; set Active Input Handling to "Input System Package (New)"
- [x] Add `.gitignore`
- [x] Initialize git repository — **user does this**
- [x] Move project documents into `docs/` and update references
- [x] Remove template leftovers (`TutorialInfo/`, `Readme.asset`); rename `SampleScene` → `Main`
- [x] Commit the base project state
- [x] Add `.gitattributes`: LF line endings, Unity YAML merge, Git LFS for binary assets
- [x] Add `.editorconfig`: formatting and .NET naming rules
- [x] Create folder structure under `Unity/Assets/` per `plan.md` §2
- [x] Set up `Main` scene: directional light and orthographic quarter-view camera (§7)
- [x] Create `PlayerControls.inputactions` with `Move` (WASD), `Jump` (Space), `Restart` (R)
  (template's `InputSystem_Actions.inputactions` can be replaced)

## M2. Flat map + capsule movement

- [x] `MapLayout` ScriptableObject (4×4 tile specs: type + height level)
- [x] Tile prefabs: Solid, Crumbling, Start (1×1 voxel-style cubes)
- [x] `MapBuilder`: instantiate tiles from `MapLayout` at scene load
- [x] Player prefab: capsule + `CharacterController`
- [x] `PlayerController`: camera-relative WASD movement and facing
- [x] `PlayerController`: jump with gravity, coyote time, jump buffer (§4.5 tuning targets)
- [x] Playtest in the editor: movement direction, turn, jump feel — **user**
- [x] Tune camera framing: the flat map fills only ~45% of the screen (revisit after M4 heights)

## M3. Falling + restart

- [x] `GameState` enum and `GameManager` with `StateChanged` event
- [x] Kill plane: `y < -5` → `GameOver`
- [x] Restart: R key reloads the scene
- [x] GameOver panel with Restart button
- [x] Disable player input when state is not `Playing`
- [x] Playtest in the editor: fall → Game Over panel, R and button restart — **user**

## M4. Heights + crumbling tiles

- [x] Height levels 0–2 (0.5 units per level) in `MapBuilder`
- [x] Holes (`.`) in layout
- [x] `CrumblingTile`: 0.8 s stand → shake → drop → respawn after 3 s
- [x] Author the initial layout from `plan.md` §4.2
- [x] Widen tiles to 1.5 × 1.5; derive camera bounds from the grid size
- [x] Playtest: jump tuning on heights and holes, crumbling timing — **user**

## M5. Items + clear

- [x] Item prefab: rotating/bobbing voxel gem with trigger collider
- [x] `ItemSpawner`: random placement on valid Solid tiles, ground/air height 50/50
- [x] `GameConfig` ScriptableObject (`itemCount`, default 4)
- [x] `GameManager`: `ItemCollected` event, all collected → `Cleared`
- [x] HUD: `n / total` item counter
- [x] Clear panel with elapsed time and Restart button
- [x] Playtest: pickups (ground and air), HUD count, clear panel and time, restart reshuffles — **user**

## M6. Blender character

- [x] `Tools/Blender/animals/fox.py`: voxel boxes + palette
- [x] `generate_character.py`: scene reset, per-part mesh build with interior-face culling
- [x] Materials from palette
- [x] Armature (`Root → Body → Head/Legs/Tail`) with rigid parts (meshes parented to bones)
- [x] Actions: `Idle`, `Walk`, `Jump`, `Fall`
- [x] FBX export to `Unity/Assets/Art/Character/Fox.fbx`; verify headless run exits 0

## M7. Character integration

- [x] Import `Fox.fbx` as Generic rig; rename clips `Fox|Idle` → `Idle` etc., loop Idle/Walk/Fall;
  verify facing +Z, feet at y = 0
- [x] Animator Controller: Idle/Walk/Jump/Fall states and parameters (§6)
- [x] `PlayerAnimator`: drive Animator parameters from `PlayerController`
- [x] Replace capsule visual with fox in Player prefab
- [x] Scale Walk playback with movement speed (playtest: walk looked too slow)
- [x] Walk: body roll and twist synced to the legs, head and tail countering
- [x] `Sprint` input action (Left/Right Shift); walk speed 1, run speed 3 while held
- [x] `Run` clip (bound gait) in the Blender generator
- [x] Animator: Run state, `Running` bool, per-clip playback speed (Walk ref 1, Run ref 3)
- [x] Run by default; Left Ctrl toggles walk mode (replaces hold-Shift sprint)
- [x] Playtest: animation states and transitions, facing, fox size vs. collider — **user**

## M8. Polish

- [ ] Item pickup effect
- [ ] Crumbling tile shake visuals
- [ ] Style UI panels
- [ ] Sounds (optional)

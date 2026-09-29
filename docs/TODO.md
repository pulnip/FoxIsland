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
- [ ] Create `PlayerControls.inputactions` with `Move` (WASD), `Jump` (Space), `Restart` (R)
  (template's `InputSystem_Actions.inputactions` can be replaced)

## M2. Flat map + capsule movement

- [ ] `MapLayout` ScriptableObject (4×4 tile specs: type + height level)
- [ ] Tile prefabs: Solid, Crumbling, Start (1×1 voxel-style cubes)
- [ ] `MapBuilder`: instantiate tiles from `MapLayout` at scene load
- [ ] Player prefab: capsule + `CharacterController`
- [ ] `PlayerController`: camera-relative WASD movement and facing
- [ ] `PlayerController`: jump with gravity, coyote time, jump buffer (§4.5 tuning targets)

## M3. Falling + restart

- [ ] `GameState` enum and `GameManager` with `StateChanged` event
- [ ] Kill plane: `y < -5` → `GameOver`
- [ ] Restart: R key reloads the scene
- [ ] GameOver panel with Restart button
- [ ] Disable player input when state is not `Playing`

## M4. Heights + crumbling tiles

- [ ] Height levels 0–2 (0.5 units per level) in `MapBuilder`
- [ ] Holes (`.`) in layout
- [ ] `CrumblingTile`: 0.8 s stand → shake → drop → respawn after 3 s
- [ ] Author the initial layout from `plan.md` §4.2 and playtest jump tuning

## M5. Items + clear

- [ ] Item prefab: rotating/bobbing voxel gem with trigger collider
- [ ] `ItemSpawner`: random placement on valid Solid tiles, ground/air height 50/50
- [ ] `GameConfig` ScriptableObject (`itemCount`, default 4)
- [ ] `GameManager`: `ItemCollected` event, all collected → `Cleared`
- [ ] HUD: `n / total` item counter
- [ ] Clear panel with elapsed time and Restart button

## M6. Blender character

- [ ] `Tools/Blender/animals/fox.py`: voxel layers + palette
- [ ] `generate_character.py`: scene reset, per-part mesh build with interior-face culling
- [ ] Materials from palette
- [ ] Armature (`Root → Body → Head/Legs/Tail`) with rigid 1.0 weights
- [ ] Actions: `Idle`, `Walk`, `Jump`, `Fall`
- [ ] FBX export to `Art/Character/Fox.fbx`; verify headless run exits 0

## M7. Character integration

- [ ] Import `Fox.fbx` as Generic rig; verify four clips, facing +Z, feet at y = 0
- [ ] Animator Controller: Idle/Walk/Jump/Fall states and parameters (§6)
- [ ] `PlayerAnimator`: drive Animator parameters from `PlayerController`
- [ ] Replace capsule visual with fox in Player prefab

## M8. Polish

- [ ] Item pickup effect
- [ ] Crumbling tile shake visuals
- [ ] Style UI panels
- [ ] Sounds (optional)

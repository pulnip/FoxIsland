# MyGame — Fox Island, a voxel animal platformer

Unity 6 3D game: a voxel animal (built with Blender Python) walks and jumps on small tile maps
(levels up to 8×8; each block has its own height 0–6 and skin: sand, meadow, forest, rock,
snow, ice, shallow water, iceberg) standing in a lake, under a fixed quarter-view camera,
collecting items.
Falling into the water = game over, all items = clear, R = restart.

## Key files

Project documents live in `docs/`.

| File               | Purpose                                                              |
| ------------------ | -------------------------------------------------------------------- |
| `docs/plan.md`     | Full spec and source of truth. Read before starting any task.        |
| `docs/TODO.md`     | Task checklist. Pick the first unchecked task unless told otherwise. |
| `docs/devlog.html` | Korean work log for the user, newest entry first.                    |
| `docs/plan.html`   | Korean visual summary of `plan.md` for the user. Git-ignored.        |

`docs/.gitignore` ignores every `.html` there except `devlog.html`.

## Workflow

After finishing each task (one `docs/TODO.md` checkbox):

1. Check the box in `docs/TODO.md`. If the task revealed new work, add it as a new unchecked
   item in the right milestone.
2. Add an entry to `docs/devlog.html` directly below the `NEW ENTRIES GO HERE` comment,
   following the template in that comment. Write the entry in Korean: what was done,
   files changed, notes (decisions, problems, what's next).
   Use neutral wording in all project documents: no chat-persona forms of address or
   nicknames for the user; the persona stays in chat only. Write "직접", or omit the subject.
3. If a decision changed the spec, update `docs/plan.md` and, when the change is user-visible,
   `docs/plan.html` too.

Project knowledge that must persist across sessions goes in this file, not in Claude's memory.

## Conventions

- C# (Unity): standard .NET conventions. The C++ rules in the global CLAUDE.md do not apply.
  `.editorconfig` owns layout and naming; follow it.
- Unity 6 compiles C# 9: no file-scoped namespaces, `record struct`, or other C# 10+ features.
- Never use `??`, `?.`, or `is null` on `UnityEngine.Object`; Unity overrides `==` for
  destroyed objects. Use `TryGetComponent` instead of `GetComponent() ?? AddComponent()`.
- Python (Blender scripts): PEP 8.
- Scripts live under `Unity/Assets/Scripts/<Area>/` as laid out in `plan.md` §2.
- Never hand-write `.meta` GUIDs; let Unity generate them.
- Build gameplay with a capsule stand-in first; the Blender character comes in M6–M7.

## Working with the Unity editor

The user may keep the editor open while Claude works. Check with `Get-Process Unity`.

- Text assets (C# scripts, `.inputactions` JSON, UXML/USS/TSS, shaders): edit the files directly.
  The editor reimports them when it regains focus.
- Scenes, prefabs, ScriptableObjects, materials: build them with an editor script in
  `Unity/Assets/Editor/` exposed as a menu item under `Tools/MyGame/`, so the setup is
  reproducible. Editor open → ask the user to run the menu item. Editor closed → run it with
  `-batchmode -quit -projectPath Unity -executeMethod <Class.Method>`.
- `Tools/MyGame/Build All` (`MainSceneSetup.BuildAll`) rebuilds every generated asset and the
  scene (plan.md §2.1). To change a generated prefab or material, change its builder and rerun;
  hand edits are lost on the next build. The levels (`Assets/ScriptableObjects/Levels/`) and
  `WorldList` are content, never overwritten by builders; edit them with `Tools/MyGame/Map Tool`.
  Play progress lives outside the project (`progress.json` in `Application.persistentDataPath`);
  a test that writes it must back it up and restore it.
  Builders must be deterministic: running Build All twice must leave `git status` clean.
  Give every child of a generated prefab a unique name; prefab saving matches children by name,
  and duplicates get new file IDs on every build.
  Scene components whose tuning lives in code (`GrassField`) are reset to their script defaults
  by `EditorAssetUtility.ResetToDefaults`; changing a code default alone does not update a
  component already saved in the scene otherwise.
- Never hand-edit `.unity`/`.prefab` YAML; an open editor would overwrite it on save.
- Batch mode fails while the project is open in the editor (project lock).
- Visual check without the editor: a temporary editor script can build the map in edit mode,
  `camera.Render()` into a RenderTexture, and save a PNG to the scratchpad. Render **twice**;
  in batch mode the first render uses stale material data. Delete the script afterwards.
  `camera.Render()` does not include UI Toolkit. To capture the UI, run a temporary script
  without `-quit`: open the scene, `EditorApplication.EnterPlaymode()`, drive it from
  `EditorApplication.update`, give the `UIDocument` a copy of its `PanelSettings` with a
  `targetTexture` (clear to transparent), composite that over a camera render, then
  `EditorApplication.Exit(0)`.

- Play-mode input in batch mode: add a virtual `Keyboard` (`InputSystem.AddDevice<Keyboard>()`,
  `QueueStateEvent` with a `KeyboardState`) and, in memory only, set
  `InputSystem.settings.backgroundBehavior = IgnoreFocus` and `editorInputBehaviorInPlayMode =
  AllDeviceInputAlwaysGoesToGameView`; without focus the keyboard is otherwise ignored.
- Mesh vertex colors are not converted to linear for us (the project is linear): convert sRGB
  colors with `Color.linear` when writing them, as `VoxelMeshBuilder` does.
- Map tool checks run without the window: `MapLayoutValidator.Validate(layout)` is plain C#, so a
  temporary batch script can assert on its `LayoutReport`. The preview objects are `DontSave`;
  a scene saved with the preview on must stay byte-identical.

## Environment

- Windows PowerShell 5.1 reads BOM-less UTF-8 as the ANSI code page (CP949) and corrupts
  non-ASCII text (`§`, `×`, `—`, `→`, Korean). Never round-trip project text files through
  `Get-Content`/`Set-Content`/`Out-File`; use the Edit tool, Git Bash, or
  `[IO.File]::ReadAllText`/`WriteAllText` with `UTF8Encoding($false)`.

- Unity editor: `C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe`.
  The project at `Unity/` was created in batch mode from the bundled
  `com.unity.template.urp-blank` template.
- Git: the repository root is `FoxIsland/` (formerly `MyGame/`); `.gitignore` there covers
  `Unity/` paths.
  Commit only when the user says to; the user decides when.
- `.gitattributes` forces LF for all text files and routes binary assets (models, images,
  audio, fonts, `LightingData.asset`) through Git LFS. When adding a new binary file type,
  add its LFS pattern there before the first commit of such a file.
- Codex CLI (image generation available, logged in with ChatGPT): not on PATH; it ships inside
  the Codex desktop app at `C:\Program Files\WindowsApps\OpenAI.Codex_<version>_x64__2p2nqsd0c76g0\app\resources\codex.exe`.
  The version changes with app updates; find it with
  `(Get-AppxPackage | Where-Object Name -like '*Codex*').InstallLocation`.
- Blender 5.2 LTS: `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`. Regenerate the
  character (from the repo root, Git Bash):
  `blender --background --factory-startup --python Tools/Blender/generate_character.py -- --animal fox --out Unity/Assets/Art/Character/Fox.fbx --preview <scratch dir>`
  and look at the preview PNGs before handing the FBX to Unity. Each export rewrites the whole
  binary (LFS), so regenerate only when the model or animations actually change.

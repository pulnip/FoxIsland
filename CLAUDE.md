# MyGame — Voxel Animal Platformer

Unity 6 3D game: a voxel animal (built with Blender Python) walks and jumps on a 4×4 tile map
under a fixed quarter-view camera, collecting random items. Falling = game over,
all items = clear, R = restart.

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
3. If a decision changed the spec, update `docs/plan.md` and, when the change is user-visible,
   `docs/plan.html` too.

Project knowledge that must persist across sessions goes in this file, not in Claude's memory.

## Conventions

- C# (Unity): standard .NET conventions. The C++ rules in the global CLAUDE.md do not apply.
- Python (Blender scripts): PEP 8.
- Scripts live under `Unity/Assets/Scripts/<Area>/` as laid out in `plan.md` §2.
- Never hand-write `.meta` GUIDs; let Unity generate them.
- Build gameplay with a capsule stand-in first; the Blender character comes in M6–M7.

## Working with the Unity editor

The user may keep the editor open while Claude works. Check with `Get-Process Unity`.

- Text assets (C# scripts, `.inputactions` JSON, shaders): edit the files directly.
  The editor reimports them when it regains focus.
- Scenes, prefabs, ScriptableObjects, materials: build them with an editor script in
  `Unity/Assets/Editor/` exposed as a menu item under `Tools/MyGame/`, so the setup is
  reproducible. Editor open → ask the user to run the menu item. Editor closed → run it with
  `-batchmode -quit -projectPath Unity -executeMethod <Class.Method>`.
- Never hand-edit `.unity`/`.prefab` YAML; an open editor would overwrite it on save.
- Batch mode fails while the project is open in the editor (project lock).

## Environment

- Unity editor: `C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe`.
  The project at `Unity/` was created in batch mode from the bundled
  `com.unity.template.urp-blank` template.
- Git: the repository root is `MyGame/`; `.gitignore` there covers `Unity/` paths.
  Commit only when the user says to; the user decides when.
- Blender runs headless:
  `blender --background --python Tools/Blender/generate_character.py -- --animal fox --out Art/Character/Fox.fbx`

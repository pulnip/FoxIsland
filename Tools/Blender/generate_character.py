"""Build a rigged, animated voxel animal and export it as FBX.

Run headless:

    blender --background --factory-startup \
        --python Tools/Blender/generate_character.py -- \
        --animal fox --out Unity/Assets/Art/Character/Fox.fbx \
        [--preview DIR] [--blend PATH]

Each body part becomes one mesh parented rigidly to its bone, so the rig
needs no skinning. The animal faces -Y in Blender, which the FBX axis
conversion turns into +Z in Unity.
"""

import argparse
import importlib.util
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

FPS = 30
BONE_LENGTH = 0.05

# (neighbour offset, quad corners counter-clockwise seen from outside)
CUBE_FACES = (
    ((1, 0, 0), ((1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1))),
    ((-1, 0, 0), ((0, 0, 0), (0, 0, 1), (0, 1, 1), (0, 1, 0))),
    ((0, 1, 0), ((0, 1, 0), (0, 1, 1), (1, 1, 1), (1, 1, 0))),
    ((0, -1, 0), ((0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1))),
    ((0, 0, 1), ((0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1))),
    ((0, 0, -1), ((0, 0, 0), (0, 1, 0), (1, 1, 0), (1, 0, 0))),
)

FRONT_LEGS = ("LegFL", "LegFR")
BACK_LEGS = ("LegBL", "LegBR")

# Each clip: (frame count, [(frame, pose)]). A pose maps a bone name to
# {"rot": (x, y, z) degrees in bone space, "lift": metres along the bone}.
# Bones point up, so bone X pitches (+ swings a leg back, dips the nose,
# raises the tail) and bone Y yaws. Unlisted bones rest at zero.


def _legs(front, back, pair_a=None, pair_b=None):
    """Leg pitches, either per front/back or per diagonal pair."""
    if pair_a is not None:
        return {
            "LegFL": {"rot": (pair_a, 0, 0)},
            "LegBR": {"rot": (pair_a, 0, 0)},
            "LegFR": {"rot": (pair_b, 0, 0)},
            "LegBL": {"rot": (pair_b, 0, 0)},
        }
    pose = {name: {"rot": (front, 0, 0)} for name in FRONT_LEGS}
    pose.update({name: {"rot": (back, 0, 0)} for name in BACK_LEGS})
    return pose


def _pose(legs=None, **bones):
    pose = dict(legs or {})
    pose.update(bones)
    return pose


ANIMATIONS = {
    "Idle": (30, [
        (0, _pose(Body={"lift": 0.0}, Tail={"rot": (0, -8, 0)})),
        (15, _pose(Body={"lift": 0.01}, Head={"rot": (3, 0, 0)},
                   Tail={"rot": (0, 8, 0)})),
        (30, _pose(Body={"lift": 0.0}, Tail={"rot": (0, -8, 0)})),
    ]),
    "Walk": (16, [
        (0, _pose(_legs(0, 0, 25, -25), Tail={"rot": (0, -12, 0)})),
        (4, _pose(_legs(0, 0, 0, 0), Body={"lift": 0.015},
                  Head={"rot": (2, 0, 0)})),
        (8, _pose(_legs(0, 0, -25, 25), Tail={"rot": (0, 12, 0)})),
        (12, _pose(_legs(0, 0, 0, 0), Body={"lift": 0.015},
                   Head={"rot": (2, 0, 0)})),
        (16, _pose(_legs(0, 0, 25, -25), Tail={"rot": (0, -12, 0)})),
    ]),
    "Jump": (9, [
        (0, _pose()),
        (9, _pose(_legs(-40, 40), Body={"rot": (-12, 0, 0)},
                  Head={"rot": (-8, 0, 0)}, Tail={"rot": (20, 0, 0)})),
    ]),
    "Fall": (12, [
        (0, _pose(_legs(-35, 25), Body={"rot": (8, 0, 0)},
                  Tail={"rot": (35, 0, 0)})),
        (6, _pose(_legs(-28, 18), Body={"rot": (8, 0, 0)},
                  Tail={"rot": (30, 0, 0)})),
        (12, _pose(_legs(-35, 25), Body={"rot": (8, 0, 0)},
                   Tail={"rot": (35, 0, 0)})),
    ]),
}


PREVIEW_FRAMES = {"Idle": 15, "Walk": 0, "Jump": 9, "Fall": 0}


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--animal", required=True)
    parser.add_argument("--out", required=True, help="FBX output path")
    parser.add_argument("--preview", help="directory for preview PNGs")
    parser.add_argument("--blend", help="also save the scene as .blend")
    return parser.parse_args(argv)


def load_animal(name):
    here = os.path.dirname(os.path.abspath(__file__))
    path = os.path.join(here, "animals", f"{name}.py")
    spec = importlib.util.spec_from_file_location(f"animals.{name}", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = FPS
    return scene


def rasterize(boxes):
    """Return {(x, y, z): palette symbol}; later boxes win."""
    voxels = {}
    for symbol, (x0, y0, z0), (x1, y1, z1) in boxes:
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                for z in range(z0, z1 + 1):
                    voxels[(x, y, z)] = symbol
    return voxels


def make_material(name, rgb):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*rgb, 1.0)
    if not material.node_tree:
        material.use_nodes = True
    nodes = material.node_tree.nodes
    bsdf = next((n for n in nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        nodes.clear()
        bsdf = nodes.new("ShaderNodeBsdfPrincipled")
        output = nodes.new("ShaderNodeOutputMaterial")
        material.node_tree.links.new(bsdf.outputs[0], output.inputs[0])
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.9
    return material


def build_part_mesh(name, voxels, size, materials):
    """One mesh per part; faces between voxels of the same part are culled.

    Faces against other parts are kept: they show when the parts rotate.
    """
    symbols = sorted(set(voxels.values()))
    slot_of = {symbol: i for i, symbol in enumerate(symbols)}
    vertex_index = {}
    vertices = []
    faces = []
    face_slots = []

    for (x, y, z), symbol in voxels.items():
        for (dx, dy, dz), corners in CUBE_FACES:
            if (x + dx, y + dy, z + dz) in voxels:
                continue
            face = []
            for cx, cy, cz in corners:
                key = (x + cx, y + cy, z + cz)
                if key not in vertex_index:
                    vertex_index[key] = len(vertices)
                    vertices.append(Vector(key) * size)
                face.append(vertex_index[key])
            faces.append(face)
            face_slots.append(slot_of[symbol])

    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    for symbol in symbols:
        mesh.materials.append(materials[symbol])
    mesh.polygons.foreach_set("material_index", face_slots)
    mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def build_armature(name, parts, size):
    data = bpy.data.armatures.new(f"{name}Armature")
    armature = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(armature)
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)

    bpy.ops.object.mode_set(mode="EDIT")
    root = data.edit_bones.new("Root")
    root.head = (0.0, 0.0, 0.0)
    root.tail = (0.0, 0.0, BONE_LENGTH)
    created = {"Root": root}

    pending = dict(parts)
    while pending:
        for bone_name, part in list(pending.items()):
            parent = created.get(part["parent"])
            if parent is None:
                continue
            bone = data.edit_bones.new(bone_name)
            bone.head = Vector(part["pivot"]) * size
            bone.tail = bone.head + Vector((0.0, 0.0, BONE_LENGTH))
            bone.roll = 0.0
            bone.parent = parent
            created[bone_name] = bone
            del pending[bone_name]
    bpy.ops.object.mode_set(mode="OBJECT")

    for pose_bone in armature.pose.bones:
        pose_bone.rotation_mode = "XYZ"
    return armature


def parent_to_bone(obj, armature, bone_name):
    """Rigidly attach obj to a bone while keeping its world placement."""
    world = obj.matrix_world.copy()
    obj.parent = armature
    obj.parent_type = "BONE"
    obj.parent_bone = bone_name
    bpy.context.view_layer.update()
    obj.matrix_world = world


def key_pose(armature, frame, pose):
    for pose_bone in armature.pose.bones:
        spec = pose.get(pose_bone.name, {})
        rx, ry, rz = spec.get("rot", (0, 0, 0))
        pose_bone.rotation_euler = tuple(map(math.radians, (rx, ry, rz)))
        pose_bone.location = (0.0, spec.get("lift", 0.0), 0.0)
        group = pose_bone.name
        pose_bone.keyframe_insert("rotation_euler", frame=frame, group=group)
        pose_bone.keyframe_insert("location", frame=frame, group=group)


def build_actions(armature):
    armature.animation_data_create()
    actions = {}
    for clip_name, (_, keys) in ANIMATIONS.items():
        action = bpy.data.actions.new(clip_name)
        action.use_fake_user = True
        armature.animation_data.action = action
        for frame, pose in keys:
            key_pose(armature, frame, pose)
        actions[clip_name] = action
    armature.animation_data.action = actions["Idle"]
    return actions


def export_fbx(path):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=False,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=False,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=False,
        armature_nodetype="NULL",
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        path_mode="AUTO",
        embed_textures=False,
    )


def render_previews(scene, armature, actions, directory):
    """Render one still per clip at its most telling frame."""
    os.makedirs(directory, exist_ok=True)
    scene.render.engine = "BLENDER_WORKBENCH"
    # Flat palette colors; cavity and outline carry the shape.
    shading = scene.display.shading
    shading.light = "FLAT"
    shading.color_type = "MATERIAL"
    shading.show_cavity = True
    shading.cavity_type = "WORLD"
    shading.show_object_outline = True
    scene.render.resolution_x = 480
    scene.render.resolution_y = 480
    scene.render.film_transparent = False
    # Unity reads the palette as sRGB; Raw shows the same values unconverted.
    scene.view_settings.view_transform = "Raw"

    world = bpy.data.worlds.new("PreviewWorld")
    world.color = (0.62, 0.78, 0.90)
    scene.world = world

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 1.3
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    scene.collection.objects.link(camera)
    target = Vector((0.0, 0.0, 0.28))
    camera.location = target + Vector((1.4, -1.4, 1.0))
    direction = target - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    scene.camera = camera

    for clip_name, action in actions.items():
        armature.animation_data.action = action
        scene.frame_set(PREVIEW_FRAMES[clip_name])
        scene.render.filepath = os.path.join(directory, f"{clip_name}.png")
        bpy.ops.render.render(write_still=True)

    bpy.data.objects.remove(camera)
    armature.animation_data.action = actions["Idle"]
    scene.frame_set(0)


def main():
    args = parse_args()
    animal = load_animal(args.animal)
    scene = reset_scene()
    size = animal.VOXEL_SIZE

    materials = {
        symbol: make_material(f"{args.animal}_{symbol}", rgb)
        for symbol, rgb in animal.PALETTE.items()
    }
    armature = build_armature(args.animal.capitalize(), animal.PARTS, size)
    for part_name, part in animal.PARTS.items():
        voxels = rasterize(part["boxes"])
        obj = build_part_mesh(f"Mesh_{part_name}", voxels, size, materials)
        obj.matrix_world = Matrix.Identity(4)
        parent_to_bone(obj, armature, part_name)

    actions = build_actions(armature)
    scene.frame_set(0)
    export_fbx(args.out)
    print(f"[generate_character] exported {args.out}")

    if args.preview:
        render_previews(scene, armature, actions, args.preview)
        print(f"[generate_character] previews in {args.preview}")
    if args.blend:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args.blend))
        print(f"[generate_character] saved {args.blend}")


if __name__ == "__main__":
    main()

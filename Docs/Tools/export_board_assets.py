"""Export the approved Blender board geometry as Unity-ready FBX assets.

The preview builder remains the single source for dimensions and mesh shapes.
This script packages those shapes into reusable model prefabs; Unity only
positions and colors the imported geometry at runtime.
"""

from __future__ import annotations

import argparse
import os
import sys
from collections.abc import Callable

import bpy


SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
SOURCE_DIR = os.path.join(PROJECT_DIR, "ArtSource")
EXPORT_DIR = os.path.join(
    PROJECT_DIR,
    "Assets",
    "OrbitSort",
    "Resources",
    "Models",
)
BLEND_PATH = os.path.join(SOURCE_DIR, "OrbitSortBoardAssets.blend")

sys.path.insert(0, SCRIPT_DIR)

from render_three_ring_asset_preview import (  # noqa: E402
    RINGS,
    TRACK_HALF_WIDTH,
    add_portal,
    add_receiver,
    add_ring_asset,
    add_uv_sphere,
    material,
    reset_scene,
)


def capture_objects(action: Callable[[], None]) -> list[bpy.types.Object]:
    before = set(bpy.data.objects)
    action()
    return [obj for obj in bpy.data.objects if obj not in before]


def create_root(
    name: str,
    children: list[bpy.types.Object],
) -> bpy.types.Object:
    root = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(root)
    for child in children:
        child.parent = root
    return root


def descendants(root: bpy.types.Object) -> list[bpy.types.Object]:
    result = [root]
    for child in root.children:
        result.extend(descendants(child))
    return result


def export_asset(root: bpy.types.Object, filename: str) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in descendants(root):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root

    path = os.path.join(EXPORT_DIR, filename)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_tspace=True,
        add_leaf_bones=False,
        bake_anim=False,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        use_space_transform=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        path_mode="AUTO",
        embed_textures=False,
    )
    print(f"Exported Unity model: {path}")


def build_assets() -> dict[str, bpy.types.Object]:
    reset_scene()
    os.makedirs(SOURCE_DIR, exist_ok=True)
    os.makedirs(EXPORT_DIR, exist_ok=True)

    cream = material(
        "Warm Ivory Rails",
        (0.89, 0.78, 0.60),
        roughness=0.28,
    )
    lavender = material(
        "Lavender Track Floor",
        (0.41, 0.37, 0.58),
        roughness=0.42,
    )
    gold = material(
        "Brushed Gold Portals",
        (0.92, 0.48, 0.035),
        metallic=0.62,
        roughness=0.25,
    )
    white = material(
        "Portal Arrow White",
        (1.0, 0.97, 0.88),
        roughness=0.20,
    )
    dark = material(
        "Deep Portal Interior",
        (0.018, 0.009, 0.06),
        roughness=0.48,
    )
    blue = material(
        "Blue Marble",
        (0.025, 0.24, 0.95),
        metallic=0.05,
        roughness=0.10,
    )
    receiver_blue = material(
        "Blue Receiver",
        (0.015, 0.22, 0.95),
        metallic=0.10,
        roughness=0.18,
    )

    assets: dict[str, bpy.types.Object] = {}
    ring_exports = (
        ("RingInner", "RingInner.fbx"),
        ("RingMiddle", "RingMiddle.fbx"),
        ("RingOuter", "RingOuter.fbx"),
    )
    for ring, (root_name, _) in zip(RINGS, ring_exports, strict=True):
        ring_without_marbles = dict(ring)
        ring_without_marbles["marbles"] = ()
        parts = capture_objects(
            lambda ring_data=ring_without_marbles: add_ring_asset(
                ring_data,
                cream,
                lavender,
                {},
            )
        )
        assets[root_name] = create_root(root_name, parts)

    gap_width = (
        RINGS[1]["radius"]
        - TRACK_HALF_WIDTH
        - 0.09
        - (RINGS[0]["radius"] + TRACK_HALF_WIDTH + 0.09)
    )
    portal_parts = capture_objects(
        lambda: add_portal(
            "Portal",
            90.0,
            -gap_width * 0.5,
            gap_width * 0.5,
            gold,
            white,
            dark,
        )
    )
    assets["Portal"] = create_root("Portal", portal_parts)

    receiver_parts = capture_objects(
        lambda: add_receiver(
            "Receiver",
            0.0,
            receiver_blue,
            dark,
        )
    )
    for receiver_part in receiver_parts:
        receiver_part.location.x -= 5.67
    assets["Receiver"] = create_root("Receiver", receiver_parts)

    center_parts = capture_objects(
        lambda: _add_center_hub(dark)
    )
    assets["CenterHub"] = create_root("CenterHub", center_parts)

    marble_parts = capture_objects(
        lambda: add_uv_sphere(
            "Marble",
            (0.0, 0.0, 0.74),
            0.32,
            blue,
        )
    )
    assets["Marble"] = create_root("Marble", marble_parts)

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print(f"Saved Blender source: {BLEND_PATH}")
    return assets


def _add_center_hub(dark: bpy.types.Material) -> None:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=96,
        radius=0.68,
        depth=0.25,
        location=(0.0, 0.0, 0.23),
    )
    hub = bpy.context.object
    hub.name = "Dark Center Hub"
    hub.data.materials.append(dark)
    bevel = hub.modifiers.new("Center Hub Bevel", "BEVEL")
    bevel.width = 0.10
    bevel.segments = 6


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--rebuild",
        action="store_true",
        help=(
            "Rebuild the Blender source from the approved scripted geometry "
            "before exporting. Without this flag, the checked-in .blend file "
            "is exported as-is."
        ),
    )
    arguments, _ = parser.parse_known_args()
    return arguments


def load_asset_roots() -> dict[str, bpy.types.Object]:
    bpy.ops.wm.open_mainfile(filepath=BLEND_PATH)
    root_names = (
        "RingInner",
        "RingMiddle",
        "RingOuter",
        "Portal",
        "Receiver",
        "CenterHub",
        "Marble",
    )
    assets: dict[str, bpy.types.Object] = {}
    for root_name in root_names:
        root = bpy.data.objects.get(root_name)
        if root is None:
            raise RuntimeError(
                f"Blender source is missing asset root '{root_name}'."
            )
        assets[root_name] = root
    return assets


def main() -> None:
    arguments = parse_arguments()
    if arguments.rebuild or not os.path.isfile(BLEND_PATH):
        assets = build_assets()
    else:
        os.makedirs(EXPORT_DIR, exist_ok=True)
        assets = load_asset_roots()

    exports = (
        ("RingInner", "RingInner.fbx"),
        ("RingMiddle", "RingMiddle.fbx"),
        ("RingOuter", "RingOuter.fbx"),
        ("Portal", "Portal.fbx"),
        ("Receiver", "Receiver.fbx"),
        ("CenterHub", "CenterHub.fbx"),
        ("Marble", "Marble.fbx"),
    )
    for root_name, filename in exports:
        export_asset(assets[root_name], filename)


if __name__ == "__main__":
    main()

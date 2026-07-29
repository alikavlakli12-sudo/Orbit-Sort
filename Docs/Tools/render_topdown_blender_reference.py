"""Render the approved Blender preview from the gameplay camera angle."""

from __future__ import annotations

import os

import bpy
from mathutils import Vector


SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PROJECT_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, "..", ".."))
BLEND_PATH = os.path.join(
    PROJECT_DIR,
    "Docs",
    "Previews",
    "orbit_sort_three_ring_asset_preview.blend",
)
OUTPUT_PATH = os.path.join(
    PROJECT_DIR,
    "Docs",
    "Previews",
    "orbit_sort_blender_topdown_reference.png",
)


def look_at(
    obj: bpy.types.Object,
    target: tuple[float, float, float],
) -> None:
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def main() -> None:
    bpy.ops.wm.open_mainfile(filepath=BLEND_PATH)
    scene = bpy.context.scene
    camera = scene.camera
    if camera is None:
        raise RuntimeError("The approved Blender preview has no camera.")

    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 13.3
    camera.location = (0.0, 0.0, 15.0)
    look_at(camera, (0.0, 0.0, 0.25))

    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 1400
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = OUTPUT_PATH
    scene.render.film_transparent = False
    bpy.ops.render.render(write_still=True)
    print(f"Top-down Blender reference: {OUTPUT_PATH}")


if __name__ == "__main__":
    main()

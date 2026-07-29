"""Render the Orbit Sort three-ring asset concept with exact gameplay geometry."""

import math
import os

import bpy
from mathutils import Vector


SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PREVIEW_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, "..", "Previews"))
RENDER_PATH = os.path.join(
    PREVIEW_DIR,
    "orbit_sort_three_ring_asset_preview.png",
)
BLEND_PATH = os.path.join(
    PREVIEW_DIR,
    "orbit_sort_three_ring_asset_preview.blend",
)

TRACK_HALF_WIDTH = 0.48
RINGS = (
    {
        "name": "Inner Ring",
        "radius": 1.80,
        "capacity": 8,
        "marbles": ((1, "blue"), (3, "red"), (5, "yellow")),
    },
    {
        "name": "Middle Ring",
        "radius": 3.28,
        "capacity": 12,
        "marbles": (
            (2, "yellow"),
            (5, "blue"),
            (8, "red"),
            (10, "yellow"),
        ),
    },
    {
        "name": "Outer Ring",
        "radius": 4.76,
        "capacity": 16,
        "marbles": (
            (1, "red"),
            (4, "blue"),
            (7, "yellow"),
            (10, "red"),
            (13, "blue"),
        ),
    },
)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, color, metallic=0.0, roughness=0.4):
    result = bpy.data.materials.new(name)
    result.diffuse_color = (*color, 1.0)
    result.use_nodes = True
    shader = result.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    return result


def add_annular_prism(
    name,
    inner_radius,
    outer_radius,
    height,
    z_center,
    mat,
    bevel=0.04,
    segments=192,
):
    vertices = []
    faces = []
    bottom = -height * 0.5
    top = height * 0.5

    for index in range(segments):
        angle = index * math.tau / segments
        cosine = math.cos(angle)
        sine = math.sin(angle)
        vertices.extend(
            (
                (inner_radius * cosine, inner_radius * sine, bottom),
                (outer_radius * cosine, outer_radius * sine, bottom),
                (inner_radius * cosine, inner_radius * sine, top),
                (outer_radius * cosine, outer_radius * sine, top),
            )
        )

    for index in range(segments):
        next_index = (index + 1) % segments
        current = index * 4
        following = next_index * 4
        faces.extend(
            (
                (current + 2, current + 3, following + 3, following + 2),
                (current, following, following + 1, current + 1),
                (current + 1, following + 1, following + 3, current + 3),
                (current, current + 2, following + 2, following),
            )
        )

    mesh = bpy.data.meshes.new(f"{name} Mesh")
    mesh.from_pydata(vertices, (), faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location.z = z_center
    obj.data.materials.append(mat)

    if bevel > 0.0:
        modifier = obj.modifiers.new("Rounded Edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 4

    return obj


def add_torus(name, major_radius, minor_radius, z, mat):
    bpy.ops.mesh.primitive_torus_add(
        align="WORLD",
        major_segments=192,
        minor_segments=24,
        major_radius=major_radius,
        minor_radius=minor_radius,
        location=(0.0, 0.0, z),
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def add_rounded_box(name, location, dimensions, rotation_z, mat, bevel=0.10):
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    obj.rotation_euler.z = rotation_z
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    modifier = obj.modifiers.new("Rounded Corners", "BEVEL")
    modifier.width = bevel
    modifier.segments = 6
    return obj


def add_triangle_prism(name, center, rotation_z, mat):
    local_points = (
        (-0.16, -0.15),
        (0.16, -0.15),
        (0.0, 0.22),
    )
    thickness = 0.035
    vertices = []
    for z in (-thickness * 0.5, thickness * 0.5):
        for x, y in local_points:
            vertices.append((x, y, z))

    faces = (
        (0, 2, 1),
        (3, 4, 5),
        (0, 1, 4, 3),
        (1, 2, 5, 4),
        (2, 0, 3, 5),
    )
    mesh = bpy.data.meshes.new(f"{name} Mesh")
    mesh.from_pydata(vertices, (), faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = center
    obj.rotation_euler.z = rotation_z
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new("Soft Arrow Edges", "BEVEL")
    bevel.width = 0.025
    bevel.segments = 3
    return obj


def add_uv_sphere(name, location, radius, mat):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=48,
        ring_count=24,
        radius=radius,
        location=location,
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    bevel = obj.modifiers.new("Marble Polish", "BEVEL")
    bevel.width = 0.012
    bevel.segments = 2
    return obj


def point_on_ring(radius, index, capacity):
    angle = math.radians(90.0 - index * (360.0 / capacity))
    return radius * math.cos(angle), radius * math.sin(angle)


def add_ring_asset(ring, cream, lavender, marble_materials):
    radius = ring["radius"]
    inner_radius = radius - TRACK_HALF_WIDTH
    outer_radius = radius + TRACK_HALF_WIDTH

    add_annular_prism(
        f"{ring['name']} Ivory Base",
        inner_radius - 0.09,
        outer_radius + 0.09,
        0.24,
        0.20,
        cream,
        bevel=0.045,
    )
    add_annular_prism(
        f"{ring['name']} Lavender Trough",
        inner_radius + 0.08,
        outer_radius - 0.08,
        0.12,
        0.34,
        lavender,
        bevel=0.055,
    )
    add_torus(
        f"{ring['name']} Inner Rail",
        inner_radius,
        0.105,
        0.43,
        cream,
    )
    add_torus(
        f"{ring['name']} Outer Rail",
        outer_radius,
        0.105,
        0.43,
        cream,
    )

    for marble_index, (slot, color_name) in enumerate(ring["marbles"]):
        x, y = point_on_ring(radius, slot, ring["capacity"])
        add_uv_sphere(
            f"{ring['name']} {color_name.title()} Marble {marble_index + 1}",
            (x, y, 0.74),
            0.32,
            marble_materials[color_name],
        )


def add_portal(name, angle_degrees, gap_inner, gap_outer, gold, white, dark):
    angle = math.radians(angle_degrees)
    radius = (gap_inner + gap_outer) * 0.5
    radial_length = gap_outer - gap_inner + 0.34
    x = radius * math.cos(angle)
    y = radius * math.sin(angle)
    rotation = angle - math.pi * 0.5

    add_rounded_box(
        f"{name} Gold Bridge",
        (x, y, 0.60),
        (0.66, radial_length, 0.30),
        rotation,
        gold,
        bevel=0.13,
    )
    add_rounded_box(
        f"{name} Dark Passage",
        (x, y, 0.765),
        (0.31, radial_length * 0.70, 0.045),
        rotation,
        dark,
        bevel=0.07,
    )
    add_triangle_prism(
        f"{name} Outward Arrow",
        (x, y, 0.815),
        rotation,
        white,
    )


def add_receiver(name, angle_degrees, colored_mat, dark):
    angle = math.radians(angle_degrees)
    radius = 5.67
    x = radius * math.cos(angle)
    y = radius * math.sin(angle)

    bpy.ops.mesh.primitive_cylinder_add(
        vertices=64,
        radius=0.29,
        depth=0.20,
        location=(x, y, 0.31),
    )
    base = bpy.context.object
    base.name = f"{name} Short Receiver Base"
    base.data.materials.append(colored_mat)
    bevel = base.modifiers.new("Receiver Base Bevel", "BEVEL")
    bevel.width = 0.07
    bevel.segments = 4

    bpy.ops.mesh.primitive_torus_add(
        major_segments=96,
        minor_segments=24,
        major_radius=0.30,
        minor_radius=0.10,
        location=(x, y, 0.48),
    )
    lip = bpy.context.object
    lip.name = f"{name} Flared Receiver Lip"
    lip.data.materials.append(colored_mat)
    for polygon in lip.data.polygons:
        polygon.use_smooth = True

    bpy.ops.mesh.primitive_cylinder_add(
        vertices=64,
        radius=0.205,
        depth=0.055,
        location=(x, y, 0.47),
    )
    opening = bpy.context.object
    opening.name = f"{name} Dark Receiver Opening"
    opening.data.materials.append(dark)


def look_at(obj, target=(0.0, 0.0, 0.25)):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def add_area_light(name, location, energy, size, color):
    bpy.ops.object.light_add(type="AREA", location=location)
    light = bpy.context.object
    light.name = name
    light.data.energy = energy
    light.data.shape = "DISK"
    light.data.size = size
    light.data.color = color
    look_at(light)


def configure_render():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1600
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = RENDER_PATH
    scene.render.film_transparent = False

    if scene.world is None:
        scene.world = bpy.data.worlds.new("Orbit Sort Preview World")
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.012, 0.008, 0.045, 1.0)
    background.inputs["Strength"].default_value = 0.22

    try:
        scene.view_settings.look = "AgX - Medium High Contrast"
    except TypeError:
        pass

    bpy.ops.object.camera_add(location=(9.0, -11.0, 12.5))
    camera = bpy.context.object
    camera.name = "Orbit Sort Preview Camera"
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 13.3
    camera.data.lens = 50
    look_at(camera)
    scene.camera = camera


def build_scene():
    reset_scene()
    os.makedirs(PREVIEW_DIR, exist_ok=True)

    cream = material("Warm Ivory Rails", (0.89, 0.78, 0.60), roughness=0.28)
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
    white = material("Portal Arrow White", (1.0, 0.97, 0.88), roughness=0.2)
    dark = material("Deep Portal Interior", (0.018, 0.009, 0.06), roughness=0.48)
    blue = material("Blue Marble", (0.025, 0.24, 0.95), metallic=0.05, roughness=0.10)
    red = material("Red Marble", (0.93, 0.035, 0.018), metallic=0.03, roughness=0.10)
    yellow = material(
        "Yellow Marble",
        (1.0, 0.54, 0.015),
        metallic=0.03,
        roughness=0.11,
    )
    receiver_blue = material(
        "Blue Receiver",
        (0.015, 0.22, 0.95),
        metallic=0.10,
        roughness=0.18,
    )
    receiver_red = material(
        "Red Receiver",
        (0.93, 0.025, 0.018),
        metallic=0.08,
        roughness=0.18,
    )
    receiver_yellow = material(
        "Yellow Receiver",
        (1.0, 0.52, 0.01),
        metallic=0.10,
        roughness=0.18,
    )
    ground = material("Studio Floor", (0.017, 0.012, 0.065), roughness=0.48)

    bpy.ops.mesh.primitive_plane_add(size=40.0, location=(0.0, 0.0, -0.02))
    floor = bpy.context.object
    floor.name = "Navy Studio Floor"
    floor.data.materials.append(ground)

    bpy.ops.mesh.primitive_cylinder_add(
        vertices=96,
        radius=0.68,
        depth=0.25,
        location=(0.0, 0.0, 0.23),
    )
    hub = bpy.context.object
    hub.name = "Dark Center Hub"
    hub.data.materials.append(dark)
    hub_bevel = hub.modifiers.new("Center Hub Bevel", "BEVEL")
    hub_bevel.width = 0.10
    hub_bevel.segments = 6

    marble_materials = {
        "blue": blue,
        "red": red,
        "yellow": yellow,
    }
    for ring in RINGS:
        add_ring_asset(ring, cream, lavender, marble_materials)

    inner_outer_edge = RINGS[0]["radius"] + TRACK_HALF_WIDTH + 0.09
    middle_inner_edge = RINGS[1]["radius"] - TRACK_HALF_WIDTH - 0.09
    middle_outer_edge = RINGS[1]["radius"] + TRACK_HALF_WIDTH + 0.09
    outer_inner_edge = RINGS[2]["radius"] - TRACK_HALF_WIDTH - 0.09

    add_portal(
        "Inner To Middle Portal",
        90.0,
        inner_outer_edge,
        middle_inner_edge,
        gold,
        white,
        dark,
    )
    add_portal(
        "Middle To Outer Portal",
        -90.0,
        middle_outer_edge,
        outer_inner_edge,
        gold,
        white,
        dark,
    )

    add_receiver("Blue", 90.0, receiver_blue, dark)
    add_receiver("Red", -22.5, receiver_red, dark)
    add_receiver("Yellow", -157.5, receiver_yellow, dark)

    add_area_light(
        "Warm Key Light",
        (4.5, -5.5, 11.0),
        1050.0,
        5.5,
        (1.0, 0.82, 0.66),
    )
    add_area_light(
        "Cool Fill Light",
        (-6.0, -1.5, 7.0),
        720.0,
        5.0,
        (0.46, 0.58, 1.0),
    )
    add_area_light(
        "Soft Rim Light",
        (1.0, 7.0, 9.0),
        950.0,
        4.0,
        (0.62, 0.48, 1.0),
    )

    configure_render()
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    bpy.ops.render.render(write_still=True)
    print(f"Saved blend: {BLEND_PATH}")
    print(f"Saved render: {RENDER_PATH}")


if __name__ == "__main__":
    build_scene()

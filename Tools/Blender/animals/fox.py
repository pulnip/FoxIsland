"""Voxel data for the fox.

Coordinates are voxel indices in Blender space: +X is the fox's left,
-Y is forward (the nose points to -Y), +Z is up, and z = 0 is the ground.
Voxel (x, y, z) fills the unit cube [x, x+1] x [y, y+1] x [z, z+1].
Boxes are inclusive index ranges; later boxes paint over earlier ones.
Pivots lie on voxel boundaries, so they may fall between indices.
"""

VOXEL_SIZE = 0.05

PALETTE = {
    "o": (0.93, 0.47, 0.13),
    "w": (0.97, 0.95, 0.90),
    "k": (0.10, 0.08, 0.08),
}


def _leg(x0, y0):
    return {
        "parent": "Body",
        "pivot": (x0 + 1, y0 + 1, 3),
        "boxes": [
            ("o", (x0, y0, 0), (x0 + 1, y0 + 1, 2)),
            ("k", (x0, y0, 0), (x0 + 1, y0 + 1, 0)),
        ],
    }


PARTS = {
    "Body": {
        "parent": "Root",
        "pivot": (0, 0.5, 5),
        "boxes": [
            ("o", (-3, -3, 3), (2, 3, 6)),
            ("w", (-2, -2, 3), (1, 2, 3)),
            ("w", (-2, -3, 3), (1, -3, 4)),
        ],
    },
    "Head": {
        "parent": "Body",
        "pivot": (0, -3, 6),
        "boxes": [
            ("o", (-3, -8, 5), (2, -4, 9)),
            ("w", (-3, -8, 5), (2, -8, 6)),
            ("k", (-2, -8, 7), (-2, -8, 7)),
            ("k", (1, -8, 7), (1, -8, 7)),
            ("w", (-1, -10, 5), (0, -9, 6)),
            ("k", (-1, -10, 6), (0, -10, 6)),
            ("o", (-3, -6, 10), (-2, -6, 11)),
            ("o", (1, -6, 10), (2, -6, 11)),
            ("k", (-3, -6, 11), (-2, -6, 11)),
            ("k", (1, -6, 11), (2, -6, 11)),
        ],
    },
    "LegFL": _leg(1, -3),
    "LegFR": _leg(-3, -3),
    "LegBL": _leg(1, 2),
    "LegBR": _leg(-3, 2),
    "Tail": {
        "parent": "Body",
        "pivot": (0, 4, 6),
        "boxes": [
            ("o", (-1, 4, 5), (0, 5, 6)),
            ("o", (-1, 6, 6), (0, 7, 7)),
            ("w", (-1, 8, 6), (0, 8, 7)),
        ],
    },
}

#!/usr/bin/env python3
"""
QuickShot logo generator / candidate sheet.

The mark has to survive being scaled down to a 16 px tray glyph, so every size is drawn
natively (4x supersampled) with optical scaling: small sizes get a proportionally larger
frame and thicker strokes instead of a shrunken copy of the big artwork.

    py -3.11 tools/make-icon.py --sheet        # compare candidates -> docs/design/icon-candidates.png
    py -3.11 tools/make-icon.py --design B     # write the real assets (logo-*.png, .ico)

Candidates
    A  blue tile, white selection frame, amber flash in the middle      (safe, clear)
    B  blue tile, white frame with the flash sitting on the corner      (dynamic)  <- shipped
    C  navy tile, thick white corner brackets, amber flash              (closest to v1)
    D  blue tile, solid white "photo" frame, cut-out flash              (strongest shape)
"""

from __future__ import annotations

import argparse
import math
import os
import struct
from io import BytesIO

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.normpath(os.path.join(HERE, "..", "src", "QuickShot", "Assets"))
DOCS = os.path.normpath(os.path.join(HERE, "..", "docs", "design"))

SS = 4                                    # supersampling factor
ICO_SIZES = [16, 24, 32, 48, 64, 128, 256]

BLUE_TOP = (91, 174, 255)
BLUE_BOTTOM = (32, 96, 214)
NAVY_TOP = (43, 58, 85)
NAVY_BOTTOM = (18, 26, 38)
WHITE = (255, 255, 255)
AMBER = (255, 193, 45)
AMBER_CORE = (255, 249, 232)


def mix(c1, c2, t):
    return tuple(int(round(c1[i] + (c2[i] - c1[i]) * t)) for i in range(3))


def tile(size: int, top, bottom, radius_ratio=0.225) -> Image.Image:
    s = size * SS
    out = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    grad = Image.new("RGBA", (1, s))
    px = grad.load()
    for y in range(s):
        px[0, y] = mix(top, bottom, y / max(1, s - 1)) + (255,)
    grad = grad.resize((s, s), Image.NEAREST)
    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, s - 1, s - 1], radius=int(s * radius_ratio), fill=255)
    out.paste(grad, (0, 0), mask)
    return out


def spark(d: ImageDraw.ImageDraw, cx: float, cy: float, outer: float, core: bool) -> None:
    """Four-point flash with points up/right/down/left."""
    inner = outer * 0.30
    points = []
    for i in range(8):
        angle = math.radians(i * 45.0)
        r = outer if i % 2 == 0 else inner
        points.append((cx + r * math.sin(angle), cy - r * math.cos(angle)))
    d.polygon(points, fill=AMBER)

    if not core:
        return

    core_r = outer * 0.40
    core_points = []
    for i in range(8):
        angle = math.radians(i * 45.0)
        r = core_r if i % 2 == 0 else core_r * 0.26
        core_points.append((cx + r * math.sin(angle), cy - r * math.cos(angle)))
    d.polygon(core_points, fill=AMBER_CORE)


def metrics(size: int) -> tuple[float, float, float]:
    """(frame side ratio, stroke ratio, spark ratio) - optical scaling by size."""
    if size <= 24:
        return 0.62, 0.125, 0.150
    if size <= 48:
        return 0.56, 0.100, 0.145
    return 0.52, 0.085, 0.140


def frame_box(size: int, side_ratio: float) -> tuple[float, float, float, float]:
    s = size * SS
    side = s * side_ratio
    x0 = (s - side) / 2.0
    return x0, x0, x0 + side, x0 + side


def draw_a(size: int) -> Image.Image:      # blue tile + white frame + centred flash
    img = tile(size, BLUE_TOP, BLUE_BOTTOM)
    d = ImageDraw.Draw(img)
    side_ratio, stroke_ratio, spark_ratio = metrics(size)
    s = size * SS
    stroke = max(round(s * stroke_ratio), 2 * SS)
    x0, y0, x1, y1 = frame_box(size, side_ratio)
    radius = (x1 - x0) * 0.20
    inset = stroke / 2.0
    d.rounded_rectangle([x0 + inset, y0 + inset, x1 - inset, y1 - inset],
                        radius=radius, outline=WHITE, width=stroke)
    spark(d, s / 2, s / 2, s * spark_ratio, core=size >= 32)
    return img.resize((size, size), Image.LANCZOS)


def draw_b(size: int) -> Image.Image:      # flash sitting on the frame's top-right corner
    img = tile(size, BLUE_TOP, BLUE_BOTTOM)
    d = ImageDraw.Draw(img)
    side_ratio, stroke_ratio, spark_ratio = metrics(size)
    s = size * SS
    stroke = max(round(s * stroke_ratio), 2 * SS)
    x0, y0, x1, y1 = frame_box(size, side_ratio)
    radius = (x1 - x0) * 0.20
    inset = stroke / 2.0
    d.rounded_rectangle([x0 + inset, y0 + inset, x1 - inset, y1 - inset],
                        radius=radius, outline=WHITE, width=stroke)
    spark(d, x1 - stroke * 0.10, y0 + stroke * 0.10, s * spark_ratio * 1.15, core=size >= 32)
    return img.resize((size, size), Image.LANCZOS)


def draw_c(size: int) -> Image.Image:      # navy tile + thick corner brackets + flash
    img = tile(size, NAVY_TOP, NAVY_BOTTOM)
    d = ImageDraw.Draw(img)
    side_ratio, stroke_ratio, spark_ratio = metrics(size)
    s = size * SS
    stroke = max(round(s * (stroke_ratio * 1.15)), 2 * SS)
    x0, y0, x1, y1 = frame_box(size, min(side_ratio + 0.06, 0.66))
    arm = (x1 - x0) * 0.42
    r = stroke / 2.0

    for (cx, cy, hx, vy) in ((x0, y0, 1, 1), (x1, y0, -1, 1), (x0, y1, 1, -1), (x1, y1, -1, -1)):
        ax0, ax1 = (cx + r, cx + hx * arm) if hx > 0 else (cx - arm, cx - r)
        d.rounded_rectangle([ax0, cy - r, ax1, cy + r], radius=r, fill=WHITE)
        ay0, ay1 = (cy + r, cy + vy * arm) if vy > 0 else (cy - arm, cy - r)
        d.rounded_rectangle([cx - r, ay0, cx + r, ay1], radius=r, fill=WHITE)

    spark(d, s / 2, s / 2, s * spark_ratio, core=size >= 32)
    return img.resize((size, size), Image.LANCZOS)


def draw_d(size: int) -> Image.Image:      # blue tile + solid white photo frame + cut flash
    img = tile(size, BLUE_TOP, BLUE_BOTTOM)
    d = ImageDraw.Draw(img)
    side_ratio, _, spark_ratio = metrics(size)
    s = size * SS
    x0, y0, x1, y1 = frame_box(size, side_ratio)
    radius = (x1 - x0) * 0.22

    # solid white frame, then punch the "photo" area out of it so the tile shows through
    d.rounded_rectangle([x0, y0, x1, y1], radius=radius, fill=WHITE)
    inner_gap = (x1 - x0) * 0.20
    d.rounded_rectangle([x0 + inner_gap, y0 + inner_gap, x1 - inner_gap, y1 - inner_gap],
                        radius=radius * 0.7, fill=mix(BLUE_TOP, BLUE_BOTTOM, 0.35))

    spark(d, s / 2, s / 2, s * spark_ratio * 1.05, core=size >= 32)
    return img.resize((size, size), Image.LANCZOS)


DESIGNS = {"A": draw_a, "B": draw_b, "C": draw_c, "D": draw_d}


def write_ico(path: str, frames: dict[int, Image.Image]) -> None:
    images = [frames[size] for size in ICO_SIZES]
    blobs = []
    for image in images:
        buffer = BytesIO()
        image.save(buffer, format="PNG")
        blobs.append(buffer.getvalue())

    with open(path, "wb") as handle:
        handle.write(struct.pack("<HHH", 0, 1, len(images)))
        offset = 6 + 16 * len(images)
        for image, blob in zip(images, blobs):
            width = 0 if image.width >= 256 else image.width
            height = 0 if image.height >= 256 else image.height
            handle.write(struct.pack("<BBBBHHII", width, height, 0, 0, 1, 32, len(blob), offset))
            offset += len(blob)
        for blob in blobs:
            handle.write(blob)


def build_design(letter: str) -> dict[int, Image.Image]:
    return {size: DESIGNS[letter](size) for size in ICO_SIZES}


def write_assets(letter: str) -> None:
    os.makedirs(ASSETS, exist_ok=True)
    frames = build_design(letter)
    frames[256].resize((1024, 1024), Image.LANCZOS).save(os.path.join(ASSETS, "logo-1024.png"))
    frames[256].save(os.path.join(ASSETS, "logo-256.png"))
    write_ico(os.path.join(ASSETS, "quickshot.ico"), frames)
    print(f"assets written for design {letter}: logo-1024.png, logo-256.png, quickshot.ico")


def write_sheet(path: str) -> None:
    """Contact sheet: how each candidate looks down to 16 px, on light and dark bars."""
    cell_w, cell_h = 138, 150
    pad = 14
    label_w = 150
    dark_h = 62
    width = label_w + cell_w * 6 + pad * 2
    height = pad * 2 + (cell_h + dark_h) * len(DESIGNS)

    sheet = Image.new("RGB", (width, height), (246, 247, 249))
    d = ImageDraw.Draw(sheet)

    sizes_native = [128, 48, 32, 24, 16]
    y = pad
    for letter, drawer in DESIGNS.items():
        frames = {size: drawer(size) for size in sizes_native + [256]}

        d.rectangle([pad, y, pad + label_w - 12, y + cell_h - 12], fill=(255, 255, 255),
                    outline=(214, 219, 226))
        d.text((pad + 12, y + 18), f"Candidate {letter}", fill=(28, 34, 44))
        d.text((pad + 12, y + 40), size_note(letter), fill=(90, 98, 110))

        x = pad + label_w
        for size in sizes_native:
            cell_x0, cell_y0 = x, y
            d.rectangle([cell_x0, cell_y0, cell_x0 + cell_w - 10, cell_y0 + cell_h - 12],
                        fill=(255, 255, 255), outline=(224, 228, 234))
            image = frames[size]
            sheet.paste(image, (x + (cell_w - 10 - size) // 2, y + (cell_h - 12 - size) // 2 - 6),
                        image)
            d.text((x + 8, y + cell_h - 32), f"{size}px", fill=(120, 128, 138))
            x += cell_w

        d.rectangle([x, y, x + cell_w - 10, y + cell_h - 12], fill=(255, 255, 255),
                    outline=(224, 228, 234))
        big = frames[16].resize((128, 128), Image.NEAREST)
        sheet.paste(big, (x + (cell_w - 10 - 128) // 2, y + (cell_h - 12 - 128) // 2 - 6))
        d.text((x + 8, y + cell_h - 32), "16px x8", fill=(120, 128, 138))

        bar_y = y + cell_h
        d.rectangle([pad, bar_y, pad + label_w + cell_w * 6 - 12, bar_y + dark_h - 12],
                    fill=(32, 32, 32))
        x = pad + 14
        for size in (32, 24, 16):
            image = frames[size]
            sheet.paste(image, (x, bar_y + (dark_h - 12 - size) // 2), image)
            x += 48
        d.text((x + 10, bar_y + dark_h // 2 - 18), "on a dark taskbar (32 / 24 / 16)",
               fill=(176, 184, 196))

        y += cell_h + dark_h

    os.makedirs(os.path.dirname(path), exist_ok=True)
    sheet.save(path)
    print(f"candidate sheet written: {path}  {sheet.size}")


def size_note(letter: str) -> str:
    return {
        "A": "blue tile + frame",
        "B": "flash on the corner",
        "C": "corner brackets (v1+)",
        "D": "solid photo frame",
    }[letter]


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--design", default="B", choices=sorted(DESIGNS))
    parser.add_argument("--sheet", action="store_true", help="write the candidate sheet only")
    parser.add_argument("--out", default=os.path.join(DOCS, "icon-candidates.png"))
    args = parser.parse_args()

    if args.sheet:
        write_sheet(args.out)
        return

    write_assets(args.design)


if __name__ == "__main__":
    main()

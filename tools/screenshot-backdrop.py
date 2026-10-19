#!/usr/bin/env python3
"""
Generates a synthetic "developer desktop" backdrop used when producing the README
screenshots. Using generated content means the published screenshots never leak whatever
happens to be on the real desktop.

    py -3.11 tools/screenshot-backdrop.py --width 1920 --height 1080 --out backdrop.png
"""

from __future__ import annotations

import argparse
from PIL import Image, ImageDraw, ImageFont

FONTS = {
    "mono": r"C:\Windows\Fonts\consola.ttf",
    "mono_bold": r"C:\Windows\Fonts\consolab.ttf",
    "ui": r"C:\Windows\Fonts\segoeui.ttf",
    "ui_bold": r"C:\Windows\Fonts\seguisb.ttf",
}

BG_TOP = (16, 20, 26)
BG_BOTTOM = (26, 34, 46)
PANEL = (22, 25, 31)
PANEL_HEAD = (31, 35, 43)
BORDER = (48, 55, 68)
TEXT = (222, 228, 236)
DIM = (128, 140, 156)
ACCENT = (91, 174, 255)
AMBER = (255, 193, 45)
GREEN = (126, 216, 138)
PURPLE = (198, 158, 255)
TEAL = (110, 214, 214)

CODE_LINES = [
    ("/// <summary>Floating snapshot: drag to move, three tiny buttons.</summary>", "comment"),
    ("public sealed partial class SnapshotWindow : Window", "type"),
    ("{", "plain"),
    ("    private readonly Snapshot _snapshot;", "plain"),
    ("    private bool _collapsed;", "plain"),
    ("", "plain"),
    ("    internal void ToggleCollapse()", "keyword"),
    ("    {", "plain"),
    ("        var (x, y) = CurrentPhysicalOrigin();", "plain"),
    ("        _collapsed = !_collapsed;", "plain"),
    ("        _snapshot.IsCollapsed = _collapsed;", "plain"),
    ("", "plain"),
    ("        ComputeDisplaySize();      // thumbnail or 1:1", "comment"),
    ("        UpdateCollapseVisual();", "plain"),
    ("        UpdateLayout();", "plain"),
    ("        RestorePhysicalOrigin(x, y);", "plain"),
    ("    }", "plain"),
    ("", "plain"),
    ("    private void OnMouseMove(object sender, MouseEventArgs e)", "type"),
    ("    {", "plain"),
    ("        var position = e.GetPosition(this);", "plain"),
    ("        UpdateAiming(position);   // guides + crosshair", "comment"),
    ("        UpdateLoupe(position);", "plain"),
    ("    }", "plain"),
    ("}", "plain"),
    ("", "plain"),
    ("// Win+Shift+Q  ->  register once, never lose the combo", "comment"),
    ("RegisterHotKey(hwnd, HotkeyId, MOD_WIN | MOD_SHIFT | MOD_NOREPEAT, 0x51);", "plain"),
]

COLORS = {
    "comment": (110, 156, 122),
    "type": (86, 182, 240),
    "keyword": (198, 158, 255),
    "plain": TEXT,
}

TREE = [
    ("QuickShot", 0, True),
    ("Interop", 1, True),
    ("NativeMethods.cs", 2, False),
    ("DpiHelper.cs", 2, False),
    ("Services", 1, True),
    ("HotkeyService.cs", 2, False),
    ("SnapshotManager.cs", 2, False),
    ("Views", 1, True),
    ("OverlayWindow.xaml", 2, False),
    ("SnapshotWindow.xaml", 2, False),
    ("README.md", 0, False),
]

TERMINAL = [
    "[09:41:02] Hotkey registered: Win+Shift+Q",
    "[09:41:07] Snapshot #1 floated (560x400, 1/20)",
    "[09:41:12] Snapshot #2 floated (420x260, 2/20)",
    "[09:41:18] Snapshot #3 floated (480x300, 3/20)",
    "[09:41:25] All snapshots collapsed",
    "[09:41:31] All snapshots restored",
    "PS D:\\QuickShot> .\\build\\publish.ps1",
    "published: build\\publish\\QuickShot.exe (62.9 MB)",
    "installer: QuickShot-Setup-1.0.0.exe (58.0 MB)",
]


def font(kind: str, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(FONTS[kind], size)


def gradient(size: tuple[int, int]) -> Image.Image:
    width, height = size
    strip = Image.new("RGB", (1, height))
    px = strip.load()
    for y in range(height):
        t = y / max(1, height - 1)
        px[0, y] = tuple(int(BG_TOP[i] + (BG_BOTTOM[i] - BG_TOP[i]) * t) for i in range(3))
    return strip.resize(size, Image.BILINEAR).convert("RGBA")


def window_card(img: Image.Image, box, title: str, subtitle: str = "") -> None:
    d = ImageDraw.Draw(img)
    x0, y0, x1, y1 = box
    d.rounded_rectangle([x0 + 3, y0 + 5, x1 + 3, y1 + 5], radius=12, fill=(0, 0, 0, 90))
    d.rounded_rectangle(box, radius=12, fill=PANEL, outline=BORDER, width=1)
    d.rounded_rectangle([x0, y0, x1, y0 + 38], radius=12, fill=PANEL_HEAD)
    d.rectangle([x0, y0 + 26, x1, y0 + 38], fill=PANEL_HEAD)
    for i, colour in enumerate(((255, 95, 86), (255, 189, 46), (39, 201, 63))):
        cx = x0 + 22 + i * 20
        d.ellipse([cx - 6, y0 + 13, cx + 6, y0 + 25], fill=colour)
    d.text((x0 + 92, y0 + 11), title, font=font("ui", 15), fill=TEXT)
    if subtitle:
        d.text((x1 - 20 - d.textlength(subtitle, font=font("ui", 13)), y0 + 13),
               subtitle, font=font("ui", 13), fill=DIM)


def draw_editor(img: Image.Image, box) -> None:
    window_card(img, box, "SnapshotWindow.xaml.cs — QuickShot", "src/QuickShot/Views")
    d = ImageDraw.Draw(img)
    x0, y0, x1, y1 = box
    sidebar_right = x0 + 236

    d.rectangle([x0 + 1, y0 + 39, sidebar_right, y1 - 1], fill=(19, 22, 28))
    tree_font = font("ui", 14)
    y = y0 + 56
    for name, depth, is_folder in TREE:
        colour = ACCENT if depth == 0 else (TEXT if not is_folder else DIM)
        icon = "▾" if is_folder else "·"
        d.text((x0 + 18 + depth * 16, y), f"{icon} {name}", font=tree_font, fill=colour)
        y += 24

    code_font = font("mono", 15)
    code_x = sidebar_right + 64
    y = y0 + 56
    for index, (line, kind) in enumerate(CODE_LINES, start=1):
        d.text((sidebar_right + 18, y), f"{index:>3}", font=code_font, fill=(72, 82, 96))
        # crude token colouring: paint the first word in the token colour
        if kind == "plain" and line.strip():
            head = line.split(" ")[0]
            d.text((code_x, y), line, font=code_font, fill=TEXT)
            keyword = head.split("(")[0]
            if keyword and keyword.strip(".{}") in {
                "public", "private", "internal", "var", "return", "void", "new", "if",
            }:
                width = d.textlength(line[:len(line) - len(line.lstrip())] + head,
                                     font=code_font)
                d.rectangle([code_x + 2, y - 1, code_x + width + 2, y + 19], fill=PANEL)
                d.text((code_x, y), line, font=code_font, fill=TEXT)
                d.text((code_x, y), head, font=code_font, fill=PURPLE)
        else:
            d.text((code_x, y), line, font=code_font, fill=COLORS[kind])
        y += 24

    d.line([x0 + 900, y0 + 39, x0 + 900, y1], fill=(30, 34, 41))

    # an outline panel, so the backdrop reads as ordinary editor content
    card = (x1 - 300, y1 - 160, x1 - 16, y1 - 16)
    d.rounded_rectangle(card, radius=8, fill=(19, 22, 28), outline=BORDER, width=1)
    d.text((card[0] + 14, card[1] + 10), "Outline", font=font("ui_bold", 13), fill=DIM)
    outline = [
        ("▸ SnapshotWindow", TEXT),
        ("    ToggleCollapse()", (176, 186, 198)),
        ("    SetCollapsed(bool)", (176, 186, 198)),
        ("    ComputeDisplaySize()", (176, 186, 198)),
        ("    ApplyTopmost()", (176, 186, 198)),
        ("▸ OverlayWindow", TEXT),
        ("    UpdateAiming(Point)", (176, 186, 198)),
        ("    UpdateLoupe(Point)", (176, 186, 198)),
    ]
    y = card[1] + 34
    for text_line, colour in outline:
        d.text((card[0] + 14, y), text_line, font=font("mono", 12), fill=colour)
        y += 17


def draw_chart(img: Image.Image, box) -> None:
    window_card(img, box, "Capture size over time", "statistics")
    d = ImageDraw.Draw(img)
    x0, y0, x1, y1 = box
    plot = (x0 + 34, y0 + 66, x1 - 26, y1 - 44)
    d.rounded_rectangle([plot[0], plot[1], plot[2], plot[3]], radius=6, fill=(17, 20, 25))
    for i in range(1, 5):
        y = plot[1] + (plot[3] - plot[1]) * i / 5
        d.line([plot[0], y, plot[2], y], fill=(34, 39, 48))

    values = [0.18, 0.32, 0.24, 0.46, 0.38, 0.62, 0.55, 0.71, 0.66, 0.83, 0.78, 0.92]
    points = []
    for i, value in enumerate(values):
        x = plot[0] + 14 + (plot[2] - plot[0] - 28) * i / (len(values) - 1)
        y = plot[3] - 16 - (plot[3] - plot[1] - 32) * value
        points.append((x, y))
    d.line(points, fill=ACCENT, width=3, joint="curve")
    for x, y in points:
        d.ellipse([x - 4, y - 4, x + 4, y + 4], fill=(17, 20, 25), outline=ACCENT, width=2)
    d.text((plot[0], plot[3] + 12), "1  2  3  4  5  6  7  8  9 10 11 12  captures",
           font=font("ui", 12), fill=DIM)


def draw_terminal(img: Image.Image, box) -> None:
    window_card(img, box, "QuickShot — dev log", "quickshot.log")
    d = ImageDraw.Draw(img)
    x0, y0, x1, y1 = box
    y = y0 + 56
    line_font = font("mono", 13)
    for line in TERMINAL:
        colour = TEXT
        if "[INFO" in line:
            colour = (150, 200, 255)
        elif line.startswith("PS "):
            colour = GREEN
        if "collapsed" in line or "restored" in line:
            colour = AMBER
        d.text((x0 + 18, y), line, font=line_font, fill=colour)
        y += 22


def draw_taskbar(img: Image.Image, width: int, height: int) -> None:
    d = ImageDraw.Draw(img)
    bar_top = height - 46
    d.rectangle([0, bar_top, width, height], fill=(14, 17, 22))
    d.line([0, bar_top, width, bar_top], fill=(38, 44, 54))
    colours = [(91, 174, 255), (255, 193, 45), (126, 216, 138), (198, 158, 255), (240, 240, 245)]
    x = 16
    for colour in colours:
        d.rounded_rectangle([x, bar_top + 11, x + 24, bar_top + 35], radius=6, fill=colour)
        x += 34
    d.text((width - 116, bar_top + 14), "09:41", font=font("ui", 14), fill=TEXT)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--width", type=int, default=1920)
    parser.add_argument("--height", type=int, default=1080)
    parser.add_argument("--out", required=True)
    args = parser.parse_args()

    img = gradient((args.width, args.height))
    width, height = img.size
    scale = width / 1920.0

    draw_editor(img, (40, 44, int(1150 * scale), int(900 * scale)))
    draw_chart(img, (int(1180 * scale), 44, width - 40, int(500 * scale)))
    draw_terminal(img, (int(1180 * scale), int(530 * scale), width - 40, int(900 * scale)))
    draw_taskbar(img, width, height)

    img.convert("RGB").save(args.out)
    print(f"backdrop written: {args.out} {img.size}")


if __name__ == "__main__":
    main()

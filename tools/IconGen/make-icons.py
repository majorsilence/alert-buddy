#!/usr/bin/env python3
"""Draws the Alert Buddy app icon (Beacon Buddy: dome, lamp, face and base on Blueberry) and writes every size the heads need.

Run from the repo root:  python3 tools/IconGen/make-icons.py
Needs Pillow. Colours are AlertPalette's; keep them in step. The art is drawn once at 1024 and scaled down, so every size is the
same picture. The foreground layer keeps its content inside the middle ~60%, which is what an Android adaptive icon never crops.
"""
import io
import math
import os
from PIL import Image, ImageDraw

BLUEBERRY = (0x23, 0x2E, 0x7A)
PAPER = (0xF6, 0xF2, 0xFF)
GRAPE = (0x2B, 0x1B, 0x4D)
BUTTER = (0xFF, 0xD8, 0x4D)
TANGERINE = (0xFF, 0x8A, 0x3D)

S = 1024
SS = 4  # supersample
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))


def px(v):
    return int(round(v * SS))


def ellipse(d, cx, cy, r, fill, outline=None, width=0):
    d.ellipse([px(cx - r), px(cy - r), px(cx + r), px(cy + r)], fill=fill, outline=outline, width=px(width))


def content(with_beams=True):
    """The buddy on a transparent layer, centred on the canvas."""
    im = Image.new("RGBA", (S * SS, S * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)

    dome_r = 215
    cx, cy = 512, 600
    lamp_r = 75
    lamp_cy = cy - dome_r * 0.95

    if with_beams:
        # Three short rays off the lamp: the "something needs a look" mark, drawn in Butter so it is a light, not a state colour.
        for deg in (-90, -135, -45):
            a = math.radians(deg)
            r0, r1 = lamp_r + 38, lamp_r + 105
            d.line([px(cx + r0 * math.cos(a)), px(lamp_cy + r0 * math.sin(a)), px(cx + r1 * math.cos(a)), px(lamp_cy + r1 * math.sin(a))],
                   fill=BUTTER, width=px(30))
            for r in (r0, r1):
                ellipse(d, cx + r * math.cos(a), lamp_cy + r * math.sin(a), 15, BUTTER)

    # base
    base = [px(cx - dome_r * 0.7), px(cy + dome_r * 0.55), px(cx + dome_r * 0.7), px(cy + dome_r * 1.0)]
    d.rounded_rectangle(base, radius=px(dome_r * 0.14), fill=GRAPE)

    # dome
    ellipse(d, cx, cy, dome_r, PAPER, GRAPE, 12)

    # lamp
    ellipse(d, cx, lamp_cy, lamp_r, BUTTER, GRAPE, 10)
    ellipse(d, cx - lamp_r * 0.3, lamp_cy - lamp_r * 0.3, lamp_r * 0.22, (255, 245, 190))

    # cheeks first, so the face sits on top
    for dx in (-dome_r * 0.66, dome_r * 0.66):
        ellipse(d, cx + dx, cy + dome_r * 0.12, dome_r * 0.1, (255, 205, 190))

    # face: watching, a gentle smile
    eye_y = cy - dome_r * 0.12
    for dx in (-dome_r * 0.4, dome_r * 0.4):
        ellipse(d, cx + dx, eye_y, dome_r * 0.12, GRAPE)
        ellipse(d, cx + dx + dome_r * 0.04, eye_y - dome_r * 0.045, dome_r * 0.04, PAPER)
    mr = dome_r * 0.3
    my = cy + dome_r * 0.05
    lw = 14
    d.arc([px(cx - mr), px(my - mr), px(cx + mr), px(my + mr)], 30, 150, fill=GRAPE, width=px(lw))
    for ang in (30, 150):
        ellipse(d, cx + (mr - lw / 2) * math.cos(math.radians(ang)), my + (mr - lw / 2) * math.sin(math.radians(ang)), lw / 2, GRAPE)
    return im


def down(im, size):
    return im.resize((size, size), Image.LANCZOS)


def squircle_mask(size, radius_frac=0.2237):
    m = Image.new("L", (size * SS, size * SS), 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, size * SS - 1, size * SS - 1], radius=int(size * SS * radius_frac), fill=255)
    return m.resize((size, size), Image.LANCZOS)


def save(im, *parts):
    path = os.path.join(ROOT, *parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path, optimize=True)
    print("wrote", os.path.relpath(path, ROOT))


def main():
    art = content()
    flat = Image.new("RGBA", art.size, BLUEBERRY + (255,))
    full = Image.alpha_composite(flat, art)  # full-bleed square: iOS and the adaptive background baked in
    master = down(full, S)

    # Source art, and iOS (the system rounds the corners; the App Store wants no alpha).
    save(master.convert("RGB"), "assets", "icon", "icon-1024.png")
    save(master.convert("RGB"), "assets", "icon", "ios", "AppIcon-1024.png")
    save(down(art, S), "assets", "icon", "icon-foreground-1024.png")
    for size in (180, 167, 152, 120, 87, 80, 76, 60, 58, 40, 29, 20):
        save(down(full, size).convert("RGB"), "assets", "icon", "ios", f"AppIcon-{size}.png")

    # Desktop: a rounded tile on transparent, since a window or taskbar does not round it for us.
    tile = down(full, 512)
    tile.putalpha(squircle_mask(512))
    save(tile.resize((256, 256), Image.LANCZOS), "src", "AlertBuddy.Desktop", "Assets", "icon-256.png")
    # A multi-size .ico for the Windows exe.
    ico_path = os.path.join(ROOT, "src", "AlertBuddy.Desktop", "AlertBuddy.ico")
    tile.save(ico_path, sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)])
    print("wrote", os.path.relpath(ico_path, ROOT))

    # Android: adaptive foreground is 108dp, with the background a flat colour (see ic_launcher_background.xml);
    # the legacy launcher icon is 48dp, a rounded tile, for API 24 and 25.
    res = os.path.join("src", "AlertBuddy.Android", "Resources")
    for name, scale in (("mdpi", 1), ("hdpi", 1.5), ("xhdpi", 2), ("xxhdpi", 3), ("xxxhdpi", 4)):
        save(down(art, int(108 * scale)), res, f"mipmap-{name}", "ic_launcher_foreground.png")
        legacy = down(full, int(48 * scale))
        legacy.putalpha(squircle_mask(int(48 * scale), 0.18))
        save(legacy, res, f"mipmap-{name}", "ic_launcher.png")
        round_ = down(full, int(48 * scale))
        m = Image.new("L", (int(48 * scale) * SS,) * 2, 0)
        ImageDraw.Draw(m).ellipse([0, 0, m.width - 1, m.height - 1], fill=255)
        round_.putalpha(m.resize((int(48 * scale),) * 2, Image.LANCZOS))
        save(round_, res, f"mipmap-{name}", "ic_launcher_round.png")
    save(down(full, 512).convert("RGB"), "assets", "icon", "play-store-512.png")


if __name__ == "__main__":
    main()

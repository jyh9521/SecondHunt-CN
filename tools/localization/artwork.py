"""Build sparse, opacity-only lettering stencils and an OFL glyph atlas.

The input PNGs are local exports from the user's own game data. No RGB pixels
from those exports are written to the locale resources.
"""
import argparse
import base64
import hashlib
import json
import struct
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont


def image(path, mode):
    with Image.open(path) as im:
        return np.asarray(im.convert(mode)).copy()


def cropped(data):
    ys, xs = np.nonzero(data)
    if not len(xs):
        raise ValueError("empty lettering stencil")
    x, y, r, b = int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1
    return dict(x=x, y=y, width=r-x, height=b-y,
                data=base64.b64encode(data[y:b, x:r].astype(np.uint8).tobytes()).decode("ascii"))


def restrict(a, box):
    x, y, right, bottom = box
    out = np.zeros_like(a)
    out[y:bottom, x:right] = a[y:bottom, x:right]
    return out


def dilate(a, radius):
    return np.array(Image.fromarray(a.astype(np.uint8) * 255)
                    .filter(ImageFilter.MaxFilter(2*radius+1))) > 0


def shift(a, dx, dy):
    out = np.zeros_like(a)
    h, w = a.shape
    out[dy:, dx:] = a[:h-dy, :w-dx]
    return out


def feather(a, radii, opacity):
    return np.select([dilate(a, r) if r else a for r in radii], opacity, 0).astype(np.uint8)


def remove_flecks(a, minimum):
    remaining = a.copy()
    for y, x in zip(*np.nonzero(a)):
        if not remaining[y, x]:
            continue
        block = [(y, x)]
        remaining[y, x] = False
        for yy, xx in block:
            for r in range(max(0, yy-1), min(a.shape[0], yy+2)):
                for c in range(max(0, xx-1), min(a.shape[1], xx+2)):
                    if remaining[r, c]:
                        remaining[r, c] = False
                        block.append((r, c))
        if len(block) < minimum:
            for r, c in block:
                a[r, c] = False
    return a


def read_frame(root, screen, n):
    return image(root / screen / f"{n:04d}.png", "RGB").astype(np.int16)


def film_stencils(root, recipes, shared):
    # Some cards use the same lettering with a different tint. Read the
    # best-separated instance and share only its coverage stencil.
    sample = read_frame(root, shared["stream"], shared["frame"])
    chroma = (sample * np.array(shared["channels"])).sum(axis=2)
    colors = chroma > shared["threshold"]
    labels = np.zeros(colors.shape, dtype=bool)
    for box in shared["boxes"]:
        labels |= restrict(colors, box)
    result = {}
    for card in recipes:
        stem, stream = card["key"], card["stream"]
        plate = read_frame(root, stream, card["namePlate"])
        before, after = card["flashSamples"]
        flash = np.max([read_frame(root, stream, n).min(axis=2)
                        for n in range(before, after+1)], axis=0)
        core = restrict((flash > 222) & ((flash-plate.min(axis=2) > 12) | (flash > 243)), card["nameBox"])
        core = remove_flecks(core, 17)
        name = feather(core | shift(core, 2, 4), (2, 3, 4), (255, 173, 64))
        glow = feather(core, (5, 7, 9), (255, 155, 49))
        result[stem+"-name"] = cropped(name)
        result[stem+"-flash"] = cropped(np.maximum(name, glow))

        steady = read_frame(root, stream, card["steady"])
        typing = read_frame(root, stream, card["valueReference"])
        delta = np.max(np.abs(steady-typing), axis=2)
        small = shift(labels, card["labelShift"], 0)
        for box in card["values"]:
            # The new typewritten letters are isolated by temporal subtraction;
            # subtract low-level VX decoder noise before expanding the soft edge.
            small |= restrict(delta > card.get("valueThreshold", 11), box)
        for frame, box in card.get("valueSamples", []):
            # Some letters flash white as they finish typing. The settled
            # colour alone can be close to the picture under those strokes.
            moment = read_frame(root, stream, frame)
            small |= restrict(np.max(np.abs(moment - typing), axis=2) > card.get("valueThreshold", 11), box)
        for x, y, r, b in card.get("earlyValueBoxes", []):
            # First letters already visible at valueReference: trace their
            # supports independently, avoiding the gaps between the two lines.
            small[y:b, x:r] = True
        if card.get("sharedChroma"):
            for box in card["values"]:
                small |= restrict(colors, box)
        small = remove_flecks(small, 2)
        result[stem+"-details"] = cropped(feather(small, (0, 1, 2), (255, 225, 70)))
    return result


def texture_stencil(rgba, rule):
    rgb, alpha = rgba[:, :, :3].astype(np.int16), rgba[:, :, 3]
    if rule["method"] == "alpha":
        sel = alpha > 0
    elif rule["method"] == "white":
        sel = (rgb < 248).any(axis=2)
    elif rule["method"] == "black":
        sel = (rgb > 1).any(axis=2)
    elif rule["method"] == "title":
        grey = (rgb.min(axis=2) > 85) & (rgb.max(axis=2) - rgb.min(axis=2) < 38) & (alpha > 0)
        chrome = np.zeros(alpha.shape, dtype=bool)
        for box in rule["chromeBoxes"]:
            chrome |= restrict(grey, box)
        chrome = (dilate(chrome, 1) | shift(dilate(chrome, 2), 1, 2)) & (alpha > 0)
        red, green, blue = np.moveaxis(rgb, -1, 0)
        orange = restrict((red > 70) & (red-green > 24) & (green-blue > 9) & (alpha > 0),
                          rule["orangeBox"])
        orange = dilate(orange, 2) & (alpha > 0)
        black = np.zeros(alpha.shape, dtype=bool)
        for x, y, r, b in rule["orangeInk"]:
            # Recover the ring behind the solid faces of the bottom word, too.
            black[y:b, x:r] |= (red[y:b, x:r] < 48) & (alpha[y:b, x:r] > 0)
        cx, cy, rx, ry = rule["clearOutside"]
        y, x = np.indices(alpha.shape)
        outside = ((x-cx) / rx)**2 + ((y-cy) / ry)**2 > 1
        # All remaining non-transparent shadow pixels outside the medallion
        # are lettering, apart from the separate registered-mark glyph.
        rim_shadows = restrict(outside & (alpha > 0), rule["shadowBox"])
        mark = rule["registeredMark"]
        registered = restrict((rgb == mark["rgb"]).all(axis=2) & (alpha > 0), mark["box"])
        if np.count_nonzero(registered) != mark["pixels"]:
            raise ValueError("title registered mark does not match the verified source")
        # Keep the entire original glyph after all dilation, without retaining
        # the adjacent strokes and shadows of the English title's final D.
        return cropped(((chrome | orange | black | rim_shadows) & ~registered).astype(np.uint8) * 255)
    else:
        raise ValueError(rule["method"])
    area = np.zeros_like(sel)
    for box in rule["boxes"]:
        area |= restrict(sel, box)
    return cropped(area.astype(np.uint8) * 255)


def write_atlas(font_path, layout, output):
    size = 48
    font = ImageFont.truetype(str(font_path), 112)
    texts = [s["text"] for t in layout["textures"] for s in t["lettering"]]
    texts += [s["text"] for m in layout["movies"] for cue in m["cues"] for s in cue["lettering"]]
    chars = sorted(set("".join(texts)) - set(" \n") | {"□"})
    out = bytearray(b"SHA1")
    for char in chars:
        tile = Image.new("L", (128, 128))
        ImageDraw.Draw(tile).text((64, 98), char, font=font, anchor="ms", fill=255)
        out.extend(struct.pack("<H", ord(char)))
        out.extend(tile.resize((size, size), Image.Resampling.LANCZOS).tobytes())
    output.write_bytes(out)
    print(f"{len(chars)} art glyphs at {size} px ({len(out)} bytes)")


def main():
    p = argparse.ArgumentParser(description=__doc__)
    for name in ["source", "layout", "frames", "textures", "font", "masks", "atlas"]:
        p.add_argument(name, type=Path)
    a = p.parse_args()
    source = json.loads(a.source.read_text(encoding="utf-8"))
    layout = json.loads(a.layout.read_text(encoding="utf-8"))
    masks = film_stencils(a.frames, source["films"], source["sharedLabels"])
    manifest = json.loads((a.textures/"textures.json").read_text(encoding="utf-8"))
    def checked_texture(t, expected):
        native = next(m for m in manifest if m["Model"].lower().replace("\\", "/") == t["model"]
                      and m["Texture"] == t["id"] and m["Palette"] == t["palette"])
        rgba = image(a.textures/native["File"], "RGBA")
        actual = hashlib.sha256(rgba.tobytes()).hexdigest()
        if actual != expected:
            raise ValueError(f"{t['model']}: decoded texture SHA256 {actual} != {expected}")
        return rgba

    for t in layout["textures"]:
        rgba = checked_texture(t, t["sha256"])
        for alias in t.get("aliases", []):
            checked_texture(alias, t["sha256"])
        masks[t["mask"]] = texture_stencil(rgba, source["textures"][t["mask"]])
    a.masks.write_text(json.dumps(masks, ensure_ascii=True, separators=(",", ":"))+"\n", encoding="ascii")
    print(f"{len(masks)} opacity-only stencils ({a.masks.stat().st_size} bytes)")
    write_atlas(a.font, layout, a.atlas)


if __name__ == "__main__":
    main()

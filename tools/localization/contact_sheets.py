"""Make review sheets from MediaInventory previews; outputs are local review aids."""

import argparse
from pathlib import Path
from PIL import Image, ImageDraw


def sheets(entries, output, label, columns, rows, width, height):
    page_size = columns * rows
    for start in range(0, len(entries), page_size):
        batch = entries[start:start + page_size]
        sheet = Image.new("RGB", (width * columns, (height + 18) * ((len(batch) + columns - 1) // columns)), "#202730")
        draw = ImageDraw.Draw(sheet)
        for offset, path in enumerate(batch):
            x = offset % columns * width
            y = offset // columns * (height + 18)
            with Image.open(path) as source:
                preview = source.convert("RGBA")
                # Preserve native pixels and enlarge small tiles for inspection.
                scale = min(width / preview.width, height / preview.height)
                size = max(1, int(preview.width * scale)), max(1, int(preview.height * scale))
                preview = preview.resize(size, Image.Resampling.NEAREST)
                sheet.paste(preview, (x + (width - size[0]) // 2, y + (height - size[1]) // 2), preview)
            draw.text((x + 3, y + height + 2), label(path), fill="#e3e7eb")
        sheet.save(output.with_name(f"{output.stem}-{1 + start // page_size:02d}.png"))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("inventory", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    sheets(sorted((args.inventory / "textures").glob("*.png")), args.output / "textures",
           lambda p: p.stem, 8, 7, 142, 98)
    folders = sorted(p for p in (args.inventory / "movies").iterdir() if p.is_dir())
    for name in sorted({p.name[:2] for p in folders}):
        match = [p for p in folders if p.name.startswith(name)]
        files = sorted((f for p in match for f in p.glob("*.png")), key=lambda p: (p.stem, not p.parent.name.endswith("_top")))
        sheets(files, args.output / f"movie-{name}", lambda p: f"{p.parent.name} / {p.stem}", 8, 8, 160, 120)


if __name__ == "__main__":
    main()

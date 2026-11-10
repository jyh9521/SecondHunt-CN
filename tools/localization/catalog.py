"""Local ROM catalogue, overlay compiler, and deterministic glyph generator.

The source catalogue is private build input. Only the translation and source
fingerprints go into the pack; ROM data and the original font are never emitted.
"""
import argparse
import hashlib
import json
import re
import struct
from pathlib import Path

TABLES = (
    "GameMessages", "HudMessagesMP", "HudMessagesSP", "HudMsgsCommon",
    "LocationNames", "MBBanner", "ScanLog", "ShipInSpace", "ShipOnGround", "WeaponNames",
)


def sha(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def save(path, doc):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(doc, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


def rom_files(rom):
    data = rom.read_bytes()
    if len(data) < 0x50 or data[12:16] != b"AMHE" or data[30] not in (0, 1):
        raise ValueError("expected a Metroid Prime Hunters USA ROM (AMHE rev 0 or 1)")
    fnt, fnt_len, fat, fat_len = struct.unpack_from("<4I", data, 0x40)
    if fnt + fnt_len > len(data) or fat + fat_len > len(data) or fat_len % 8:
        raise ValueError("invalid filesystem tables")
    files, visited = {}, set()

    def walk(index, prefix):
        if index < 0 or index in visited or (index + 1) * 8 > fnt_len:
            raise ValueError("invalid directory index")
        visited.add(index)
        offset, first, _ = struct.unpack_from("<IHH", data, fnt + index * 8)
        i, end = fnt + offset, fnt + fnt_len
        while i < end and data[i]:
            kind = data[i]
            size = kind & 127
            i += 1
            if not size or i + size + (2 if kind & 128 else 0) > end:
                raise ValueError("invalid file name length")
            name = prefix + data[i:i+size].decode("ascii")
            i += size
            if kind & 128:
                child, = struct.unpack_from("<H", data, i)
                walk(child - 0xF000, name + "/")
                i += 2
            else:
                if (first + 1) * 8 > fat_len:
                    raise ValueError("invalid file index")
                start, stop = struct.unpack_from("<II", data, fat + first * 8)
                first += 1
                if not start <= stop <= len(data):
                    raise ValueError("invalid file extent")
                files[name] = (start, stop)
        if i == end:
            raise ValueError("unterminated directory")

    walk(0, "")
    return data[30], {name: data[start:stop] for name, (start, stop) in files.items()
                      if name.startswith("stringTables/") or name == "frontend/metroidhunters_text_en.bin"}


def cstring(data, offset, length):
    if offset + length > len(data):
        raise ValueError("string out of range")
    return data[offset:offset+length].split(b"\0", 1)[0].decode("latin1")


def menu(data):
    result = []
    for i in range(0, len(data) - 3, 4):
        entry, = struct.unpack_from("<I", data, i)
        if entry == 0:
            return result
        off, copy, size, size_copy = struct.unpack_from("<IIHH", data, entry)
        if (off, size) != (copy, size_copy):
            raise ValueError("mismatched menu offsets")
        result.append(cstring(data, off, size))
    raise ValueError("unterminated menu offset table")


def table(name, data):
    count, = struct.unpack_from("<I", data)
    header = 8 if name == "ScanLog" else 4
    result = {}
    for i in range(count):
        key, off, size, speed, category = struct.unpack_from("<4sIHBB", data, header + 12*i)
        key = key[::-1].decode("ascii")
        text = cstring(data, off, size).replace("$", "")
        prefix = ""
        if name == "GameMessages" and text:
            prefix, text = text[0], text[1:]
        first, second = text.split("\\") if text.count("\\") == 1 else (text, "")
        if key in result:
            raise ValueError(f"duplicate ID {name}:{key}")
        result[key] = {"first": first, "second": second, "prefix": prefix,
                       "speed": speed, "category": chr(category)}
    return result


def export(args):
    rev, files = rom_files(args.rom)
    source = {"revision": rev, "menu": menu(files["frontend/metroidhunters_text_en.bin"]),
              "tables": {name+".bin": table(name, files["stringTables/"+name+".bin"]) for name in TABLES}}
    save(args.output, source)
    print(f"AMHE{rev}: {len(source['menu'])} menu indices, "
          f"{sum(map(len, source['tables'].values()))} table entries in {len(TABLES)} tables")


TOKENS = re.compile(r"%(?:[0-9]+\$)?[sdif%]|&tab\d+")


def validate(original, translation):
    if not isinstance(translation, str) or not translation.strip():
        raise ValueError("expected non-empty translated text")
    if TOKENS.findall(original) != TOKENS.findall(translation):
        raise ValueError("substitution fields do not match")
    if any(ord(c) < 32 and c != "\n" or 127 <= ord(c) <= 255 or ord(c) > 0xffff for c in translation):
        raise ValueError("unsupported control or legacy-byte characters in translated text")


def compile_pack(args):
    source, edit = load(args.source), load(args.translation)
    by_hash = {}
    for key, text in edit["menu"].items():
        i = int(key)
        if not 0 <= i < len(source["menu"]):
            raise ValueError(f"invalid menu index: {key}")
        original = source["menu"][i]
        if original and text:
            validate(original, text)
            h = sha(original)
            if h in by_hash and by_hash[h] != text:
                raise ValueError(f"conflicting menu translations for identical source at {key}")
            by_hash[h] = text
    tables = {}
    for name, entries in edit["tables"].items():
        out = tables[name] = {}
        for key, fields in entries.items():
            row = source["tables"][name][key]
            if not fields or not fields.keys() <= {"first", "second"}:
                raise ValueError(f"invalid translation fields: {name}:{key}")
            for field, text in fields.items():
                validate(row[field], text)
            out[key] = {"source": sha(row["first"]+"\0"+row["second"]), **fields}
    save(args.output, {"version": 1, "locale": args.locale, "menu": by_hash, "tables": tables})
    print(f"{len(by_hash)} unique menu strings, {sum(map(len, tables.values()))} table entries")


def unpack(args):
    source, pack = load(args.source), load(args.pack)
    menu_text = {str(i): pack["menu"][sha(text)] for i, text in enumerate(source["menu"])
                 if sha(text) in pack["menu"]}
    tables = {}
    for name, rows in source["tables"].items():
        tables[name] = {}
        for key, row in rows.items():
            translated = pack["tables"].get(name, {}).get(key, {})
            if translated.get("source") == sha(row["first"]+"\0"+row["second"]):
                tables[name][key] = {field: v for field, v in translated.items() if field != "source"}
    save(args.output, {"menu": menu_text, "tables": tables})
    print(f"{len(menu_text)} matched menu indices, {sum(map(len, tables.values()))} matched table entries")


def glyphs(args):
    from PIL import Image, ImageDraw, ImageFont
    from fontTools.ttLib import TTFont

    font = ImageFont.truetype(str(args.font), 64)
    cmap = TTFont(args.font).getBestCmap()
    ui, rom = load(args.ui), load(args.pack)
    texts = list(ui.values()) + list(rom["menu"].values())
    texts += [v for tab in rom["tables"].values() for entry in tab.values()
              for k, v in entry.items() if k != "source"]
    chars = sorted({c for text in texts for c in text if ord(c) > 127} | {"□"})
    if any(ord(c) > 0xffff or ord(c) not in cmap for c in chars):
        raise ValueError("unavailable glyphs: " + "".join(c for c in chars if ord(c) > 0xffff or ord(c) not in cmap))
    output = bytearray(b"SHG1")
    for c in chars:
        image = Image.new("L", (72, 72))
        ImageDraw.Draw(image).text((36, 56), c, font=font, fill=255, anchor="ms")
        output.extend(struct.pack("<H", ord(c)))
        output.extend(image.resize((16, 16), Image.Resampling.LANCZOS).tobytes())
    args.output.write_bytes(output)
    print(f"{len(chars)} glyphs ({len(output)} bytes)")


def main():
    parser = argparse.ArgumentParser()
    cmds = parser.add_subparsers(dest="cmd", required=True)
    for name, handler, paths in (
        ("export", export, ("rom", "output")),
        ("pack", compile_pack, ("source", "translation", "output")),
        ("unpack", unpack, ("source", "pack", "output")),
        ("glyphs", glyphs, ("font", "ui", "pack", "output")),
    ):
        cmd = cmds.add_parser(name)
        for path in paths:
            cmd.add_argument(path, type=Path)
        if name == "pack":
            cmd.add_argument("--locale", required=True)
        cmd.set_defaults(handler=handler)
    args = parser.parse_args()
    args.handler(args)


if __name__ == "__main__":
    main()

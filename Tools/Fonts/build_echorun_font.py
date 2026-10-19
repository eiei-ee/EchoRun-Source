#!/usr/bin/env python3
"""Build and verify EchoRun's project-specific Simplified Chinese font subset.

Requires fonttools 4.55.0. The input must be the official static Regular OTF
from the Noto Sans CJK 2.004 release. The output remains under OFL-1.1 and is
renamed so it cannot be mistaken for an unmodified upstream font.
"""

from __future__ import annotations

import argparse
import hashlib
import os
import tempfile
from pathlib import Path

from fontTools import __version__ as fonttools_version
from fontTools.subset import Options, Subsetter
from fontTools.ttLib import TTFont


UPSTREAM_SHA256 = "2C76254F6FC379FDDFCE0A7E84FB5385BB135D3E399294F6EEB6680D0365B74B"
REQUIRED_FONTTOOLS_VERSION = "4.55.0"
FAMILY = "EchoRun Sans SC"
FULL_NAME = "EchoRun Sans SC Regular"
POSTSCRIPT_NAME = "EchoRunSansSC-Regular"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def load_unicodes(path: Path) -> set[int]:
    values: set[int] = set()
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.split("#", 1)[0]
        for token in line.split(","):
            token = token.strip()
            if not token:
                continue
            token = token.removeprefix("U+")
            if "-" in token:
                start, end = token.split("-", 1)
                values.update(range(int(start, 16), int(end, 16) + 1))
            else:
                values.add(int(token, 16))
    if not values:
        raise ValueError(f"No Unicode values found in {path}")
    return values


def rename_font(font: TTFont) -> None:
    replacements = {
        1: FAMILY,
        2: "Regular",
        3: "2.004;ECHORUN;EchoRunSansSC-Regular;ADOBE",
        4: FULL_NAME,
        6: POSTSCRIPT_NAME,
        16: FAMILY,
        17: "Regular",
    }
    name_table = font["name"]
    for record in name_table.names:
        if record.nameID in replacements:
            record.string = replacements[record.nameID].encode(record.getEncoding())
    for name_id, value in replacements.items():
        name_table.setName(value, name_id, 3, 1, 0x409)

    cff = font["CFF "].cff
    cff.fontNames = [POSTSCRIPT_NAME]
    top_dict = cff.topDictIndex[0]
    top_dict.FamilyName = FAMILY
    top_dict.FullName = FULL_NAME
    top_dict.Notice = (
        "Copyright 2014-2021 Adobe (http://www.adobe.com/). "
        "Noto is a trademark of Google Inc. EchoRun Sans SC is a "
        "project-specific subset derived from Noto Sans CJK SC 2.004."
    )


def verify(path: Path, requested: set[int]) -> None:
    font = TTFont(path, recalcTimestamp=False)
    name_table = font["name"]
    cmap = set(font.getBestCmap())
    variable_tables = {"fvar", "gvar", "avar", "HVAR"}.intersection(font.keys())

    if variable_tables:
        raise ValueError(f"Unexpected variable tables: {sorted(variable_tables)}")
    if font["OS/2"].usWeightClass != 400:
        raise ValueError("Output must have OS/2 weight class 400")
    if name_table.getDebugName(1) != FAMILY:
        raise ValueError("Unexpected family name")
    if name_table.getDebugName(2) != "Regular":
        raise ValueError("Unexpected subfamily name")
    if name_table.getDebugName(6) != POSTSCRIPT_NAME:
        raise ValueError("Unexpected PostScript name")
    if "2.004" not in (name_table.getDebugName(5) or ""):
        raise ValueError("Unexpected upstream version")
    missing = requested.difference(cmap)
    if missing:
        preview = ", ".join(f"U+{value:04X}" for value in sorted(missing)[:12])
        raise ValueError(f"Output is missing requested characters: {preview}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path, help="Official Noto Sans CJK SC 2.004 Regular OTF")
    parser.add_argument(
        "--unicodes",
        type=Path,
        default=Path(__file__).with_name("echorun-unicodes.txt"),
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("Assets/Resources/Fonts/EchoRunSansSC-Regular.otf"),
    )
    args = parser.parse_args()

    if fonttools_version != REQUIRED_FONTTOOLS_VERSION:
        raise RuntimeError(
            f"Expected fonttools {REQUIRED_FONTTOOLS_VERSION}, got {fonttools_version}"
        )
    if sha256(args.source) != UPSTREAM_SHA256:
        raise ValueError("Input does not match the pinned upstream 2.004 Regular OTF")

    requested = load_unicodes(args.unicodes)
    font = TTFont(args.source, recalcTimestamp=False)
    options = Options()
    options.layout_features = ["*"]
    options.name_IDs = ["*"]
    options.name_languages = ["*"]
    options.name_legacy = True
    options.notdef_outline = True
    options.recommended_glyphs = True
    options.recalc_timestamp = False
    options.canonical_order = True
    subsetter = Subsetter(options=options)
    subsetter.populate(unicodes=requested)
    subsetter.subset(font)
    rename_font(font)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    handle, temp_name = tempfile.mkstemp(
        prefix=f".{args.output.name}.", suffix=".tmp", dir=args.output.parent
    )
    os.close(handle)
    temp_output = Path(temp_name)
    try:
        font.save(temp_output, reorderTables=False)
        verify(temp_output, requested)
        temp_output.replace(args.output)
    finally:
        temp_output.unlink(missing_ok=True)

    print(f"Wrote {args.output}")
    print(f"SHA256 {sha256(args.output)}")
    print(f"Characters {len(requested)}")


if __name__ == "__main__":
    main()

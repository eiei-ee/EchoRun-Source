#!/usr/bin/env python3
"""Check the bundled cmap, pinned Unicode list and actual WeChat C# string literals.

Read-only; uses the same FontTools dependency as build_echorun_font.py.
Run from any working directory. No Unity editor or cloud connection is needed.
"""

from __future__ import annotations

import argparse
from pathlib import Path
import re

from fontTools.ttLib import TTFont

from build_echorun_font import load_unicodes


ROOT = Path(__file__).resolve().parents[2]
# Comments and character literals are consumed before string literals. This covers
# the project's ordinary/verbatim strings, including text in interpolated strings.
TOKENS = re.compile(
    r'//[^\r\n]*|/\*.*?\*/|\'(?:\\.|[^\'\\])\''
    r'|(?P<verbatim>@"(?:[^"]|"")*")'
    r'|(?P<regular>"(?:\\.|[^"\\])*")', re.DOTALL
)
ESCAPES = re.compile(r'\\(?:u([0-9A-Fa-f]{4})|U([0-9A-Fa-f]{8})|x([0-9A-Fa-f]{1,4})|(.))')
SIMPLE_ESCAPES = dict(zip('0abfnrtv', '\0\a\b\f\n\r\t\v'))


def decode_escape(match: re.Match[str]) -> str:
    for digits in match.groups()[:3]:
        if digits is not None:
            return chr(int(digits, 16))
    value = match.group(4)
    return SIMPLE_ESCAPES.get(value, value)


def literal_characters(source: str):
    for match in TOKENS.finditer(source):
        if match.group('verbatim') is not None:
            value = match.group('verbatim')[2:-1].replace('""', '"')
        elif match.group('regular') is not None:
            value = ESCAPES.sub(decode_escape, match.group('regular')[1:-1])
        else:
            continue
        yield source.count('\n', 0, match.start()) + 1, {
            ord(character) for character in value if character.isprintable()
        }


def describe(values: set[int]) -> str:
    # ASCII diagnostics survive Windows terminal encodings.
    return ', '.join(f'U+{value:04X}' for value in sorted(values))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--font', type=Path,
                        default=ROOT / 'Assets/Resources/Fonts/EchoRunSansSC-Regular.otf')
    parser.add_argument('--unicodes', type=Path,
                        default=Path(__file__).with_name('echorun-unicodes.txt'))
    parser.add_argument('--source-dir', type=Path, default=ROOT / 'Assets/WeixinMiniGame',
                        help='C# source directory whose UI text must be covered')
    args = parser.parse_args()
    requested = load_unicodes(args.unicodes)
    with TTFont(args.font, recalcTimestamp=False) as font:
        cmap = set(font.getBestCmap())
    errors = []
    if requested - cmap:
        errors.append('Unicode list missing from font: ' + describe(requested - cmap))
    if cmap - requested:
        errors.append('Font characters missing from Unicode list: ' + describe(cmap - requested))
    files = sorted(args.source_dir.rglob('*.cs'))
    if not files:
        parser.error('No C# files found in the requested source directory')
    literal_count = 0
    for path in files:
        source = path.read_text(encoding='utf-8-sig')
        for line, characters in literal_characters(source):
            literal_count += 1
            missing = characters - (cmap & requested)
            if missing:
                errors.append(f'{path.name}:{line}: UI text not covered: {describe(missing)}')
    if errors:
        parser.exit(1, '\n'.join(errors) + '\n')
    print(f'PASS: {len(cmap)} characters, {len(files)} C# files, '
          f'{literal_count} string literals; Unicode list and font match; UI coverage complete.')


if __name__ == '__main__':
    main()

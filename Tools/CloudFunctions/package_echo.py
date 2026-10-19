#!/usr/bin/env python3
"""Package the installed Echo cloud function for portable, manual ZIP deployment.

Uses only the Python standard library; does not install dependencies or deploy.
The output directory must already exist. Existing ZIP/manifest files are refused.
"""

import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import stat
import sys
import zipfile


ROOT_FILES = (
    "index.js", "handler.js", "protocol.js", "repository.js",
    "package.json", "package-lock.json",
)
SDK_VERSION = "3.0.1"
REQUIRED_ENTRIES = (*ROOT_FILES, "node_modules/wx-server-sdk/index.js",
                    "node_modules/wx-server-sdk/package.json")


def checked_stat(path):
    info = path.lstat()
    if stat.S_ISLNK(info.st_mode) or (
        getattr(info, "st_file_attributes", 0)
        & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)
    ):
        raise ValueError(f"Links and reparse points are not allowed: {path}")
    return info


def validate_entry_name(name):
    parts = PurePosixPath(name).parts
    if (not name or "\\" in name or ":" in name or name.startswith("/")
            or any(part in ("", ".", "..") for part in name.split("/"))
            or PurePosixPath(name).is_absolute() or not parts):
        raise ValueError(f"Unsafe ZIP entry: {name!r}")


def source_entry(path, root):
    info = checked_stat(path)
    path.resolve(strict=True).relative_to(root)
    if not stat.S_ISREG(info.st_mode):
        raise ValueError(f"Expected a regular file: {path}")
    name = path.relative_to(root).as_posix()
    validate_entry_name(name)
    return name, path


def collect_sources(root):
    if not stat.S_ISDIR(checked_stat(root).st_mode):
        raise ValueError("The echo source directory is missing")
    root = root.resolve(strict=True)
    entries = [source_entry(root / name, root) for name in ROOT_FILES]
    dependencies = root / "node_modules"
    if not stat.S_ISDIR(checked_stat(dependencies).st_mode):
        raise ValueError("Install the locked dependencies before packaging")
    for directory, dirs, files in os.walk(dependencies, followlinks=False):
        current = Path(directory)
        for name in dirs:
            child = current / name
            if not stat.S_ISDIR(checked_stat(child).st_mode):
                raise ValueError(f"Expected a dependency directory: {child}")
            child.resolve(strict=True).relative_to(root)
        entries.extend(source_entry(current / name, root) for name in files)
    names = [name for name, _ in entries]
    if len(names) != len(set(names)):
        raise ValueError("Duplicate archive paths")
    # Keep Linux paths unambiguous even if input later moves to a case-sensitive host.
    if len(names) != len({name.casefold() for name in names}):
        raise ValueError("Archive paths differ only by letter case")
    if not set(REQUIRED_ENTRIES).issubset(names):
        raise ValueError("Required SDK runtime files are missing")
    package = json.loads((root / "package.json").read_text(encoding="utf-8"))
    lock = json.loads((root / "package-lock.json").read_text(encoding="utf-8"))
    sdk = json.loads((dependencies / "wx-server-sdk/package.json").read_text(encoding="utf-8"))
    if (package.get("dependencies", {}).get("wx-server-sdk") != SDK_VERSION
            or lock.get("packages", {}).get("node_modules/wx-server-sdk", {}).get("version") != SDK_VERSION
            or sdk.get("version") != SDK_VERSION):
        raise ValueError(f"Expected installed and locked wx-server-sdk {SDK_VERSION}")
    return sorted(entries)


def sha256_file(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def verify_archive(path, expected_names):
    with zipfile.ZipFile(path, "r") as archive:
        names = archive.namelist()
        for name in names:
            validate_entry_name(name)
        if len(names) != len(set(names)) or set(names) != set(expected_names):
            raise ValueError("ZIP contents do not match the selected production files")
        if not set(REQUIRED_ENTRIES).issubset(names):
            raise ValueError("ZIP is missing required root or SDK files")
        if archive.testzip() is not None:
            raise ValueError("ZIP CRC verification failed")
        sdk = json.loads(archive.read("node_modules/wx-server-sdk/package.json"))
        if sdk.get("version") != SDK_VERSION:
            raise ValueError("ZIP contains an unexpected SDK version")
    return len(names)


def package_echo(output):
    root = Path(__file__).absolute().parent / "echo"
    sources = collect_sources(root)
    output = output.absolute()
    if ".." in output.parts or output.suffix.lower() != ".zip":
        raise ValueError("Choose a new .zip path without parent traversal")
    # Refuse symlinks/junctions in the destination chain as well as in source files.
    for parent in [output.parent, *output.parent.parents]:
        if not stat.S_ISDIR(checked_stat(parent).st_mode):
            raise ValueError(f"Output directory does not exist: {parent}")
    manifest_path = output.with_suffix(".manifest.json")
    for target in (output, manifest_path):
        if os.path.lexists(target):
            raise FileExistsError(f"Refusing to overwrite: {target}")
    if output.is_relative_to(root.resolve(strict=True)):
        raise ValueError("Output must be outside the echo source directory")

    created = []
    try:
        # Exclusive creation also handles a competing process creating the same path.
        with manifest_path.open("x", encoding="utf-8", newline="\n") as manifest_stream:
            created.append(manifest_path)
            with output.open("xb") as output_stream:
                created.append(output)
                with zipfile.ZipFile(output_stream, "w", compression=zipfile.ZIP_DEFLATED,
                                     compresslevel=9) as archive:
                    for name, source in sources:
                        source_entry(source, root.resolve(strict=True))
                        info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
                        info.compress_type = zipfile.ZIP_DEFLATED
                        info.create_system = 3
                        info.external_attr = (stat.S_IFREG | 0o644) << 16
                        with source.open("rb") as input_stream, archive.open(info, "w") as destination:
                            shutil.copyfileobj(input_stream, destination)
            count = verify_archive(output, [name for name, _ in sources])
            manifest = {
                "file": output.name,
                "sha256": sha256_file(output),
                "bytes": output.stat().st_size,
                "files": count,
                "sdk": f"wx-server-sdk {SDK_VERSION}",
                "packageLockSha256": sha256_file(root / "package-lock.json"),
                "deployed": False,
            }
            manifest_stream.write(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
        return manifest
    except Exception:
        # Remove only files created by this invocation, never a pre-existing output.
        for target in reversed(created):
            target.unlink(missing_ok=True)
        raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True, help="New ZIP file in an existing directory")
    args = parser.parse_args()
    try:
        manifest = package_echo(args.output)
    except (OSError, ValueError, zipfile.BadZipFile) as error:
        parser.exit(1, f"Packaging failed: {error}\n")
    print(json.dumps(manifest, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())

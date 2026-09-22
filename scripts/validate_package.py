#!/usr/bin/env python3
from __future__ import annotations

import json
import re
import struct
import sys
import zipfile
from pathlib import Path


def fail(message: str) -> None:
    raise RuntimeError(message)


def main() -> int:
    try:
        root = Path(__file__).resolve().parents[1]
        manifest = json.loads((root / "package/manifest.json").read_text(encoding="utf-8"))
        if manifest.get("name") != "Valheim_Performance_Profiler":
            fail("unexpected package name")
        if not re.fullmatch(r"\d+\.\d+\.\d+", manifest.get("version_number", "")):
            fail("version_number must be semantic version x.y.z")
        if manifest.get("dependencies") != ["denikson-BepInExPack_Valheim-5.4.2350"]:
            fail("the standalone profiler must depend only on BepInEx")
        icon = (root / "package/icon.png").read_bytes()
        if icon[:8] != b"\x89PNG\r\n\x1a\n" or struct.unpack(">II", icon[16:24]) != (256, 256):
            fail("icon.png must be a 256 by 256 PNG")
        archive = root / "artifacts" / f"LostKode-{manifest['name']}-{manifest['version_number']}.zip"
        with zipfile.ZipFile(archive) as package:
            names = set(package.namelist())
            required = {"manifest.json", "README.md", "icon.png", "plugins/ValheimPerformanceProfiler.dll"}
            if required - names:
                fail(f"archive is missing {sorted(required - names)}")
            if package.testzip() is not None:
                fail("archive contains a corrupt entry")
            if json.loads(package.read("manifest.json")) != manifest:
                fail("packaged manifest differs from source")
        print(f"validated {manifest['name']} {manifest['version_number']}")
        print(f"archive: {archive}")
        return 0
    except (OSError, ValueError, RuntimeError, json.JSONDecodeError, zipfile.BadZipFile) as error:
        print(f"validation failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())

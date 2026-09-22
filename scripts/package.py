#!/usr/bin/env python3
import sys
import zipfile
from pathlib import Path

if len(sys.argv) != 4:
    raise SystemExit("usage: package.py PACKAGE_DIR DLL OUTPUT")
package, dll, output = map(Path, sys.argv[1:])
if not dll.is_file():
    raise SystemExit(f"missing DLL: {dll}")
output.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
    for source in sorted(package.rglob("*")):
        if source.is_file(): archive.write(source, source.relative_to(package).as_posix())
    archive.write(dll, f"plugins/{dll.name}")


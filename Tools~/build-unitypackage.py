#!/usr/bin/env python3
"""Build the Asset Store .unitypackage for SceneXRay.

A .unitypackage is a gzipped tar of one directory per asset, named after the asset's GUID:

    <guid>/pathname    project-relative path, no trailing newline
    <guid>/asset.meta  the asset's .meta file, verbatim
    <guid>/asset       the asset's bytes; absent for folders

Unity's own exporter is not used here on purpose: it needs a running editor, and the archive
must be reproducible from a clean checkout so that "is the shipped archive the same as the
source?" stays a question anyone can answer with a diff.

Everything under Assets/SceneXRay ships except EXCLUDED — the repository tests would otherwise
make Unity Test Framework a customer-facing dependency.

    python Tools~/build-unitypackage.py [--version 1.0.0]
"""

import argparse
import gzip
import hashlib
import io
import os
import re
import sys
import tarfile
import time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACKAGE_ROOT = "Assets/SceneXRay"
EXCLUDED = {"Assets/SceneXRay/Tests"}
GUID_RE = re.compile(rb"^guid:\s*([0-9a-fA-F]{32})\s*$", re.MULTILINE)


def is_excluded(rel):
    return any(rel == e or rel.startswith(e + "/") for e in EXCLUDED)


def collect():
    """Every shippable path under the package root, folders included, sorted for determinism."""
    entries = []
    for dirpath, dirnames, filenames in os.walk(os.path.join(ROOT, PACKAGE_ROOT)):
        dirnames.sort()
        filenames.sort()
        for name in dirnames + filenames:
            if name.endswith(".meta"):
                continue
            full = os.path.join(dirpath, name)
            rel = os.path.relpath(full, ROOT).replace(os.sep, "/")
            if is_excluded(rel):
                continue
            entries.append(rel)
    entries.append(PACKAGE_ROOT)  # the root folder itself carries a meta too
    return sorted(set(entries))


def guid_of(meta_path):
    with open(meta_path, "rb") as fh:
        m = GUID_RE.search(fh.read())
    if not m:
        raise SystemExit(f"no guid in {meta_path}")
    return m.group(1).decode("ascii").lower()


def build(out_path):
    entries = collect()
    seen = {}
    raw = io.BytesIO()
    mtime = int(time.time())

    def add(tar, name, payload, isdir=False):
        info = tarfile.TarInfo(name)
        info.mtime = mtime
        if isdir:
            info.type = tarfile.DIRTYPE
            info.mode = 0o777
            tar.addfile(info)
        else:
            info.size = len(payload)
            info.mode = 0o777
            tar.addfile(info, io.BytesIO(payload))

    with tarfile.open(fileobj=raw, mode="w", format=tarfile.GNU_FORMAT) as tar:
        for rel in entries:
            full = os.path.join(ROOT, rel.replace("/", os.sep))
            meta = full + ".meta"
            if not os.path.exists(meta):
                raise SystemExit(f"missing .meta for {rel}")

            guid = guid_of(meta)
            if guid in seen:
                raise SystemExit(f"duplicate guid {guid}: {seen[guid]} and {rel}")
            seen[guid] = rel

            add(tar, guid + "/", b"", isdir=True)
            if os.path.isfile(full):
                with open(full, "rb") as fh:
                    add(tar, guid + "/asset", fh.read())
            with open(meta, "rb") as fh:
                add(tar, guid + "/asset.meta", fh.read())
            add(tar, guid + "/pathname", rel.encode("utf-8"))

    data = raw.getvalue()
    with open(out_path, "wb") as fh:
        # mtime=0 so a rebuild of unchanged sources produces an identical archive.
        with gzip.GzipFile(fileobj=fh, mode="wb", mtime=0) as gz:
            gz.write(data)

    files = sum(1 for r in entries if os.path.isfile(os.path.join(ROOT, r.replace("/", os.sep))))
    return entries, files


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--version", default="1.0.0")
    args = ap.parse_args()

    out_dir = os.path.join(ROOT, "AssetStore")
    os.makedirs(out_dir, exist_ok=True)
    out_path = os.path.join(out_dir, f"SceneXRay-{args.version}.unitypackage")

    entries, files = build(out_path)

    with open(out_path, "rb") as fh:
        digest = hashlib.sha256(fh.read()).hexdigest().upper()

    print(f"wrote     {os.path.relpath(out_path, ROOT)}")
    print(f"paths     {len(entries)} ({files} files, {len(entries) - files} folders)")
    print(f"size      {os.path.getsize(out_path)} bytes")
    print(f"sha256    {digest}")
    return 0


if __name__ == "__main__":
    sys.exit(main())

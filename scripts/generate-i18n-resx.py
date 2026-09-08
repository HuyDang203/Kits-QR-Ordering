#!/usr/bin/env python3
"""Generate SharedResources.resx + SharedResources.vi.resx from guest JSON bundles."""
from __future__ import annotations

import json
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WEB = ROOT / "Annap.CoffeeQrOrdering.Web"
RES_DIR = WEB / "Resources"
RES_DIR.mkdir(parents=True, exist_ok=True)


def flatten(obj: object, prefix: str = "") -> dict[str, str]:
    items: dict[str, str] = {}
    if isinstance(obj, dict):
        for key, value in obj.items():
            if key == "lang":
                continue
            path = f"{prefix}.{key}" if prefix else str(key)
            if isinstance(value, dict):
                items.update(flatten(value, path))
            else:
                items[path] = str(value)
    return items


def write_resx(path: Path, entries: dict[str, str]) -> None:
    root = ET.Element("root")
    for name in (
        "resheader",
        "resheader",
        "resheader",
        "resheader",
    ):
        pass

    headers = [
        ("resmimetype", "text/microsoft-resx"),
        ("version", "2.0"),
        ("reader", "System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
        ("writer", "System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
    ]
    for name, val in headers:
        h = ET.SubElement(root, "resheader", {"name": name})
        v = ET.SubElement(h, "value")
        v.text = val

    for key in sorted(entries):
        data = ET.SubElement(root, "data", {"name": key, "{http://www.w3.org/XML/1998/namespace}space": "preserve"})
        val = ET.SubElement(data, "value")
        val.text = entries[key]

    tree = ET.ElementTree(root)
    ET.indent(tree, space="  ")
    path.write_text('<?xml version="1.0" encoding="utf-8"?>\n' + ET.tostring(root, encoding="unicode"), encoding="utf-8")


def main() -> None:
    en = flatten(json.loads((WEB / "wwwroot/i18n/guest-en.json").read_text(encoding="utf-8")))
    vi = flatten(json.loads((WEB / "wwwroot/i18n/guest-vi.json").read_text(encoding="utf-8")))
    all_keys = sorted(set(en) | set(vi))
    en_full = {k: en.get(k, vi.get(k, k)) for k in all_keys}
    vi_full = {k: vi.get(k, en.get(k, k)) for k in all_keys}
    write_resx(RES_DIR / "SharedResources.resx", en_full)
    write_resx(RES_DIR / "SharedResources.vi.resx", vi_full)
    print(f"Wrote {len(all_keys)} keys to {RES_DIR}")


if __name__ == "__main__":
    main()

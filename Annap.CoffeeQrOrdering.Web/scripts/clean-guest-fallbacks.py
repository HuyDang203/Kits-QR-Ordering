import re
from pathlib import Path

root = Path(__file__).resolve().parent.parent

guest_files = [
    root / "wwwroot/js/order-tray-dock.js",
    root / "Pages/Index.cshtml",
    root / "wwwroot/js/guest-interaction-contract.js",
    root / "wwwroot/js/guest-discovery-duck.js",
    root / "wwwroot/js/guest-order-queue.js",
    root / "wwwroot/js/guest-sommelier-lite.js",
]

# Remove chained t()/tOrder()/LuxuryI18n.t() fallbacks ending in || "literal"
chain_re = re.compile(
    r'((?:tOrder|LuxuryI18n\.t)\("[^"]+"\)(?:\s*\|\|\s*(?:tOrder|LuxuryI18n\.t)\("[^"]+"\))*)\s*\|\|\s*"[^"]*"',
)

for path in guest_files:
    if not path.exists():
        continue
    text = path.read_text(encoding="utf-8")
    new_text = chain_re.sub(r"\1", text)
    if new_text != text:
        path.write_text(new_text, encoding="utf-8")
        print(f"Cleaned fallbacks in {path.name}")

print("Done")

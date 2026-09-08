import re
from pathlib import Path

root = Path(__file__).resolve().parent.parent

tray_path = root / "wwwroot/js/order-tray-dock.js"
tray = tray_path.read_text(encoding="utf-8")

dup = re.compile(
    r"function trayCopyFallback\(key, vi, en\) \{.*?\n        \}\n\n        function trayCopyFallback\(key, vi, en\) \{.*?\n        \}",
    re.S,
)
tray = dup.sub(
    'function trayCopy(key) {\n            return tOrder(key) || "";\n        }',
    tray,
    count=1,
)
tray = re.sub(r'trayCopyFallback\(\s*"([^"]+)"[^)]*\)', r'trayCopy("\1")', tray)
tray = re.sub(r'tOrder\("([^"]+)"\) \|\| trayCopy\("\1"\)', r'tOrder("\1")', tray)
tray_path.write_text(tray, encoding="utf-8")

track_path = root / "Pages/Track/Order.cshtml"
track = track_path.read_text(encoding="utf-8")
track = re.sub(r"^\s*const MILESTONE_FALLBACK = \[.*?\];\n\n", "", track, flags=re.M)
track = track.replace('return MILESTONE_FALLBACK[step - 1] || "";', 'return "";')
track = re.sub(r'trackT\("([^"]+)"\) \|\| "[^"]*"', r'trackT("\1")', track)
track = re.sub(
    r'bill\.shopName \|\| trackT\("([^"]+)"\) \|\| "[^"]*"',
    r'bill.shopName || trackT("\1")',
    track,
)
track_path.write_text(track, encoding="utf-8")

bank_path = root / "wwwroot/js/guest-bank-transfer.js"
bank = bank_path.read_text(encoding="utf-8")
bank = re.sub(r't\("([^"]+)", "[^"]*", "[^"]*"\)', r't("\1")', bank)
bank_path.write_text(bank, encoding="utf-8")

print("Fixed tray, track, and bank-transfer fallbacks")

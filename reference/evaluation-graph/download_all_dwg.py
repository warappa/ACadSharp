#!/usr/bin/env python3
"""Download all 36 pages of dwg.ru thread 24597 to raw files."""
import os
import time
import urllib.request

BASE = "https://forum.dwg.ru/showthread.php?t=24597"
HEADERS = {"User-Agent": "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36"}
OUT = "/home/warappa/Projects/ACadSharp/.tmp_research/dwg_raw"
os.makedirs(OUT, exist_ok=True)


def fetch(url):
    req = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(req, timeout=90) as r:
        return r.read()


for p in range(1, 37):
    out = f"{OUT}/p{p:02d}.html"
    if os.path.exists(out) and os.path.getsize(out) > 1000:
        print(f"p{p:02d} exists")
        continue
    url = BASE if p == 1 else f"{BASE}&page={p}"
    try:
        data = fetch(url)
        with open(out, "wb") as f:
            f.write(data)
        print(f"p{p:02d} {len(data)} bytes")
    except Exception as e:
        print(f"p{p:02d} FAILED: {e}")
    time.sleep(0.5)

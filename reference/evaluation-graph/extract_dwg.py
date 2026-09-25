#!/usr/bin/env python3
"""Download dwg.ru forum thread pages and extract post messages."""
import re
import sys
import html as htmllib
import urllib.request

BASE = "https://forum.dwg.ru/showthread.php?t=24597"
HEADERS = {"User-Agent": "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36"}


def fetch(url):
    req = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(req, timeout=60) as r:
        data = r.read()
    return data.decode("cp1251", errors="replace")


def strip_tags(s):
    s = re.sub(r"<script.*?</script>", " ", s, flags=re.S | re.I)
    s = re.sub(r"<style.*?</style>", " ", s, flags=re.S | re.I)
    s = re.sub(r"<br\s*/?>", "\n", s, flags=re.I)
    s = re.sub(r"</(p|div|tr|pre|table|li|h[1-6])>", "\n", s, flags=re.I)
    s = re.sub(r"<(p|div|tr|pre|table|li|h[1-6])[^>]*>", "\n", s, flags=re.I)
    s = re.sub(r"<[^>]+>", "", s)
    s = htmllib.unescape(s)
    s = re.sub(r"[ \t]+", " ", s)
    s = re.sub(r"\n\s*\n+", "\n\n", s)
    return s.strip()


def main():
    pages = [int(x) for x in sys.argv[1:]]
    out = []
    for p in pages:
        url = BASE if p == 1 else f"{BASE}&page={p}"
        try:
            page = fetch(url)
        except Exception as e:
            out.append(f"=== PAGE {p} FAILED: {e} ===")
            continue
        # split by post start markers
        chunks = re.split(r"<!-- post #(\d+) -->", page)
        # chunks: [pre, id1, body1, id2, body2, ...]
        out.append(f"=== PAGE {p} ===")
        for i in range(1, len(chunks) - 1, 2):
            pid = chunks[i]
            body = chunks[i + 1]
            m = re.search(r"#<strong>(\d+)</strong>", body)
            pnum = m.group(1) if m else "?"
            mdate = re.search(r"(\d{2}\.\d{2}\.\d{4}, \d{2}:\d{2})", body)
            date = mdate.group(1) if mdate else "?"
            mauth = re.search(r'class="bigusername" href="member\.php[^"]*">([^<]+)</a>', body)
            author = mauth.group(1) if mauth else "?"
            mm = re.search(r'<div id="post_message_\d+"[^>]*>(.*?)<!-- / message -->', body, flags=re.S)
            msg = strip_tags(mm.group(1)) if mm else "(no message extracted)"
            out.append(f"\n--- post #{pnum} by {author} ({date}) ---\n{msg}")
    print("\n".join(out))


if __name__ == "__main__":
    main()

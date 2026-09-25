#!/usr/bin/env python3
"""Extract clean text from the Russian Lazebny parts (koi8-r encoded)."""
import re
import html as htmllib
import glob


def strip_tags(s):
    s = re.sub(r"<script.*?</script>", " ", s, flags=re.S | re.I)
    s = re.sub(r"<style.*?</style>", " ", s, flags=re.S | re.I)
    s = re.sub(r"<br\s*/?>", "\n", s, flags=re.I)
    s = re.sub(r"</(p|div|tr|pre|table|li|h[1-6]|blockquote)>", "\n", s, flags=re.I)
    s = re.sub(r"<(p|div|tr|pre|table|li|h[1-6]|blockquote)[^>]*>", "\n", s, flags=re.I)
    s = re.sub(r"<[^>]+>", "", s)
    s = htmllib.unescape(s)
    s = re.sub(r"[ \t]+", " ", s)
    s = re.sub(r"\n\s*\n+", "\n\n", s)
    return s.strip()


for f in sorted(glob.glob("ru_part*.html")):
    with open(f, "rb") as fh:
        data = fh.read()
    text = data.decode("koi8-r", errors="replace")
    # main content: between <body> and the nav footer
    m = re.search(r"<body[^>]*>(.*)</body>", text, re.S)
    body = m.group(1) if m else text
    # cut the top/bottom nav lines
    out = strip_tags(body)
    # remove the leading nav line and trailing nav
    lines = out.split("\n")
    # find where the article starts (line containing "Мистери" or "Тайны")
    start = 0
    for i, l in enumerate(lines):
        if "Мистери" in l or "Тайны" in l:
            start = i
            break
    # find where nav resumes at end
    end = len(lines)
    for i in range(len(lines) - 1, -1, -1):
        if "[CAD" in lines[i] or "poleshchuk" in lines[i]:
            end = i
            break
    print(f"===== {f} =====")
    print("\n".join(lines[start:end]))
    print()

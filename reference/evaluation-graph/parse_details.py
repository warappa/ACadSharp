#!/usr/bin/env python3
"""Parse OARX detail HTML pages into structured JSON."""
import json, os, re, sys
from html.parser import HTMLParser

RAW = "/home/warappa/Projects/ACadSharp/.tmp_research/raw/detail"

class Extractor(HTMLParser):
    def __init__(self):
        super().__init__()
        self.sections = []  # list of (section_title, [blocks])
        self.cur_title = None
        self.cur_blocks = []
        self.in_pre = False
        self.pre_buf = []
        self.in_h1 = False
        self.h1_buf = []
        self.in_p = False
        self.p_buf = []
        self.in_td = False
        self.td_buf = []
        self.td_stack = []
        self.in_th = False
        self.th_buf = []
        self.table_rows = []
        self.cur_row = []
        self.in_table = False
        self.in_h3 = False
        self.h3_buf = []

    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if tag == "h1":
            self.in_h1 = True; self.h1_buf = []
        elif tag == "h3":
            self.in_h3 = True; self.h3_buf = []
        elif tag == "pre":
            self.in_pre = True; self.pre_buf = []
        elif tag == "p":
            self.in_p = True; self.p_buf = []
        elif tag == "table":
            self.in_table = True; self.table_rows = []; self.cur_row = []
        elif tag == "tr":
            self.cur_row = []
        elif tag in ("td", "th"):
            if tag == "td":
                self.in_td = True; self.td_buf = []
            else:
                self.in_th = True; self.th_buf = []

    def handle_endtag(self, tag):
        if tag == "h1" and self.in_h1:
            self.in_h1 = False
            self.sections.append(("__TITLE__", ["".join(self.h1_buf)]))
        elif tag == "h3" and self.in_h3:
            self.in_h3 = False
            t = re.sub(r"\s+", " ", "".join(self.h3_buf)).strip()
            self.sections.append((t, []))
        elif tag == "pre" and self.in_pre:
            self.in_pre = False
            self.sections.append(("__PRE__", ["".join(self.pre_buf)]))
        elif tag == "p" and self.in_p:
            self.in_p = False
            t = re.sub(r"\s+", " ", "".join(self.p_buf)).strip()
            if t:
                # attach to current named section
                named = [s for s in self.sections if s[0] != "__PRE__"]
                if named:
                    named[-1][1].append(t)
                else:
                    self.sections.append(("__PARA__", [t]))
        elif tag == "table" and self.in_table:
            self.in_table = False
            self.sections.append(("__TABLE__", [json.dumps(self.table_rows)]))
        elif tag in ("td", "th"):
            if tag == "td" and self.in_td:
                self.in_td = False
                self.cur_row.append(re.sub(r"\s+", " ", "".join(self.td_buf)).strip())
            elif tag == "th" and self.in_th:
                self.in_th = False
                self.cur_row.append(re.sub(r"\s+", " ", "".join(self.th_buf)).strip())
        elif tag == "tr":
            if self.cur_row:
                self.table_rows.append(self.cur_row)
            self.cur_row = []

    def handle_data(self, data):
        if self.in_h1:
            self.h1_buf.append(data)
        if self.in_h3:
            self.h3_buf.append(data)
        if self.in_pre:
            self.pre_buf.append(data)
        if self.in_p:
            self.p_buf.append(data)
        if self.in_td:
            self.td_buf.append(data)
        if self.in_th:
            self.th_buf.append(data)


def parse_file(path):
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        html = f.read()
    e = Extractor()
    e.feed(html)
    out = {"file": os.path.basename(path)}
    # Title
    m = re.search(r"<h1>(.*?)</h1>", html, re.S)
    out["title"] = re.sub(r"\s+", " ", m.group(1)).strip() if m else ""
    # Signature
    m = re.search(r'<pre class="pre codeblock prettyprint">(.*?)</pre>', html, re.S)
    if m:
        sig = m.group(1)
        sig = sig.replace("&#xD;", "\n").replace("&amp;", "&").replace("&lt;", "<").replace("&gt;", ">").replace("&#xA;", "\n")
        sig = re.sub(r"\n\s*", "\n", sig)
        out["signature"] = sig.strip()
    # Description: text between Description h3 and next h3
    m = re.search(r"Description</h3>(.*?)(?=<h3|</div></div></body>)", html, re.S)
    if m:
        desc_html = m.group(1)
        # strip tags
        desc = re.sub(r"<[^>]+>", " ", desc_html)
        desc = desc.replace("&#xD;", " ").replace("&amp;", "&").replace("&lt;", "<").replace("&gt;", ">").replace("&#xA;", " ")
        desc = re.sub(r"[ \t]+", " ", desc)
        desc = re.sub(r"\n\s*\n?", "\n", desc)
        # collapse
        parts = [p.strip() for p in desc.split("\n") if p.strip()]
        out["description"] = " ".join(parts)
    # Parameters table
    m = re.search(r"Parameters</h3>(.*?)<h3", html, re.S)
    if m:
        rows = re.findall(r"<tr>(.*?)</tr>", m.group(1), re.S)
        params = []
        for r in rows:
            cells = re.findall(r"<t[dh][^>]*>(.*?)</t[dh]>", r, re.S)
            if len(cells) >= 2:
                c0 = re.sub(r"<[^>]+>", " ", cells[0])
                c1 = re.sub(r"<[^>]+>", " ", cells[1])
                c0 = c0.replace("&#xD;", " ").replace("&amp;", "&").replace("&lt;", "<").replace("&gt;", ">")
                c1 = c1.replace("&#xD;", " ").replace("&amp;", "&").replace("&lt;", "<").replace("&gt;", ">")
                c0 = re.sub(r"\s+", " ", c0).strip()
                c1 = re.sub(r"\s+", " ", c1).strip()
                if c0 == "Parameters":
                    continue
                params.append({"name": c0, "desc": c1})
        if params:
            out["parameters"] = params
    # Other sections: look for h3 titles
    h3s = re.findall(r"<h3[^>]*>\s*(.*?)</h3>", html, re.S)
    out["sections"] = [re.sub(r"\s+", " ", x).strip() for x in h3s]
    # Remarks / Return value etc.
    for sec in ("Return value", "Returns", "Remarks", "See Also", "Examples", "Notes", "Exception"):
        m = re.search(re.escape(sec) + r"</h3>(.*?)(?=<h3|$)", html, re.S)
        if m:
            t = re.sub(r"<[^>]+>", " ", m.group(1))
            t = t.replace("&#xD;", " ").replace("&amp;", "&").replace("&lt;", "<").replace("&gt;", ">")
            t = re.sub(r"\s+", " ", t).strip()
            if t:
                out[sec.lower().replace(" ", "_")] = t
    return out


def main():
    results = []
    for fn in sorted(os.listdir(RAW)):
        if fn.endswith(".html"):
            results.append(parse_file(os.path.join(RAW, fn)))
    with open("/home/warappa/Projects/ACadSharp/.tmp_research/parsed_details.json", "w", encoding="utf-8") as f:
        json.dump(results, f, indent=2, ensure_ascii=False)
    print(f"Parsed {len(results)} files")
    for r in results:
        print(f"- {r['title']} | sig={'Y' if r.get('signature') else 'N'} | desc={'Y' if r.get('description') else 'N'} | params={len(r.get('parameters', []))}")

if __name__ == "__main__":
    main()

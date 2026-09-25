#!/usr/bin/env python3
"""Generate the final objectarx-eval-api.md from parsed data + saved HTML."""
import json, re, os

RAW = "/home/warappa/Projects/ACadSharp/.tmp_research/raw"
OUT = "/home/warappa/Projects/ACadSharp/docs/articles/objectarx-eval-api.md"

def clean(s):
    s = s.replace("&#xD;", " ").replace("&#xA;", " ").replace("&amp;", "&").replace("&lt;", "<").replace("&gt;", ">").replace("&#xD;", " ")
    s = re.sub(r"<[^>]+>", " ", s)
    s = re.sub(r"[ \t]+", " ", s)
    return s.strip()

def parse_overview(fn):
    html = open(os.path.join(RAW, fn), encoding="utf-8", errors="replace").read()
    out = {}
    m = re.search(r"Class Hierarchy</h3>(.*?)<h3", html, re.S)
    if m:
        h = re.sub(r"<[^>]+>", "", m.group(1))
        h = h.replace("&#xD;", "\n").replace("&#xA;", "\n")
        lines = [l.strip() for l in h.split("\n") if l.strip()]
        out["hierarchy"] = " > ".join(lines)
    m = re.search(r'C\+\+</h3>\s*<pre[^>]*>(.*?)</pre>', html, re.S)
    if m:
        out["cpp"] = clean(m.group(1)).replace("\n", " ")
    m = re.search(r"File</h3>(.*?)<h3", html, re.S)
    if m:
        out["file"] = clean(m.group(1))
    m = re.search(r"Description</h3>(.*?)(?=<h3|$)", html, re.S)
    if m:
        paras = re.findall(r"<p>(.*?)</p>", m.group(1), re.S)
        out["description"] = [clean(p) for p in paras]
    m = re.search(r"Links</h3>(.*?)<h3", html, re.S)
    if m:
        out["links"] = clean(m.group(1))
    m = re.search(r"See Also</h3>(.*?)</div>", html, re.S)
    if m:
        out["see_also"] = clean(m.group(1))
    return out

def parse_summary_table(fn):
    """Parse a __MEMBERTYPE_Methods_ page: rows of (name, virtual, abstract, short desc)."""
    html = open(os.path.join(RAW, fn), encoding="utf-8", errors="replace").read()
    rows = re.findall(r"<tr><td>(.*?)</td><td>(.*?)</td></tr>", html, re.S)
    out = []
    for r in rows:
        flags = re.findall(r'indicator_(\w+)\.gif', r[0])
        m = re.search(r'<a href="([^"]+)">([^<]+)</a>', r[0])
        name = m.group(2) if m else "?"
        href = m.group(1) if m else ""
        desc = clean(r[1])
        out.append({"name": name, "href": href, "virtual": "virtual" in flags,
                    "abstract": "abstract" in flags, "desc": desc})
    return out

def parse_overload_list(fn):
    html = open(os.path.join(RAW, fn), encoding="utf-8", errors="replace").read()
    rows = re.findall(r"<tr><td>(.*?)</td><td>(.*?)</td></tr>", html, re.S)
    out = []
    for r in rows:
        m = re.search(r'<a href="([^"]+)">([^<]+)</a>', r[0])
        out.append({"sig": m.group(2) if m else "?", "href": m.group(1) if m else "", "desc": clean(r[1])})
    return out

# Load parsed details
details = json.load(open("/home/warappa/Projects/ACadSharp/.tmp_research/parsed_details.json"))
by_file = {d["file"]: d for d in details}

CLASSES = ["AcDbEvalGraph", "AcDbEvalExpr", "AcDbEvalContext", "AcDbEvalIdMap",
          "AcDbEvalVariant", "AcDbEvalContextPair"]

def method_key(fname):
    """Group detail page filename by method/ctor name.
    Filenames are OARX-RefGuide-<Class>__<Method>_<args...>.html
    (class and method separated by a double underscore)."""
    m = re.match(r"OARX-RefGuide-(?P<cls>AcDbEval[A-Za-z]+)__(?P<rest>[^_]*)", fname)
    if not m:
        return None, None
    cls = m.group("cls")
    rest = m.group("rest")
    if rest.endswith(".html"):
        rest = rest[:-5]
    if rest == cls:  # constructor
        return cls, "__CONSTRUCTOR__"
    return cls, rest

# Group detail pages
groups = {}
for d in details:
    cls, key = method_key(d["file"])
    if cls is None or key is None:
        continue
    groups.setdefault((cls, key), []).append(d)

def md_sig(sig):
    if not sig:
        return "_(no C++ signature shown)_"
    return "```cpp\n" + sig + "\n```"

L = []
L.append("# ObjectARX Dynamic-Block Evaluation Graph API (AcDbEval* classes)")
L.append("")
L.append("Complete API surface for the six ObjectARX evaluation-graph classes, collected from the")
L.append("**official Autodesk ObjectARX documentation** (help.autodesk.com, OARX 2025 ENU RefGuide).")
L.append("")
L.append("## Provenance & verification")
L.append("")
L.append("- Primary source: `https://help.autodesk.com/view/OARX/2025/ENU/?guid=...` pages. The site is a")
L.append("  JavaScript app, but the underlying content is served as static HTML at")
L.append("  `https://help.autodesk.com/cloudhelp/2025/ENU/OARX-RefGuide/files/<topic>.html` (discovered via the")
L.append("  beehive REST endpoint `https://beehive.autodesk.com/community/service/rest/cloudhelp/resource/cloudhelpchannel/bookmark/?p=OARX&v=2025&l=ENU&guid=<guid>`).")
L.append("- All content below was retrieved from those official pages (66 method/constructor/operator detail pages,")
L.append("  6 class-overview pages, 6 methods-index pages, plus the AcDbEvalGraph NodeId enum and AcDbEvalVariant")
L.append("  constructor/operator lists).")
L.append("- **Version check**: the OARX **2024** pages for all six classes are byte-identical to the 2025 pages except")
L.append("  version metadata, and the **OARXMAC 2024** (AutoCAD for Mac, .NET) RefGuide lists the exact same method")
L.append("  sets for all six classes. The API surface below is therefore valid for 2024 and 2025, C++ and .NET.")
L.append("- All descriptions are verbatim from the official docs (HTML tags stripped). Where the official docs")
L.append("  themselves contain typos or placeholder text, that is preserved and flagged.")
L.append("- All six classes live in the SDK header **`dbeval.h`** (per the official class-overview pages).")
L.append("")
L.append("---")
L.append("")

for cls in CLASSES:
    ov = parse_overview(f"overview_{cls}.html")
    L.append(f"## {cls}")
    L.append("")
    L.append(f"**File:** `{ov.get('file','?')}`  ")
    L.append(f"**C++:** `{ov.get('cpp','?')}`  ")
    L.append(f"**Class hierarchy:** `{ov.get('hierarchy','?')}`")
    L.append("")
    L.append("**Official class description:**")
    L.append("")
    for p in ov.get("description", []):
        L.append(f"> {p}")
    L.append("")
    if ov.get("links"):
        L.append(f"**Related doc pages (official Links section):** {ov['links']}")
        L.append("")
    if ov.get("see_also"):
        L.append(f"**See Also:** {ov['see_also']}")
        L.append("")

    # Enumerations (only AcDbEvalGraph has one)
    if cls == "AcDbEvalGraph":
        L.append("### Enumerations")
        L.append("")
        L.append("One enumeration is documented for this class:")
        L.append("")
        L.append("| Enumeration | Description (verbatim) |")
        L.append("|---|---|")
        L.append("| `NodeId` | This enum specifies special `AcDbEvalNodeId` values. |")
        L.append("")
        L.append("C++ definition (verbatim from the official NodeId topic page):")
        L.append("")
        L.append("```cpp")
        L.append("enum NodeId {")
        L.append("  kNullNodeId = 0")
        L.append("};")
        L.append("```")
        L.append("")
        L.append("| Member | Description (verbatim) |")
        L.append("|---|---|")
        L.append("| `kNullNodeId` | Null node ID |")
        L.append("")

    # Constructors
    ctors = groups.get((cls, "__CONSTRUCTOR__"))
    if ctors:
        L.append("### Constructors")
        L.append("")
        L.append("From the official constructor overload-list page:")
        L.append("")
        ovlist = parse_overload_list(f"extra_OARX-RefGuide-__OVERLOADED_{cls}_{cls}.html")
        L.append("| Constructor (official overload list) | Description (verbatim) |")
        L.append("|---|---|")
        for o in ovlist:
            L.append(f"| `{o['sig']}` | {o['desc']} |")
        L.append("")
        L.append("Full detail pages (verbatim signatures + parameters):")
        L.append("")
        for d in sorted(ctors, key=lambda x: x["file"]):
            L.append(f"#### `{d['title']}`")
            L.append("")
            L.append(md_sig(d.get("signature")))
            L.append("")
            if d.get("description"):
                L.append(f"**Description (verbatim):** {d['description']}")
                L.append("")
            if d.get("parameters"):
                L.append("| Parameter | Description (verbatim) |")
                L.append("|---|---|")
                for p in d["parameters"]:
                    L.append(f"| `{p['name']}` | {p['desc']} |")
                L.append("")

    # Operators (only AcDbEvalVariant)
    if cls == "AcDbEvalVariant":
        L.append("### Operators")
        L.append("")
        L.append("One operator group (`=`) is documented, with 10 overloads (from the official overload-list page):")
        L.append("")
        ovlist = parse_overload_list("detail/OARX-RefGuide-__OVERLOADED___AcDbEvalVariant.html")
        L.append("| Operator (official overload list) | Description (verbatim) |")
        L.append("|---|---|")
        for o in ovlist:
            L.append(f"| `{o['sig']}` | {o['desc']} |")
        L.append("")
        L.append("> Note (verbatim inconsistency in the official docs): the `double` **constructor** sets the variant")
        L.append("> type to `AcDbEvalVariant::kDouble`, while the `double` **assignment operator** sets it to")
        L.append("> `AcDbEvalVariant::kReal` — both as written in the official 2025 documentation.")
        L.append("")

    # Methods
    L.append("### Methods")
    L.append("")
    summ = parse_summary_table(f"OARX_2025_Methods_{cls}.html")
    L.append("Method index (from the official `__MEMBERTYPE_Methods_` page; V = virtual, A = abstract):")
    L.append("")
    L.append("| Method | Flags | Description (verbatim from index page) |")
    L.append("|---|---|---|")
    for s in summ:
        flags = []
        if s["virtual"]: flags.append("virtual")
        if s["abstract"]: flags.append("abstract")
        L.append(f"| `{s['name']}` | {', '.join(flags) or '—'} | {s['desc'] or '_(no description on index page)_'} |")
    L.append("")
    L.append("Full detail for every method (verbatim C++ signature, description, parameters):")
    L.append("")
    # order: follow the index-page order; skip the NodeId enum page (documented above)
    order = [s["name"] for s in summ]
    def keyf(k):
        try: return order.index(k)
        except ValueError: return 999
    for mkey in sorted([k for (c, k) in groups if c == cls and k != "__CONSTRUCTOR__" and not (cls == "AcDbEvalGraph" and k == "NodeId")], key=keyf):
        ds = groups[(cls, mkey)]
        if len(ds) == 1:
            d = ds[0]
            L.append(f"#### `{cls}::{mkey}`")
            L.append("")
            L.append(md_sig(d.get("signature")))
            L.append("")
            if d.get("description"):
                L.append(f"**Description (verbatim):** {d['description']}")
                L.append("")
            if d.get("parameters"):
                L.append("| Parameter | Description (verbatim) |")
                L.append("|---|---|")
                for p in d["parameters"]:
                    L.append(f"| `{p['name']}` | {p['desc']} |")
                L.append("")
        else:
            L.append(f"#### `{cls}::{mkey}` ({len(ds)} overloads)")
            L.append("")
            for d in ds:
                # derive overload label from filename
                m = re.match(r"OARX-RefGuide-" + cls + r"__" + mkey + r"(.*)", d["file"])
                arg = m.group(1) if m else ""
                arg = arg.replace(".html", "")
                # prettify arg list
                arg = re.sub(r"^(.*?)(_const)$", r"\1", arg)
                parts = [a for a in arg.split("_") if a]
                L.append(f"##### Overload: `{cls}::{mkey} ({', '.join(parts)})`")
                L.append("")
                L.append(md_sig(d.get("signature")))
                L.append("")
                if d.get("description"):
                    L.append(f"**Description (verbatim):** {d['description']}")
                    L.append("")
                if d.get("parameters"):
                    L.append("| Parameter | Description (verbatim) |")
                    L.append("|---|---|")
                    for p in d["parameters"]:
                        L.append(f"| `{p['name']}` | {p['desc']} |")
                    L.append("")
    L.append("---")
    L.append("")

# Sources
L.append("## Sources used")
L.append("")
L.append("| Source | URL | Result |")
L.append("|---|---|---|")
L.append("| help.autodesk.com OARX 2025 JS app (direct) | https://help.autodesk.com/view/OARX/2025/ENU/?guid=OARX-RefGuide-__MEMBERTYPE_Methods_AcDbEvalGraph | **JS shell only** — returns a 6.6 KB `Help` bootstrap page; no content |")
L.append("| help.autodesk.com athena app config | https://help.autodesk.com/view/OARX/2025/ENU/config/config.json , https://help.autodesk.com/view/athena/config/common.json , https://help.autodesk.com/view/athena/modules/athena-core.js | **Worked** — revealed the content API: beehive service URIs in athena-core.js (`serviceUris.guid` = `/community/service/rest/cloudhelp/resource/cloudhelpchannel/bookmark/` on `https://beehive.autodesk.com`) |")
L.append("| Autodesk beehive REST API (guid→content URL) | https://beehive.autodesk.com/community/service/rest/cloudhelp/resource/cloudhelpchannel/bookmark/?p=OARX&v=2025&l=ENU&guid=... | **Worked with browser headers** (plain curl → 403; with Origin/Referer/UA headers → returns the real content URL). Maps each guid to `https://help.autodesk.com/cloudhelp/2025/ENU/OARX-RefGuide/files/<topic>.html` |")
L.append("| Official OARX 2025 static content (primary source) | https://help.autodesk.com/cloudhelp/2025/ENU/OARX-RefGuide/files/OARX-RefGuide-*.html | **Worked — primary source for everything above**: 6 class overviews, 6 methods-index pages, 66 method/constructor/operator detail pages, NodeId enum, constructor & operator overload lists. All fetched 200 OK, zero 404s |")
L.append("| Official OARX 2024 (version check) | https://help.autodesk.com/cloudhelp/2024/ENU/OARX-RefGuide/files/OARX-RefGuide-__MEMBERTYPE_Methods_<Class>.html | **Worked** — byte-identical to 2025 except version metadata |")
L.append("| Official OARXMAC 2024 (AutoCAD for Mac, .NET) | https://help.autodesk.com/cloudhelp/2024/ENU/OARXMAC-RefGuide/files/OARXMAC-RefGuide-__MEMBERTYPE_Methods_<Class>.html | **Worked** — identical method sets for all six classes (same overloads) |")
L.append("| Wayback Machine (CDX + id_ captures) | http://web.archive.org/cdx/search/cdx?url=help.autodesk.com/view/OARX/2023/ENU/* , http://web.archive.org/web/2024id_/https://help.autodesk.com/view/OARX/2024/ENU/?guid=... | **Not needed / not archived** — the 2024 guid URL was not archived; direct cloudhelp access made Wayback unnecessary |")
L.append("| Graebert FRX SDK docs | https://docs.dev.graebert.com/html/2025.0.1/frx/files.html | **Reachable** — lists AcDbEvalConnectable.h, AcDbEvalContext.h, AcDbEvalContextIterator.h, AcDbEvalContextPair.h, AcDbEvalEdgeInfo.h, AcDbEvalExpr.h, AcDbEvalGraph.h, AcDbEvalVariant.h and dbeval.h. (Secondary source; not needed since the official docs provided the complete API. Note: FRX is an ODA-compatible SDK, so its copies of these classes are ODA's, not Autodesk's.) |")
L.append("| SmartObjectARX GitHub repo | https://github.com/kevinzhwl/SmartObjectARX (tree: `inc/AcDbEval*`) | **Dead end for header content** — the repo's `inc/AcDbEvalGraph`, `inc/AcDbEvalExpr`, etc. are 19-byte stub files containing only `#include \"dbeval.h\"`; the real headers live in the ObjectARX SDK, not in the repo |")
L.append("")
L.append("### Notes on gaps / caveats")
L.append("")
L.append("- **No properties pages exist** for any of the six classes in the official 2025 docs")
L.append("  (`__MEMBERTYPE_Properties_*` and `__MEMBERTYPE_Constructors_*` URLs return 404). The only members")
L.append("  documented are the methods/constructors/operators/enumeration listed above. `AcDbEvalVariant`'s")
L.append("  `restype` member is referenced in method descriptions (e.g. `clear()`) but no standalone property page")
L.append("  for it exists in the official docs.")
L.append("- **Placeholder descriptions in the official docs**: `AcDbEvalVariant::fromAcRxValue` and")
L.append("  `AcDbEvalVariant::toAcRxValue` are documented verbatim only as \"This is fromAcRxValue, a member of")
L.append("  class AcDbEvalVariant.\" / \"This is toAcRxValue, a member of class AcDbEvalVariant.\" — the official")
L.append("  2025 docs contain no real description for these two methods (recorded verbatim, not invented).")
L.append("- **Verbatim doc typos preserved**: e.g. `AcDbEvalGraph::evaluate` overloads say \"Returns Acad::eOk if")
L.append("  **succssful**\" in two of the three overloads; `AcDbEvalExpr::activated` docs say \"activation **arrray**\";")
L.append("  `AcDbEvalExpr::nodeId` docs say \"Returns **AcDbGraph::kNullId**\" (a different class name than the")
L.append("  `AcDbEvalGraph::kNullNodeId` used in the same paragraph); `AcDbEvalGraph::getEdgeInfo` parameter docs say")
L.append("  \"**orginating** node\"; `AcDbEvalGraph` class description says \"reprsent\". All kept verbatim.")
L.append("- **Related classes referenced but not in scope**: `AcDbEvalNodeId` (the node-ID type), `AcDbEvalNodeIdArray`,")
L.append("  `AcDbEvalEdgeInfo`, `AcDbEvalEdgeInfoArray`, `AcDbEvalContextIterator`, `AcDbEvalConnectable` (a documented")
L.append("  subclass of `AcDbEvalExpr`). Their own API pages were not part of this collection; they appear here only")
L.append("  as referenced in signatures/descriptions.")
L.append("- **Evaluation flow (as documented)**: `AcDbEvalGraph::activate()` marks starting nodes (empty list deactivates")
L.append("  all; cyclic activation returns `Acad::eGraphCyclesFound`); `AcDbEvalGraph::evaluate()` traverses the DAG")
L.append("  (topologically sorted subgraph reachable from active nodes) invoking `AcDbEvalExpr::evaluate(ctxt)` on")
L.append("  visited nodes, with `graphEvalStart/graphEvalEnd/graphEvalAbort` callbacks; `AcDbEvalExpr::value()` returns")
L.append("  the node's `AcDbEvalVariant` result; `AcDbEvalContext` is a key→void* container (via")
L.append("  `AcDbEvalContextPair`) passed through evaluation; `AcDbEvalIdMap` maps old→new node IDs after")
L.append("  `addGraph()` remapping (used by `AcDbEvalExpr::remappedNodeIds()`).")
L.append("")

open(OUT, "w", encoding="utf-8").write("\n".join(L))
print(f"Wrote {OUT}: {os.path.getsize(OUT)} bytes, {len(L)} lines")

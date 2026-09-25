# Evaluation graph research material

The raw source material behind [`docs/articles/evaluation-graph.md`](../../docs/articles/evaluation-graph.md) and [`docs/articles/objectarx-eval-api.md`](../../docs/articles/objectarx-eval-api.md). Kept for re-verification: the distilled findings live in the docs; this is the verbatim source.

## Contents

### The main research document
- **`lazebny-and-forums.md`** — the consolidated research: verbatim quotes + translations from Lazebny's "Mysteries of Autodesk's Caves" parts 6–12 and the forum threads, 8 full DXF dumps, and a 21-row source table. The single most useful file here.

### Lazebny's "Mysteries of Autodesk's Caves" parts 7–12 (raw)
- **`en_part07.html` … `en_part12.html`** — the English parts (HTML as fetched).
- **`en_part07.txt` … `en_part12.txt`** — the English parts (extracted text).
- **`ru_part07.html` … `ru_part12.html`** — the Russian originals (HTML; **koi8-r** encoded).
- **`ru_part07.txt` … `ru_part12.txt`** — the Russian parts (extracted text).
- **`ru_extracted.txt`** — consolidated extracted Russian text.

### The forum.dwg.ru thread t=24597 (717 posts, raw)
- **`dwg_full_thread.txt`** — all 717 posts extracted (the master record-structure explanation, 96/97 semantics, visibility-parameter dumps, code meanings, evaluation semantics, lookup semantics, version statements, `BLOCKSTRETCHACTION` dumps + `entmod` procedure).
- **`dwg_p1-4.txt`** — a partial (pages 1–4) extraction.
- **`dwg_raw/p01.html` … `p36.html`** — the raw paginated HTML (36 pages).

### The ObjectARX official API capture (raw)
- **`raw/OARX_2025_*.html`** — the 2025 ObjectARX reference pages (Constructors / Methods / Properties for `AcDbEvalContext`, `AcDbEvalContextPair`, `AcDbEvalExpr`, `AcDbEvalGraph`, `AcDbEvalIdMap`, `AcDbEvalVariant`).
- **`raw/overview_*.html`** — the class overview pages.
- **`raw/detail/*.html`** — the individual member (method/property/constructor) detail pages.
- **`raw/extra_*.html`** — the enumeration / operator / overload pages.
- **`raw/v2024/`**, **`raw/v2024mac/`** — the 2024 (Windows + macOS) API pages, used to verify 2024/2025 are identical.
- **`raw/ctor_urls.txt`**, **`raw/detail_urls.txt`**, **`raw/overload_urls.txt`** — the URL lists (what was fetched, for re-fetching).
- **`raw/oarx_2025_shell.html`**, **`raw/guid_evalgraph.json`** — the beehive content-API shell + the `AcDbEvalGraph` guid (the site is a JS app; pages were fetched via the beehive content API).
- **`raw/frx_dbeval.html`**, **`raw/frx_files.html`** — the Graebert FRX SDK pages (ODA's copy of the `AcDbEval*` headers).

### Other
- **`abok_14612.html`** — the forum.abok.ru topic 14612 fetch attempt (a DDoS-Guard 403 page; kept as evidence the source is unreachable).
- **`parsed_details.json`** — the parsed member-detail data (JSON).
- **`*.py`** — the scraping/extraction scripts (`download_all_dwg.py`, `dump_graph.py`, `extract_dwg.py`, `extract_ru.py`, `generate_md.py`, `parse_details.py`), kept for re-running the capture.

## Source URLs

The full source table (with URLs + status) is in the "Research sources" section of [`docs/articles/evaluation-graph.md`](../../docs/articles/evaluation-graph.md). Key sources:

- ObjectARX API reference (Autodesk): https://help.autodesk.com/view/OARX/2025/ENU/?guid=OARX-RefGuide-__MEMBERTYPE_Methods_AcDbEvalGraph
- Lazebny parts 6–12 (EN): http://d107535.00067.h001.peterlink.ru/cad/2009/tainypod06e.htm … tainypod12e.htm
- Lazebny parts (RU, koi8-r): http://poleshchuk.spb.ru/cad/2009/tainypod07.htm …
- forum.dwg.ru: https://forum.dwg.ru/showthread.php?t=24597
- adn-cis.org: https://adn-cis.org/forum/index.php?topic=1069.0
- ezdxf DXF-tags: https://ezdxf.readthedocs.io/en/stable/dxfinternals/dxftags.html

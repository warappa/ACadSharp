#!/usr/bin/env python3
"""Dump the full AcDbEvalGraph + all referenced expressions from a DXF file.

Usage: python3 dump_graph.py <file.dxf>
"""
import sys


def all_objects(path):
    lines = open(path, 'rb').read().decode('utf-8', errors='replace').splitlines()
    start = None
    for i, l in enumerate(lines):
        if l.strip() == 'OBJECTS':
            start = i
            break
    if start is None:
        return {}
    i = start + 2
    objs = {}
    while i < len(lines):
        if lines[i] == '  0':
            name = lines[i + 1].strip()
            if name == 'ENDSEC':
                break
            j = i + 2
            while j < len(lines) and lines[j] != '  0':
                j += 1
            block = lines[i + 2:j]
            pairs = []
            k = 0
            while k + 1 < len(block):
                pairs.append((block[k].strip(), block[k + 1].strip()))
                k += 2
            objs[name] = pairs
            i = j
        else:
            i += 1
    return objs


def obj_info(pairs):
    """Return (handle, class, {code: [values]}) for an object."""
    handle = None
    cls = None
    by_code = {}
    for c, v in pairs:
        if c == '5' and handle is None:
            handle = v
        if c == '100' and cls is None:
            cls = v
        by_code.setdefault(c, []).append(v)
    return handle, cls, by_code


def main(path):
    objs = all_objects(path)
    # map handle -> (class, by_code)
    hmap = {}
    for name, pairs in objs.items():
        if not pairs:
            continue
        h, cls, by = obj_info(pairs)
        if h:
            hmap[h] = (cls, by)

    # find the eval graph
    gpairs = None
    for name, pairs in objs.items():
        if name == 'ACAD_EVALUATION_GRAPH':
            gpairs = pairs
            break
    if gpairs is None:
        print('NO ACAD_EVALUATION_GRAPH in', path)
        return
    _, gcls, gby = obj_info(gpairs)
    v96 = gby.get('96', ['?'])[0]
    v97 = gby.get('97', ['?'])[0]
    print(f'=== {path} ===')
    print(f'graph class={gcls} 96={v96} 97={v97}')

    # parse nodes then edges (linear walk)
    si = None
    for k, (c, v) in enumerate(gpairs):
        if c == '100' and v == 'AcDbEvalGraph':
            si = k
            break
    i = si + 1
    i += 2  # skip 96, 97
    nodes = []
    while i < len(gpairs) and gpairs[i][0] == '91':
        idx = int(gpairs[i][1]); i += 1
        flags = int(gpairs[i][1]); i += 1
        nid = int(gpairs[i][1]); i += 1
        h = gpairs[i][1]; i += 1
        d = [int(gpairs[i + k][1]) for k in range(4)]; i += 4
        nodes.append(dict(idx=idx, flags=flags, id=nid, handle=h, data=d))
    edges = []
    while i < len(gpairs) and gpairs[i][0] == '92':
        idx = int(gpairs[i][1]); i += 1
        flags = int(gpairs[i][1]); i += 1
        tracked = int(gpairs[i][1]); i += 1
        frm = int(gpairs[i][1]); i += 1
        to = int(gpairs[i][1]); i += 1
        d = [int(gpairs[i + k][1]) for k in range(5)]; i += 5
        edges.append(dict(idx=idx, flags=flags, tracked=tracked, frm=frm, to=to, data=d))

    # id -> node
    id2node = {n['id']: n for n in nodes}

    def expr_of(node):
        cls, by = hmap.get(node['handle'], ('?', {}))
        name = by.get('300', ['?'])[0]
        return cls, name, by

    print(f'\n--- {len(nodes)} nodes / {len(edges)} edges ---')
    for n in nodes:
        cls, name, by = expr_of(n)
        line = f"  node {n['idx']:>2} (id {n['id']:>2}, flags {n['flags']:>2}): {cls:28s} name={name!r:20s} data={n['data']}"
        # interesting fields
        extra = []
        if '140' in by:
            extra.append(f"140={by['140']}")
        if '70' in by:
            extra.append(f"70={by['70']}")
        if '1' in by:
            extra.append(f"1={by['1']!r}")
        if '1010' in by:
            extra.append(f"1010-1030=({by['1010'][0]},{by['1010'][1]},{by['1010'][2]})")
        if '1011' in by:
            extra.append(f"1011-1031=({by['1011'][0]},{by['1011'][1]},{by['1011'][2]})")
        if '91' in by and cls != 'AcDbEvalGraph':
            extra.append(f"91={by['91']}")
        if '92' in by and cls != 'AcDbEvalGraph':
            extra.append(f"92={by['92']}")
        if '93' in by and cls != 'AcDbEvalGraph':
            extra.append(f"93={by['93']}")
        if '94' in by:
            extra.append(f"94={by['94']}")
        if '95' in by:
            extra.append(f"95={by['95']}")
        if '96' in by:
            extra.append(f"96={by['96']}")
        if '170' in by:
            extra.append(f"170={by['170']}")
        if '301' in by:
            extra.append(f"301={by['301']!r}")
        if '302' in by:
            extra.append(f"302={by['302']!r}")
        if '303' in by:
            extra.append(f"303={by['303']!r}")
        if '304' in by:
            extra.append(f"304={by['304']!r}")
        if '305' in by:
            extra.append(f"305={by['305']!r}")
        if '1071' in by:
            extra.append(f"1071={by['1071']}")
        if '330' in by:
            extra.append(f"330={by['330']}")
        if extra:
            line += '  ' + ' '.join(extra)
        print(line)

    print(f'\n--- edges ---')
    for e in edges:
        print(f"  E{e['idx']:>2} {e['frm']:>2}->{e['to']:>2}  flags={e['flags']} tracked={e['tracked']}  data={e['data']}")

    # cross-reference connections
    print(f'\n--- connection cross-reference ---')
    for n in nodes:
        cls, name, by = expr_of(n)
        if '170' in by and '91' in by:
            ids = by['91']
            # 171-174: per-connection blocks (id, name)
            names = []
            for code in ('171', '172', '173', '174'):
                if code in by:
                    # each 17x block: 92 id + 303 name (or 91 id + 304 name)
                    pass
            # collect 301-304 names
            conn_names = []
            for code in ('301', '302', '303', '304'):
                if code in by:
                    conn_names.extend(by[code])
            print(f"  param node {n['idx']} ({name!r}): 170={by['170']} ids={ids}")
            for cid, cname in zip(ids, conn_names):
                tgt = id2node.get(int(cid))
                if tgt:
                tc, tn, tb = expr_of(tgt)
                    print(f"      conn id={cid} name={cname!r} -> node {tgt['idx']} {tc} {tn!r}")
                else:
                    print(f"      conn id={cid} name={cname!r} -> (no node with that id)")
        if cls == 'BlockGrip' or 'GRIP' in cls:
            if '91' in by and '92' in by:
                a, b = int(by['91'][0]), int(by['92'][0])
                ta = id2node.get(a); tb = id2node.get(b)
                sa = expr_of(ta)[1] if ta else '?'
                sb = expr_of(tb)[1] if tb else '?'
                print(f"  grip node {n['idx']} ({name!r}): 91={a} -> {sa!r}, 92={b} -> {sb!r}")
        if 'ACTION' in cls and ('301' in by or '302' in by):
            print(f"  action node {n['idx']} ({name!r}): 301={by.get('301')}, 302={by.get('302')}, 92={by.get('92')}, 93={by.get('93')}")
        if 'ACTION' in cls and ('303' in by or '304' in by or '305' in by):
            print(f"  action node {n['idx']} ({name!r}): 303={by.get('303')}, 304={by.get('304')}, 305={by.get('305')}, 94={by.get('94')}, 95={by.get('95')}, 96={by.get('96')}")


if __name__ == '__main__':
    main(sys.argv[1])

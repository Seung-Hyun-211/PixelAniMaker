"""Lists every Korean UI string in the app (C# literals with {n} holes, XAML attribute values and
StringFormats) — the keys the English table must cover. Also used to check coverage:
    python scripts/check-translations.py src/PixelAniMaker.App/Assets/Lang/en.json
Keys reported missing that are only pieces of a joined message are covered by the joined key."""
import re, glob, os, sys, json, html

table_path = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else None

os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src"))
ko = re.compile(r'[\uac00-\ud7a3]')
cs_lit = re.compile(r'(\$?)"((?:[^"\\]|\\.)*)"')
xaml_attr = re.compile(r'(?:Text|Content|Header|ToolTip\.Tip|Title|Watermark)="([^"]*)"')
fmt = re.compile(r"StringFormat='([^']*)'")


def holes(s):
    n = [0]
    def rep(m):
        r = '{%d}' % n[0]
        n[0] += 1
        return r
    return re.sub(r'\{[^{}]*\}', rep, s)


keys = {}
for root in ['PixelAniMaker.App', 'PixelAniMaker.Core']:
    for f in glob.glob(root + '/**/*.*', recursive=True):
        p = f.replace(os.sep, '/')
        if '/obj/' in p or '/bin/' in p or not p.endswith(('.cs', '.axaml')):
            continue
        text = open(f, encoding='utf-8').read()
        if p.endswith('.cs'):
            for line in text.splitlines():
                if line.strip().startswith('//'):
                    continue
                for dollar, s in cs_lit.findall(line):
                    if ko.search(s):
                        s = s.replace('\\n', '\n').replace('\\"', '"')
                        keys.setdefault(holes(s) if dollar else s, p)
        elif p.endswith('.axaml'):
            for s in xaml_attr.findall(text):
                if ko.search(s) and not s.startswith('{'):
                    keys.setdefault(html.unescape(s), p)
            for s in fmt.findall(text):
                if ko.search(s):
                    keys.setdefault(s, p)

if table_path:
    table = json.load(open(table_path, encoding='utf-8'))
    missing = [k for k in keys if k not in table]
    print(f"{len(keys)} keys, {len(missing)} missing")
    for k in missing:
        print(repr(k), keys[k])
else:
    for k, v in keys.items():
        print(json.dumps(k, ensure_ascii=False), '|', v)
    print(len(keys))

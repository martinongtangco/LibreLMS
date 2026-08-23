#!/usr/bin/env python3
"""Render .NET TRX files into a single visual HTML report."""
import glob, html, sys, time
import xml.etree.ElementTree as ET

TRX_GLOB = sys.argv[1] if len(sys.argv) > 1 else "/workspace/tests/*/TestResults/TestResults/dotnet.trx"
OUT = sys.argv[2] if len(sys.argv) > 2 else "/workspace/TestResults/dotnet-report.html"

def duration_ms(el):
    try:
        return int(el.get("duration", "0")) // 10000  # TRX ticks -> ms
    except (TypeError, ValueError):
        return 0

def message_of(res):
    for node in res.iter():
        if node.tag.endswith("Message") and node.text and node.text.strip():
            return node.text.strip()
    for node in res.iter():
        if node.tag.endswith("InfoMessage") and node.text and node.text.strip():
            return node.text.strip()
    return ""

projects = []
for path in sorted(glob.glob(TRX_GLOB)):
    name = path.split("/tests/")[1].split("/")[0]
    root = ET.parse(path).getroot()
    results = []
    for res in root.iter():
        if not res.tag.endswith("UnitTestResult"):
            continue
        outcome = res.get("outcome", "Unknown")
        status = {
            "Passed": "pass", "Failed": "fail",
            "NotExecuted": "skip", "Warning": "skip",
        }.get(outcome, "fail")
        results.append({
            "name": res.get("testName", "?"),
            "status": status,
            "outcome": outcome,
            "ms": duration_ms(res),
            "msg": message_of(res),
        })
    projects.append((name, results))

total = sum(len(r) for _, r in projects)
passed = sum(1 for _, r in projects for t in r if t["status"] == "pass")
failed = sum(1 for _, r in projects for t in r if t["status"] == "fail")
skipped = sum(1 for _, r in projects for t in r if t["status"] == "skip")
badge = "all-green" if failed == 0 else "has-failures"

rows = []
for pname, results in projects:
    p_pass = sum(1 for t in results if t["status"] == "pass")
    p_fail = sum(1 for t in results if t["status"] == "fail")
    p_skip = sum(1 for t in results if t["status"] == "skip")
    rows.append(f"""
    <details {"open" if p_fail else ""}>
      <summary class="proj">
        <span class="dot {'green' if p_fail==0 else 'red'}"></span>
        <strong>{html.escape(pname)}</strong>
        <span class="counts">{p_pass} passed · {p_fail} failed · {p_skip} skipped</span>
      </summary>
      <table>
        <tr><th>Status</th><th>Test</th><th>Duration</th><th>Detail</th></tr>""")
    for t in sorted(results, key=lambda t: (t["status"] != "fail", t["name"])):
        dot = {"pass": "green", "fail": "red", "skip": "yellow"}[t["status"]]
        rows.append(f"""
        <tr>
          <td><span class="dot {dot}"></span>{t["outcome"]}</td>
          <td class="test">{html.escape(t["name"])}</td>
          <td>{t["ms"]} ms</td>
          <td class="msg">{html.escape(t["msg"][:300])}</td>
        </tr>""")
    rows.append("      </table>\n    </details>")

stamp = time.strftime("%Y-%m-%d %H:%M")
page = f"""<!doctype html>
<html><head><meta charset="utf-8"><title>Libre LMS — .NET Test Report</title>
<style>
  body {{ font-family: -apple-system, 'Segoe UI', Roboto, sans-serif; margin: 0; background: #f5f3f0; color: #201e1d; }}
  header {{ background: #201e1d; color: #f5f3f0; padding: 24px 32px; }}
  header h1 {{ margin: 0 0 8px; font-size: 20px; }}
  .summary {{ display: flex; gap: 12px; flex-wrap: wrap; }}
  .pill {{ padding: 6px 14px; border-radius: 999px; font-size: 14px; font-weight: 600; }}
  .pill.pass {{ background: #1d4d2b; color: #a7f3c0; }}
  .pill.fail {{ background: #5c1f1f; color: #ffb4b4; }}
  .pill.skip {{ background: #4d3d1d; color: #ffe08a; }}
  .pill.total {{ background: #3a3634; color: #c9c2ba; }}
  main {{ padding: 24px 32px; }}
  details {{ background: #fff; border-radius: 10px; margin-bottom: 12px; box-shadow: 0 1px 3px rgba(0,0,0,.08); }}
  summary.proj {{ display: flex; align-items: center; gap: 10px; padding: 14px 18px; cursor: pointer; font-size: 15px; }}
  .counts {{ color: #8a8178; font-size: 13px; }}
  .dot {{ width: 10px; height: 10px; border-radius: 50%; display: inline-block; flex: none; }}
  .dot.green {{ background: #2f9e57; }} .dot.red {{ background: #d64545; }} .dot.yellow {{ background: #e0a82e; }}
  table {{ width: 100%; border-collapse: collapse; font-size: 13px; }}
  th {{ text-align: left; padding: 8px 18px; color: #8a8178; border-top: 1px solid #eee; font-weight: 600; }}
  td {{ padding: 7px 18px; border-top: 1px solid #f2f0ed; vertical-align: top; }}
  td.test {{ font-family: ui-monospace, monospace; }}
  td.msg {{ color: #8a8178; font-family: ui-monospace, monospace; font-size: 12px; }}
</style></head>
<body>
<header>
  <h1>Libre LMS — .NET Test Report <small style="opacity:.6">xUnit · master @ {stamp}</small></h1>
  <div class="summary">
    <span class="pill total">{total} total</span>
    <span class="pill pass">✓ {passed} passed</span>
    <span class="pill fail">✗ {failed} failed</span>
    <span class="pill skip">↷ {skipped} skipped</span>
  </div>
</header>
<main>
  {''.join(rows)}
</main>
</body></html>"""

with open(OUT, "w") as f:
    f.write(page)
print(f"wrote {OUT}: {total} tests ({passed} passed, {failed} failed, {skipped} skipped)")

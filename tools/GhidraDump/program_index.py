"""Rebuilds the generated sections of docs/rules/program-index.md.

- by-address: every routine and data row of the topic tables, sorted by address.
- coverage: how many routines of the game code band the index names.

The topic tables are the source; run this after editing them. ApplyIndexNames.java reads the
by-address section.

Usage: python3 -I program_index.py <program-index.md> [<dump directory>]
"""

import collections
import os
import re
import sys

# The game code band (docs/rules/game-code.md).
BAND_START = 0x41DA10
BAND_END = 0x47B739


def replace(text, section, body):
    start, end = f"<!-- {section}:start -->", f"<!-- {section}:end -->"
    head, rest = text.split(start, 1)
    _, tail = rest.split(end, 1)
    return f"{head}{start}\n{body}{end}{tail}"


def topic_rows(text):
    rows, topic = {}, ""
    for line in text.split("\n"):
        if line.startswith("## "):
            topic = ""
        if line.startswith("### "):
            topic = line[4:].strip()
        # A row may list several routines: `0xa`, `0xb` | `NameA`, `NameB`.
        m = re.match(
            r"\| (`0x[0-9a-f]{6}`(?:, `0x[0-9a-f]{6}`)*) \| ((?:`\w+`|—)(?:, `\w+`)*) \| [^|]+\| "
            r"(confirmed|inferred|unknown) \|",
            line,
        )
        if m and topic:
            addresses = re.findall(r"0x[0-9a-f]{6}", m.group(1))
            names = m.group(2).split(", ")
            if len(names) != len(addresses):
                names = [m.group(2)] * len(addresses)
            for address, name in zip(addresses, names):
                rows.setdefault(int(address, 16), (name, m.group(3), topic))
    return rows


def in_band(address):
    return BAND_START <= address <= BAND_END


def by_address(rows):
    lines = ["| Address | Name | Status | Topic |", "| --- | --- | --- | --- |"]
    for address in sorted(rows):
        name, status, topic = rows[address]
        lines.append(f"| `0x{address:06x}` | {name} | {status} | {topic} |")
    return "\n".join(lines) + "\n"


def coverage(rows, dump):
    band = {a: r for a, r in rows.items() if in_band(a)}
    named = sum(1 for r in band.values() if r[0] != "—")
    counts = collections.Counter(r[1] for r in band.values())
    total = ""
    if dump:
        with open(os.path.join(dump, "functions.tsv")) as f:
            functions = sum(1 for line in f.readlines()[1:] if in_band(int(line.split("\t")[0], 16)))
        total = f" of the {functions} functions in the dump's game code band"
    return (
        f"Coverage: the index names {named} routines{total} and lists {len(band) - named} more without a "
        f"name: {counts['confirmed']} confirmed, {counts['inferred']} inferred, {counts['unknown']} unknown.\n"
    )


def main(path, dump=None):
    with open(path) as f:
        text = f.read()
    rows = topic_rows(text)
    text = replace(text, "by-address", by_address(rows))
    text = replace(text, "coverage", coverage(rows, dump))
    with open(path, "w") as f:
        f.write(text)


if __name__ == "__main__":
    main(*sys.argv[1:])

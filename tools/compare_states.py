"""Compare two directories of per-topic final-state JSON (reference vs C#).

    python tools/compare_states.py data/reference/fp3 data/csharp/fp3

Compares parsed JSON, so key order and \\uXXXX escaping don't matter.
Exit code is 1 if anything differs.
"""
import json
import sys
from pathlib import Path


def first_difference(a, b, path="$"):
    """Return a short description of the first place a and b differ, or None."""
    if type(a) is not type(b):
        return f"{path}: type {type(a).__name__} vs {type(b).__name__}"
    if isinstance(a, dict):
        for key in sorted(set(a) | set(b)):
            if key not in a:
                return f"{path}.{key}: missing in reference"
            if key not in b:
                return f"{path}.{key}: missing in C#"
            found = first_difference(a[key], b[key], f"{path}.{key}")
            if found:
                return found
        return None
    if isinstance(a, list):
        if len(a) != len(b):
            return f"{path}: list length {len(a)} vs {len(b)}"
        for i, (x, y) in enumerate(zip(a, b)):
            found = first_difference(x, y, f"{path}[{i}]")
            if found:
                return found
        return None
    return None if a == b else f"{path}: {a!r} vs {b!r}"


def main(reference_dir, csharp_dir):
    ref, cs = Path(reference_dir), Path(csharp_dir)
    ref_topics = {p.stem for p in ref.glob("*.json")}
    cs_topics = {p.stem for p in cs.glob("*.json")}
    failures = 0
    for topic in sorted(ref_topics | cs_topics):
        if topic not in cs_topics:
            print(f"MISSING in C#        {topic}")
            failures += 1
        elif topic not in ref_topics:
            print(f"MISSING in reference {topic}")
            failures += 1
        else:
            a = json.loads((ref / f"{topic}.json").read_text(encoding="utf-8"))
            b = json.loads((cs / f"{topic}.json").read_text(encoding="utf-8"))
            diff = first_difference(a, b)
            if diff:
                print(f"DIFFERENT            {topic}: {diff}")
                failures += 1
    total = len(ref_topics | cs_topics)
    print(f"{total - failures}/{total} topics match")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1], sys.argv[2]))

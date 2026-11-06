"""Reference reducer: merge a session archive's deltas into final per-topic state.

Independent of the C# reducer on purpose; the C# output is compared against this.

    python tools/reference_state.py <archive_dir> <out_dir>
    python tools/reference_state.py race_raw data/reference/race

Skips the compressed .z topics (samples, not deltas). Rules (see CLAUDE.md):
  * dicts deep-merge; any other value replaces
  * {"_deleted": [keys]} removes those keys from the sibling dict
  * a dict delta applied to a list is {"<index>": item_delta}; index == len appends
  * the first message of a topic is applied to {} (so rules also apply to it)
"""
import copy
import json
import re
import sys
from pathlib import Path

LINE = re.compile(r"^(\d+\.)?\d{2}:\d{2}:\d{2}\.\d+(.*)$", re.S)


def merge(state, delta):
    """Return the merged value; dicts and lists are updated in place."""
    if isinstance(state, dict) and isinstance(delta, dict):
        for key, value in delta.items():
            if key == "_deleted" and isinstance(value, list):
                for name in value:
                    state.pop(name, None)
            elif isinstance(value, dict) and isinstance(state.get(key), (dict, list)):
                merge(state[key], value)
            elif isinstance(value, dict):
                state[key] = merge({}, value)
            else:
                state[key] = copy.deepcopy(value)
        return state

    if isinstance(state, list) and isinstance(delta, dict):
        for index_text, item in delta.items():
            index = int(index_text)
            if index < 0 or index > len(state):
                raise ValueError(f"index {index} out of range for list of {len(state)}")
            if index == len(state):
                state.append(merge({}, item) if isinstance(item, dict) else copy.deepcopy(item))
            elif isinstance(item, dict) and isinstance(state[index], (dict, list)):
                merge(state[index], item)
            else:
                state[index] = copy.deepcopy(item)
        return state

    return copy.deepcopy(delta)


def read_topic(path):
    """Yield the parsed payload of every non-blank line (bytes, so \\r\\n and the BOM are explicit)."""
    text = path.read_bytes().decode("utf-8-sig")
    for line in text.split("\r\n"):
        if not line.strip():
            continue
        match = LINE.match(line)
        if not match:
            raise ValueError(f"{path.name}: unparseable line: {line[:80]!r}")
        yield json.loads(match.group(2))


def main(archive_dir, out_dir):
    archive, out = Path(archive_dir), Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    for path in sorted(archive.glob("*.jsonStream")):
        topic = path.name[: -len(".jsonStream")]
        if topic.endswith(".z"):
            continue
        state, count = {}, 0
        for payload in read_topic(path):
            state = merge(state, payload)
            count += 1
        (out / f"{topic}.json").write_text(json.dumps(state, sort_keys=True, indent=1, ensure_ascii=False), encoding="utf-8")
        print(f"{topic:28} {count:7} messages")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])

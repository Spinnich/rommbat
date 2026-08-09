#!/usr/bin/env python3
"""Rewrite OpenAPI 3.1 nullable idioms into the 3.0 form NSwag understands.

RomM serves OpenAPI 3.1, where an optional string is `anyOf: [{type: string},
{type: "null"}]`. NSwag's 3.1 support does not recognise that as nullability and
emits an empty placeholder class per occurrence, so `expires_at` arrives as a
class named `Expires_at4` instead of `string?`.

The pinned schema stays byte-exact; this writes a derived copy that the
generator consumes. Run through generate.sh rather than directly.
"""

import copy
import json
import sys

# Keywords that live on the wrapper alongside anyOf and must survive the collapse.
CARRIED = ("title", "description", "default", "deprecated", "readOnly", "writeOnly")


def collapse_any_of(node: dict) -> dict | None:
    """Collapse `anyOf: [X, {type: null}]` to X marked nullable, or None if it does not match."""
    variants = node.get("anyOf")
    if not isinstance(variants, list):
        return None

    nulls = [v for v in variants if isinstance(v, dict) and v.get("type") == "null"]
    others = [v for v in variants if not (isinstance(v, dict) and v.get("type") == "null")]
    if not nulls or len(others) != 1:
        return None

    collapsed = copy.deepcopy(others[0])
    collapsed["nullable"] = True
    for key in CARRIED:
        if key in node and key not in collapsed:
            collapsed[key] = node[key]
    return collapsed


def collapse_type_array(node: dict) -> None:
    """Rewrite `type: [X, "null"]` in place to `type: X, nullable: true`."""
    kinds = node.get("type")
    if not isinstance(kinds, list):
        return
    remaining = [k for k in kinds if k != "null"]
    if len(remaining) != 1 or len(remaining) == len(kinds):
        return
    node["type"] = remaining[0]
    node["nullable"] = True


def walk(node):
    if isinstance(node, list):
        return [walk(item) for item in node]
    if not isinstance(node, dict):
        return node

    collapsed = collapse_any_of(node)
    if collapsed is not None:
        node = collapsed
    collapse_type_array(node)

    # 3.1 allows a bare `examples` list where 3.0 wants a single `example`.
    if isinstance(node.get("examples"), list) and "example" not in node:
        node.pop("examples")

    return {key: walk(value) for key, value in node.items()}


def main() -> int:
    if len(sys.argv) != 3:
        print(f"usage: {sys.argv[0]} <pinned.json> <normalized.json>", file=sys.stderr)
        return 2

    with open(sys.argv[1], encoding="utf-8") as handle:
        document = json.load(handle)

    document = walk(document)
    document["openapi"] = "3.0.3"

    with open(sys.argv[2], "w", encoding="utf-8") as handle:
        json.dump(document, handle, indent=1)
        handle.write("\n")

    return 0


if __name__ == "__main__":
    sys.exit(main())

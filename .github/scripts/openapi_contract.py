#!/usr/bin/env python3
"""Compares the OpenAPI document generated from the backend code with api/openapi.yaml.

Usage: openapi_contract.py <contract.yaml|json> <generated.json>

api/openapi.yaml is the contract and the generated document (/openapi/v1.json) is what the code
does. The two are written differently (descriptions, examples, schema layout, 3.0 versus 3.1), so
only what a client depends on is compared, per operation:

  - the operations (HTTP method + path) and their operationId
  - the parameters (name + location + whether required)
  - whether the request body is required
  - the response status codes

The content types are only checked in one direction: every content type of the contract must be
offered by the implementation. ASP.NET Core adds text/json and text/plain to every JSON response,
which the contract does not need to list.

Prints every difference and exits with 1 if there is one.
"""

import json
import sys

METHODS = ("get", "put", "post", "delete", "options", "head", "patch", "trace")

# Sign-in with Google is implemented by the backend, but deliberately not part of the contract
# (see the comment on `security` in api/openapi.yaml).
IGNORED_PATH_PREFIXES = ("/api/auth/",)


def load(path):
    with open(path, encoding="utf-8") as f:
        if path.endswith(".json"):
            return json.load(f)
        import yaml  # PyYAML, only needed for the YAML contract

        return yaml.safe_load(f)


def resolve(doc, node):
    """Follows local $refs ('#/components/parameters/Foo')."""
    seen = set()
    while isinstance(node, dict) and "$ref" in node:
        ref = node["$ref"]
        if not ref.startswith("#/") or ref in seen:
            raise ValueError(f"Unsupported or circular $ref: {ref}")
        seen.add(ref)
        node = doc
        for part in ref[2:].split("/"):
            node = node[part.replace("~1", "/").replace("~0", "~")]
    return node


def content_types(doc, node):
    node = resolve(doc, node) or {}
    return sorted(node.get("content", {}))


def summarize(doc):
    """{(METHOD, path): description of what a client depends on}"""
    operations = {}
    for path, item in doc.get("paths", {}).items():
        if path.startswith(IGNORED_PATH_PREFIXES):
            continue
        item = resolve(doc, item)
        for method in METHODS:
            if method not in item:
                continue
            op = item[method]

            # Parameters of the path item apply to all its operations, unless the operation overrides them.
            params = {}
            for raw in item.get("parameters", []) + op.get("parameters", []):
                p = resolve(doc, raw)
                params[(p["in"], p["name"])] = bool(p.get("required", p["in"] == "path"))

            body = resolve(doc, op["requestBody"]) if "requestBody" in op else None
            responses = {
                code: content_types(doc, r)
                for code, r in op.get("responses", {}).items()
            }

            operations[(method.upper(), path)] = {
                "operationId": op.get("operationId"),
                "parameters": params,
                "requestBody": None if body is None else {
                    "required": bool(body.get("required", False)),
                    "contentTypes": sorted(body.get("content", {})),
                },
                "responses": responses,
            }
    return operations


def compare(contract, generated):
    problems = []
    for key in sorted(contract.keys() - generated.keys()):
        problems.append(f"{key[0]} {key[1]}: in the contract, but not implemented")
    for key in sorted(generated.keys() - contract.keys()):
        problems.append(f"{key[0]} {key[1]}: implemented, but not in the contract")

    for key in sorted(contract.keys() & generated.keys()):
        want, got = contract[key], generated[key]
        where = f"{key[0]} {key[1]}"

        if want["operationId"] != got["operationId"]:
            problems.append(f"{where}: operationId is '{got['operationId']}', the contract says '{want['operationId']}'")

        for param in sorted(want["parameters"].keys() - got["parameters"].keys()):
            problems.append(f"{where}: {param[0]} parameter '{param[1]}' is in the contract, but not implemented")
        for param in sorted(got["parameters"].keys() - want["parameters"].keys()):
            problems.append(f"{where}: {param[0]} parameter '{param[1]}' is implemented, but not in the contract")
        for param in sorted(want["parameters"].keys() & got["parameters"].keys()):
            if want["parameters"][param] != got["parameters"][param]:
                problems.append(
                    f"{where}: {param[0]} parameter '{param[1]}' is "
                    f"{'required' if got['parameters'][param] else 'optional'}, the contract says "
                    f"{'required' if want['parameters'][param] else 'optional'}")

        want_body, got_body = want["requestBody"], got["requestBody"]
        if (want_body is None) != (got_body is None):
            problems.append(
                f"{where}: request body is {'expected' if got_body is None else 'unexpected'}, "
                f"the contract says it is {'there' if want_body else 'not there'}")
        elif want_body is not None:
            if want_body["required"] != got_body["required"]:
                problems.append(
                    f"{where}: request body is {'required' if got_body['required'] else 'optional'}, "
                    f"the contract says {'required' if want_body['required'] else 'optional'}")
            missing = sorted(set(want_body["contentTypes"]) - set(got_body["contentTypes"]))
            if missing:
                problems.append(f"{where}: request body does not accept {missing}")

        for code in sorted(want["responses"].keys() - got["responses"].keys()):
            problems.append(f"{where}: response {code} is in the contract, but not implemented")
        for code in sorted(got["responses"].keys() - want["responses"].keys()):
            problems.append(f"{where}: response {code} is implemented, but not in the contract")
        for code in sorted(want["responses"].keys() & got["responses"].keys()):
            missing = sorted(set(want["responses"][code]) - set(got["responses"][code]))
            if missing:
                problems.append(f"{where}: response {code} does not offer {missing}")
    return problems


def main(argv):
    if len(argv) != 3:
        print(__doc__, file=sys.stderr)
        return 2

    contract_doc, generated_doc = load(argv[1]), load(argv[2])
    contract, generated = summarize(contract_doc), summarize(generated_doc)
    problems = compare(contract, generated)

    if problems:
        print(f"{len(problems)} difference(s) between {argv[1]} and the generated OpenAPI document:\n")
        for problem in problems:
            print(f"  - {problem}")
        return 1

    print(f"OK: {len(contract)} operations match.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

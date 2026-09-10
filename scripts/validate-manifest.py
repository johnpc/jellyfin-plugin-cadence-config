#!/usr/bin/env python3
"""Validate manifest.json for the Jellyfin plugin repository.

Structural checks always run. When --new-version/--zip are given, also assert
that the newest manifest entry matches the release being cut and that its
checksum equals the md5 of the freshly built zip.
"""
import argparse
import hashlib
import json
import re
import sys

EXPECTED_GUID = "7d49437c-2e97-430f-b6b7-fa9d58110448"
REQUIRED_TOP_LEVEL = ["guid", "name", "overview", "description", "owner", "category", "versions"]
REQUIRED_VERSION_FIELDS = ["version", "targetAbi", "sourceUrl", "checksum", "timestamp", "changelog"]
CHECKSUM_RE = re.compile(r"^[a-f0-9]{32}$")


def fail(msg):
    print(f"MANIFEST VALIDATION FAILED: {msg}", file=sys.stderr)
    sys.exit(1)


def version_tuple(version):
    parts = version.split(".")
    if len(parts) != 4 or not all(p.isdigit() for p in parts):
        fail(f"version '{version}' is not a numeric 4-tuple")
    return tuple(int(p) for p in parts)


def md5_of(path):
    digest = hashlib.md5()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", nargs="?", default="manifest.json")
    parser.add_argument("--new-version", help="tag being released; must be the newest entry")
    parser.add_argument("--zip", help="built zip; its md5 must equal the newest entry's checksum")
    args = parser.parse_args()

    try:
        with open(args.manifest) as f:
            data = json.load(f)
    except FileNotFoundError:
        fail(f"{args.manifest} not found")
    except json.JSONDecodeError as e:
        fail(f"{args.manifest} is not valid JSON: {e}")

    if not isinstance(data, list) or len(data) != 1:
        fail("manifest must be a JSON array containing exactly one plugin object")
    plugin = data[0]

    for field in REQUIRED_TOP_LEVEL:
        if field not in plugin:
            fail(f"plugin object is missing required field '{field}'")
    if plugin["guid"] != EXPECTED_GUID:
        fail(f"guid is '{plugin['guid']}', expected '{EXPECTED_GUID}'")

    versions = plugin["versions"]
    if not isinstance(versions, list) or not versions:
        fail("'versions' must be a non-empty list")

    tuples = []
    for entry in versions:
        for field in REQUIRED_VERSION_FIELDS:
            if not entry.get(field):
                fail(f"version entry {entry.get('version', '<unknown>')} is missing '{field}'")
        if not CHECKSUM_RE.match(entry["checksum"]):
            fail(f"version {entry['version']} checksum '{entry['checksum']}' is not a lowercase md5")
        tuples.append(version_tuple(entry["version"]))

    for newer, older in zip(tuples, tuples[1:]):
        if newer <= older:
            fail(f"versions must be strictly descending with no duplicates; "
                 f"found {'.'.join(map(str, newer))} before {'.'.join(map(str, older))}")

    newest = versions[0]
    if args.new_version and newest["version"] != args.new_version:
        fail(f"newest entry is {newest['version']}, expected the release tag {args.new_version}")
    if args.zip:
        actual = md5_of(args.zip)
        if actual != newest["checksum"]:
            fail(f"newest entry checksum {newest['checksum']} does not match built zip md5 {actual}")

    print(f"manifest OK: {len(versions)} versions, newest {newest['version']}")


if __name__ == "__main__":
    main()

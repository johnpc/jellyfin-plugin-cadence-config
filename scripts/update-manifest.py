#!/usr/bin/env python3
"""Prepend a new release entry to manifest.json in the repo checkout.

Git is the source of truth for the accumulated version history: this script
never fetches the previous manifest over the network. targetAbi is read from
build.yaml (single source of truth). Existing entries are never rewritten;
the new entry wins only over a same-version duplicate.
"""
import argparse
import datetime
import json
import re
import sys

MANIFEST = "manifest.json"
BUILD_YAML = "build.yaml"
REPO_URL = "https://github.com/johnpc/jellyfin-plugin-cadence-config"

EMPTY_PLUGIN = {
    "guid": "7d49437c-2e97-430f-b6b7-fa9d58110448",
    "name": "CadenceConfig",
    "overview": "Serves the Cadence music client its runtime config and proxies Lidarr requests server-side.",
    "description": "CadenceConfig lets a Jellyfin server operator set the Cadence client's runtime config (marlin search URL, sign-up URL, cast receiver id) once in the plugin dashboard so every client — web and native iOS — is auto-configured at sign-in. It also proxies the client's 'request missing music' calls to Lidarr through a curated allowlist, injecting the Lidarr API key server-side so the write credential never reaches a client.",
    "owner": "johnpc",
    "category": "General",
    "versions": [],
}


def read_target_abi():
    with open(BUILD_YAML) as f:
        match = re.search(r'^targetAbi:\s*"([^"]+)"', f.read(), re.MULTILINE)
    if not match:
        sys.exit(f"ERROR: could not read targetAbi from {BUILD_YAML}")
    return match.group(1)


def load_manifest():
    try:
        with open(MANIFEST) as f:
            data = json.load(f)
    except FileNotFoundError:
        print(f"WARNING: {MANIFEST} not found in the checkout — starting with an "
              "EMPTY version history. If this is not the first release ever, "
              "the accumulated history has been lost; investigate before shipping.",
              file=sys.stderr)
        return [dict(EMPTY_PLUGIN)]
    except json.JSONDecodeError as e:
        sys.exit(f"ERROR: {MANIFEST} exists but is not valid JSON ({e}). "
                 "Refusing to overwrite it — fix the manifest on main first.")
    return data


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", required=True, help="release tag, e.g. 0.0.0.18")
    parser.add_argument("--checksum", required=True, help="md5 of the built zip")
    args = parser.parse_args()

    data = load_manifest()
    plugin = data[0]
    new_entry = {
        "version": args.version,
        "changelog": f"{REPO_URL}/releases/tag/{args.version}",
        "targetAbi": read_target_abi(),
        "sourceUrl": f"{REPO_URL}/releases/download/{args.version}/"
                     f"jellyfin-plugin-cadence-config-{args.version}.zip",
        "checksum": args.checksum,
        "timestamp": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }
    kept = [v for v in plugin.get("versions", []) if v["version"] != args.version]
    plugin["versions"] = sorted(
        [new_entry] + kept,
        key=lambda v: tuple(int(p) for p in v["version"].split(".")),
        reverse=True,
    )

    with open(MANIFEST, "w") as f:
        json.dump(data, f, indent=2)
        f.write("\n")
    print(f"manifest updated: {len(plugin['versions'])} versions, "
          f"newest {plugin['versions'][0]['version']} (targetAbi {new_entry['targetAbi']})")


if __name__ == "__main__":
    main()

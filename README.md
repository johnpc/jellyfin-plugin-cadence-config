# Jellyfin Plugin: CadenceConfig

Configure the [Cadence](https://github.com/johnpc/cadence) music client for **every** user of your
Jellyfin server, from one place — the plugin dashboard. No one has to type URLs into Settings on
their phone, and no secret ever leaves the server.

## What it does

Cadence is a Spotify-like client for a self-hosted Jellyfin server. Some of its features depend on
optional companion services (a Meilisearch/marlin search index, a Lidarr instance for requesting
missing music) that the client needs to be told about. On the web PWA those were injected by the
serving nginx, but the **native iOS app has no nginx** — so those features didn't work there, and
each user would otherwise have to configure URLs by hand.

This plugin fixes that by making the **Jellyfin server itself** the source of that config. Every
client — web and native — already signs in to Jellyfin, so it just asks the server:

- **`GET /Cadence/Config`** (authenticated) → the client's non-secret runtime config:
  - `MarlinUrl` — Meilisearch/marlin base URL for faster search (its `/search` is unauthenticated,
    so no token is needed or sent)
  - `SignupUrl` — optional sign-up link for the sign-in screen
  - `CastReceiverAppId` — optional Google Cast receiver id
  - `LidarrProxy` — a boolean: whether the server's Lidarr request proxy is available
- **`GET|POST /Cadence/Lidarr/{path}`** (authenticated) → proxies "request missing music" calls to
  Lidarr, **injecting the Lidarr API key server-side**. The write-capable key is configured in the
  dashboard and never reaches a client. Restricted to a curated allowlist
  (`search` / `qualityprofile` / `metadataprofile` / `rootfolder` / `queue` / `artist` / `album`,
  GET+POST only) so delete/command/config endpoints stay unreachable.

## Configuration

Install the plugin, then open **Dashboard → Plugins → CadenceConfig** and set:

| Field | Purpose |
|-------|---------|
| Marlin search URL | Meilisearch/marlin base URL, or blank for native Jellyfin search |
| Sign-up URL | Optional registration link shown on the client's sign-in screen |
| Cast receiver app id | Optional custom Google Cast receiver |
| Lidarr URL | Lidarr base URL (e.g. `http://localhost:8686`) |
| Lidarr API key | Lidarr's API key — stays on the server, injected into proxied requests |

The Requests feature turns on automatically once both the Lidarr URL and API key are set.

## Security

- The **Lidarr API key is never sent to a client.** `GET /Cadence/Config` returns only a boolean
  (`LidarrProxy`) indicating the proxy is available; the key is attached to outbound requests inside
  the Jellyfin server process only.
- The Lidarr proxy is **allowlisted** — only the read/add paths the request feature needs are
  reachable; DELETE/PUT and non-allowlisted resources (`command`, `config`, `system`, …) are refused.
- Both endpoints require an authenticated Jellyfin user (`[Authorize]`).
- `MarlinUrl` is a non-secret (marlin's `/search` is unauthenticated by design), so it is safe to
  hand to any authenticated client.

## Install

Add this plugin repository to Jellyfin (**Dashboard → Plugins → Repositories → +**):

```
https://github.com/johnpc/jellyfin-plugin-cadence-config/releases/latest/download/manifest.json
```

Then install **CadenceConfig** from the catalog and restart Jellyfin. (The manifest
is published as a release asset that accumulates every version, so Jellyfin always
sees the newest release — the repo's branch protection blocks committing it to
`main`, so the release asset is the source of truth.)

## Development

```bash
dotnet build --configuration Release /p:TreatWarningsAsErrors=true   # strict build
dotnet format jellyfin-plugin-cadence-config.sln                     # format
dotnet test  Jellyfin.Plugin.CadenceConfig.Tests/...                 # unit tests (≥80% coverage)
bash scripts/check-crap.sh coverage/coverage.json                    # CRAP ≤15
bash scripts/check-line-count.sh 250                                 # ≤250 lines/file
```

The forward/refuse decision for the Lidarr proxy lives in the pure, fully unit-tested
`LidarrProxyPlan` / `LidarrProxyPolicy` / `LidarrUpstream`; the controllers are thin HTTP plumbing.

# Jellyfin Plugin: CadenceConfig

Configure the [Cadence](https://github.com/johnpc/cadence) music client for **every** user of your
Jellyfin server, from one place — the plugin dashboard. No one has to type URLs into Settings on
their phone, and no secret ever leaves the server.

## Scope

This plugin is an **extensions backend**, not a cache. Earlier versions precomputed Home shelves
(`/Cadence/Home`), the audiobook library (`/Cadence/Audiobooks`), and m4b chapters; those are gone
because native Jellyfin 12 queries are now fast enough (0.1–0.9 s cold on a 31k-song library) and
Jellyfin 12 exposes audiobook chapters natively. The plugin now only provides deployment config
(`/Cadence/Config`) and server-side extensions Jellyfin lacks: the Lidarr request proxy, Deezer
playlist import/sync (with Music Grabber fulfillment), generated playlist covers, and the
Storyteller bridge (Whispersync-style audiobook position sync + read-along text).
`/Cadence/Config` still reports `HomeShelves: false` and `Audiobooks: false` so older Cadence
clients stay on their native paths.

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

## Storyteller: Whispersync position sync + read-along

[Storyteller](https://gitlab.com/storyteller-platform/storyteller) aligns an ebook with its
audiobook sentence by sentence. With a Storyteller URL configured, this plugin turns that alignment
into two things for Jellyfin audiobooks:

- **Whispersync-style position sync, for every Jellyfin client.** Whenever Jellyfin saves a user's
  playback position on an audiobook (Cadence, the web UI, Finamp — anything), the plugin converts it to
  the sentence being narrated and writes it to Storyteller, so the Storyteller reader opens there. The
  "Sync Storyteller positions" scheduled task (default every 5 min) pulls the other way: a position
  saved by reading becomes the audiobook's resume point. Newer timestamp wins; the plugin never pushes
  back a position it just pulled.
- **Read-along for the Cadence client.** Cadence fetches the book's sentence timeline and the chapter
  text through the plugin and highlights the sentence being narrated karaoke-style; tapping a sentence
  seeks the audio.

### Configuration

| Field | Purpose |
|-------|---------|
| Storyteller URL | Storyteller base URL (e.g. `http://192.168.7.211:8001`). Blank = feature off. |
| Position sync interval (minutes) | How often Storyteller reading positions are pulled into Jellyfin. Default 5. |
| Linked users | Read-only list of users who connected an account; an admin can remove a link. |

Users connect their own Storyteller account from the Cadence client's Settings. The password is
exchanged once for a Storyteller token that is stored in plugin configuration and only ever used inside
the server. Storyteller positions are per Storyteller user, so the plugin always acts as the linked
user, never as an admin.

### Endpoints (all authenticated, caller-scoped — no `userId` parameter anywhere)

| Endpoint | What it does |
|----------|--------------|
| `GET /Cadence/Config` | …plus `Storyteller: true` when a URL is configured |
| `GET\|POST\|DELETE /Cadence/Storyteller/Link` | The caller's link: `{Linked, Username}`; POST `{Username, Password}` → 200 / 401 bad credentials / 404 not configured / 502 unreachable |
| `GET /Cadence/Storyteller/Books/{itemId}` | `Status` (`unlinked` · `unmatched` · `aligning` · `mismatch` · `ready`), `Parts`, `Chapters`, `Clips` (global seconds ↔ sentence id) |
| `GET /Cadence/Storyteller/Books/{itemId}/Text/{path}` | Streams a chapter/image/css of the aligned publication with the caller's token (Range honoured) |
| `GET\|PUT /Cadence/Storyteller/Books/{itemId}/Position` | The newer of Storyteller's and Jellyfin's position as global seconds; PUT pushes (409 = Storyteller is newer) |

### How the timeline is built

Storyteller serves the aligned book as a Readium Web Publication. The plugin reads its manifest, one
guided-navigation document per chapter (sentence id ↔ audio chunk clip), probes each audio chunk's
length from its MP4 `moov/mvhd` header with small Range requests (no download), and lays the chunks end
to end — the same concatenation Storyteller itself relies on. The result is cached in memory and on
disk (`<cache>/cadence-storyteller/`) per (book, alignment), so a restart doesn't re-probe. If the
aligned audio's total length differs from the Jellyfin book's by more than 3 s the book reports
`mismatch` rather than guessing.

### Security

- The Storyteller token never leaves the server: `/Cadence/Config` only carries a boolean, the link
  endpoint returns the username and a flag, the text proxy attaches the token upstream.
- Everything is scoped to the verified caller (`CallerScope`), so one user can't read another's
  positions or text.
- Chapter text is third-party HTML from the user's own Storyteller; the Cadence client sanitizes it
  before rendering.

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
The Storyteller bridge follows the same shape: `OverlayTimeline`, `PositionConverter`, `BookMatcher`,
`Mp4Duration`, `PushThrottle`, `SyncDecision` and friends are pure and tested; `JellyfinBooks`, the
controllers, the hosted `PositionBridge` and the scheduled task are thin plumbing.

## Testing policy: no ignored failures

There is no such thing as a flaky test. A failing test is a bug — in the test, in the code, or in the
environment — and the only acceptable response is to find the cause and fix it. Concretely: no retry
wrappers in CI, no re-running a red job until it goes green, no skipped or quarantined tests, no
timeouts or sleeps added "to calm things down". If the cause is environmental (a NuGet outage, a
Jellyfin ABI change), fix the environment and make the pipeline fail fast with the real error.
`AGENTS.md` carries the same rule for coding agents.

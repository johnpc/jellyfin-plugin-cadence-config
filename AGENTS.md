# Working in this repo (humans and coding agents)

CadenceConfig is a Jellyfin 12 plugin (`net10.0`) that backs the [Cadence](https://github.com/johnpc/cadence)
client: runtime config, the Lidarr request proxy, Deezer import/sync, playlist covers, and the
Storyteller bridge (Whispersync-style position sync + read-along). Read `README.md` for what each piece
does; this file is about how to change it.

## Shape of the code

- **Thin plumbing, pure logic.** Controllers, scheduled tasks, hosted services and Jellyfin
  library/user-data access are `[ExcludeFromCodeCoverage]` plumbing with no decisions in them. Every
  decision — allowlists, matching, timelines, conversions, "which side wins" — is a small pure or
  injectable class with xunit + FluentAssertions (+ NSubstitute) tests next to it.
- **One type per file, ≤ 250 lines per file** (StyleCop SA1402/SA1649 and `scripts/check-line-count.sh`).
  Split along concepts, never along line counts.
- **Explicit states, not boolean clusters.** A thing with phases is an `enum` with a doc comment per
  member (`LinkState`, `ReadAlongStatus`, `LoginOutcome`, `PutPositionOutcome`, `PushDecision`,
  `SyncOutcome`). Never represent a phase as two nullable/boolean fields that could disagree.
- **Secrets stay server-side.** API keys and Storyteller tokens are used only inside the Jellyfin
  process. Anything returned to a client carries at most a boolean or a display name. Every per-user
  endpoint resolves the caller from the verified token (`CallerScope`); there is no `userId` parameter.
- **Jellyfin ABI.** `build.yaml` `targetAbi` is the single source of truth and must move with every ABI
  change. Match the server's `Jellyfin.Controller`/`Jellyfin.Model` package version.
- **Never edit `manifest.json` by hand** — the release workflow appends to it.

## Gates (run all before pushing; CI runs the same)

```bash
dotnet format jellyfin-plugin-cadence-config.sln --verify-no-changes
dotnet build jellyfin-plugin-cadence-config.sln -c Release /p:TreatWarningsAsErrors=true
dotnet test Jellyfin.Plugin.CadenceConfig.Tests/Jellyfin.Plugin.CadenceConfig.Tests.csproj -c Release --no-build \
  /p:CollectCoverage=true /p:CoverletOutputFormat=json /p:CoverletOutput=../coverage/coverage.json \
  /p:Include="[Jellyfin.Plugin.CadenceConfig]*" /p:ThresholdType=line /p:Threshold=80
bash scripts/check-crap.sh coverage/coverage.json     # CRAP ≤ 15 per method
bash scripts/check-line-count.sh 250
python3 scripts/validate-manifest.py
```

When a gate fails, fix the code — never the gate. Analyzer suppressions need a documented reason in
`.editorconfig` / `jellyfin.ruleset` and are reserved for rules that conflict with the conventions above.

## Testing policy: there is no such thing as a flaky test

A failing test is a bug — in the test, in the code, or in the environment — and must be fixed at its
cause. Not allowed, ever: retry wrappers or re-run-until-green in CI, skipped/quarantined tests,
`Thread.Sleep` or widened timeouts to "stabilize", tests that pass by accident of ordering or shared
state. If the cause is environmental (NuGet, Docker, a Jellyfin ABI change), fix the environment and
make the pipeline fail fast with the real error. If you catch yourself wanting to re-run a red job,
read the failure instead — it is telling you something.

## Commits and PRs

Conventional commits (`feat(storyteller): …`, `fix(config): …`), one coherent change per commit. PRs
describe the behaviour, the wire contract touched, and list the gates run with their results. A
user-visible change in the Cadence client gets a demo artifact (screenshot/recording) in its PR.

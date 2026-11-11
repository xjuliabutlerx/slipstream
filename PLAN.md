# Build Plan

Staged plan for the F1 Session Replay & Strategy Tool (see `PROJECT_IDEA.md` for the idea and architecture). One new technology per stage. Each stage has a deliverable and a "done when" check. If a stage stalls for more than a few sessions, use the fallback and move on.

**Decision (Option A):** Python keeps the working recorder and downloader for capture. The reducer, normalizer and replay engine are built in C# against the sessions already captured. Ingestion is ported to C# last, so the whole data path ends up in one language.

## Stage 0: Housekeeping

- [x] Back up the five captured session files and the archive folders outside the project folder. Live sessions cannot be re-recorded.
- [x] Download the P3, Qualifying and Race archives once `ArchiveStatus` is `Complete`.
- [x] Create the repo with a `.gitignore` that excludes `credentials.json`, `.venv`, raw captures and archive folders.
- [ ] Start a learning log (3 lines per stage: what I learned, what I would do differently).

**Done when:** data is backed up, nothing sensitive is tracked by git.

## Stage 1: C# reducer and decoders

**Build**
- Delta reducer: deep-merge, `_deleted` handling, numeric-key updates to lists, first-message-as-base when the snapshot is empty.
- `CarData.z` / `Position.z` decoders (base64, raw deflate, UTF-8 BOM).
- Session clock from `Utc` fields.

**Learn:** C# basics, records, `System.Text.Json`, xUnit.

**Test fixtures:** the captured sessions, including the empty-snapshot case (Practice 3), the red flag (Qualifying), and VSC / Safety Car / red flag (Race).

**Done when:** the reducer's final state matches the archive's for every session (the Python comparison script is the reference), and tests cover `_deleted`, empty snapshots and keyframes. The red-flag pause moved to Stage 2, since only the replay engine has a notion of time.

**Status: done.** Archive path: C# equals the Python reference for every topic in all five sessions. Live path vs archive: every remaining difference is a capture gap, not a reducer bug (see `CLAUDE.md` Status). Added beyond the original scope: `_kf` keyframe handling (found by the live comparison), an archive reader, a live reader, a CLI, and the Python reference and compare scripts in `tools/`. `ZDecoder` and `SessionClock` were built here and are now used by Stage 2.

## Stage 2: Normalizer and replay engine (C#)

**Build**
- Verify the car-data channel meanings against known values, then fix units. (Done; a FastF1 one-lap cross-check is still open.)
- Session clock from `SessionInfo` (scheduled `StartDate` + `GmtOffset`) and a time window around the session that drops stale data. (Done.)
- Channel-sample schema: `session_time, car, channel, value, unit`, written to Parquet. (Done for `CarData.z` and `Position.z`: `SessionSeconds` as a double; write in 100,000-row groups. Position is a separate `PositionSample` (X, Y, Z in metres, `Status`) with its own normalizer, reader and writer; both sample types share one generic `SampleWindow.Within` through `ISessionTimed`.)
- Live-file adapter and archive adapter feeding one reducer.
- Replay engine: original timing, speed factor, scrub. Skips dead time (gaps with no samples). Red flags (`Aborted` to `Started`) show a brief "red flag" pause and then auto-skip to the restart, with the clock showing real elapsed time.

**Learn:** async streams / `IAsyncEnumerable`, Parquet.Net.

**Done when:** replaying the Race at 60x produces a plausible leaderboard, the live and archive paths give the same output, and the replay shows the red flag as a brief pause and then skips to the restart (`SessionStatus` `Aborted` to `Started`).

**Status: in progress.** Done: channel meanings, `ChannelSample` + `CarDataNormalizer` + `CarDataFile`, `SessionInfo`-based clock and window, the filter, and the Parquet writer. Race check: 4,788,190 of 5,049,880 samples kept, from -00:48:55 to 02:28:39.9, written to a 14.1 MB Parquet file (about 3 s, about 150 MB peak memory with 100,000-row groups; the default row group size used 1.18 GB) and checked in DuckDB. `Position.z` is done the same way: 941,150 rows in 7.4 MB (about 2.1 s), `(0, 0, 0)` placeholders dropped, units decimetres converted to metres, with `Z` dropouts noted in `CLAUDE.md`. Next: first DuckDB lap queries, then the replay engine.

## Stage 3: Analysis exploration (Python notebooks)

**Build:** lap table, stint table, distance-aligned lap comparison with delta line, sector bests, first session brief.

**Learn:** DuckDB over Parquet.

**Done when:** two Qualifying laps can be compared and the brief names the biggest time loss. Later, move the settled analysis into the C# data path.

## Stage 4: Frontend basics

1. TypeScript fundamentals, away from React.
2. React with Vite (Next.js only if a real need appears).
3. Load one precomputed chunk, draw a speed trace with Plotly.js, add play and scrub.
4. Generate TypeScript types from the C# models for the schema boundary.

**Done when:** the browser replays the Qualifying lap from Stage 3.

## Stage 5: First usable product and deploy

**Build:** session picker, lap comparison panel, session brief, jump-to-event navigation. Deploy static files to Vercel.

**Before publishing:** read F1's current terms, decide what derived data is safe to host, and credit sources.

**Done when:** someone else can open the link and compare two laps.

## Stage 6: Strategy

**Build:** fuel-corrected tyre degradation, pit windows, undercut detection, Safety Car / VSC / red-flag handling, then strategist mode and backtesting.

**Done when:** the model reproduces the stops in the Mexico race and I can state one validated result.

## Stage 7: BoxBoxBot panel

**Build:** port the agent logic to TypeScript (Anthropic SDK browser mode, Zod tools, `toolRunner`), BYOK settings screen, and the key-handling safeguards from `PROJECT_IDEA.md`.

**Check first:** the SDK docs for current browser-mode behavior.

**Done when:** it answers "who had the best long-run pace?" from the precomputed data.

## Stage 8: C# SignalR replay hub

**Build:** ASP.NET Core SignalR hub streaming the replay to the browser locally.

**Done when:** the frontend can switch between client-side playback and the hub.

## Stage 9: Port ingestion to C#

**Build:** the sign-in flow (config scrape, PKCE, `pi.flow`, subscription-token claim, token cache), the live SignalR client with the negotiate cookie, and the archive downloader. Add a race-calendar client: FastF1's `f1timing` backend reads the season `Index.json` at `/static/{year}/` (per meeting: `Name`, `Location`, `Country`, `Number`, `Sessions[]` with `Name`, `StartDate`, `GmtOffset`, `Path`). Use it to build archive paths (no hand-built race-day dates) and to decide when to stream live.

**Reference:** `f1_client.py` and FastF1.

**Done when:** the C# recorder captures a full session that matches a Python capture of the same session. Then retire the Python recorder. Fallback: keep the Python recorder.

## Stage 10: Live mode polish

Demo-ready local live mode and a short demo video.

## Working habits

- Keep ingestion behind an adapter, since F1's endpoints can change.
- Use real captured data as test fixtures; do not commit raw data.
- Prefer the simplest option when a new technology blocks progress.

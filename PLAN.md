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
- Replay engine: original timing, speed factor, scrub. Red flags (`Aborted` to `Started`) show a brief "red flag" pause and then auto-skip to the restart, with the clock showing real elapsed time.

**Learn:** async streams / `IAsyncEnumerable`, Parquet.Net.

**Done when:** replaying the Race at 60x produces a plausible leaderboard, the live and archive paths give the same output, and the replay shows the red flag as a brief pause and then skips to the restart (`SessionStatus` `Aborted` to `Started`).

**Status: done** (against the "done when" check; the deferred items below do not block it). Done: channel meanings, `ChannelSample` + `CarDataNormalizer` + `CarDataFile`, `SessionInfo`-based clock and window, the filter, and the Parquet writer. Race check: 4,788,190 of 5,049,880 samples kept, from -00:48:55 to 02:28:39.9, written to a 14.1 MB Parquet file (about 3 s, about 150 MB peak memory with 100,000-row groups; the default row group size used 1.18 GB) and checked in DuckDB. `Position.z` is done the same way: 941,150 rows in 7.4 MB (about 2.1 s), `(0, 0, 0)` placeholders dropped, units decimetres converted to metres, with `Z` dropouts noted in `CLAUDE.md`. Replay engine, first working version: `ReplaySlice.Between` (the samples in `(from, to]` by binary search), `ReplayClock` (play, pause, speed, seek, end), `ReplayEngine.Tick` and the `IAsyncEnumerable` loop `RunAsync` (tested with a fake time provider), and a CLI `replay` mode that prints the Race's first minutes at 60x. Timing path done: `Leaderboard`, `StreamAlignment` (stream start from the `Heartbeat` median, frozen tail excluded), `TimedDelta` and its file reader, `TimingState`, and deltas carried by `ReplayEngine`/`ReplayBatch`; the CLI `replay` mode prints the top 5 every 10 s of session time (checked on the Race at 60x: gaps consistent, lap pace about 82 to 87 s, a VSC-like gap jump near 00:12; 115 tests). Alignment check: the heartbeat payload `Utc` runs a constant 2.05 s ahead of the clock the rest of the feed uses (found by comparing live `SessionStatus` `Utc` with the archive-aligned times, Race and Qualifying); `ReadStreamStart` now subtracts that constant and archive status times match the live `Utc` within 2 to 4 ms. Red flag done: `RedFlagPeriods` (Race 01:06:04 to 01:45:02; Qualifying `Aborted` 00:06:56 to `Started` 00:14:02), the clock holds 3 s of real time at a stoppage and then jumps to the restart, the engine drops samples but keeps timing deltas on the skip tick, `Seek` cancels a hold and clamps to `End`, `TimingState.At` rebuilds the board for a scrub (134 tests). Checked on the Race: the board at the restart shows lap 46 and `PIT` on the top five rows, so the deltas from the stoppage were applied. Dead-time skipping is dropped: there are no sample gaps over 5 s in the Race (apart from one before the session) or in Qualifying. Scrubbing is done: the CLI `replay` mode has keyboard controls (pause, seek, speed) that call `ReplayClock.Seek` and `TimingState.At`, and `ScrubConsistencyTests` proves streaming and scrubbing give the same board (136 tests). Deferred: chunked reading instead of whole sample lists, a final-board check against the Python reference, rewind checkpoints (only if rebuilding becomes too slow: it takes 23 to 135 ms on the Race), and re-checking the 2.05 s heartbeat lead on FP1, FP2 and FP3. First DuckDB lap queries move to Stage 3.

## Stage 3: Analysis exploration (Python notebooks)

**Build** (every step takes a focus driver and an optional reference car, the teammate by default; all cars stay in the data, see the focus-driver rule in `CLAUDE.md`):
1. Lap table for all drivers: lap number, lap time, sector times, compound, tyre age, in-lap and out-lap flags. This also settles what a lap and a stint are in this data.
2. Stint table from `TyreStintSeries` and `CurrentTyres`.
3. Distance-aligned comparison of any two laps with a delta-time line, defaulting to the focus driver against the teammate. The car data has no distance channel, so distance is built by integrating speed or from the `X`/`Y` track coordinates; this is the main technical question of the stage.
4. Sector bests and ideal lap for the focus driver and the reference.
5. A session brief relative to the focus driver: biggest time loss against the reference, pace ranking.
Also settle along the way: the FastF1 one-lap cross-check of the car-data channels and `Position` units, and how to treat `Z == 0` dropouts.

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

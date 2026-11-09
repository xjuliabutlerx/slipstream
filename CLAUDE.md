# Slipstream (working name)

Unofficial F1 session replay and strategy tool built on F1's public live-timing feed and static archive. The idea is in `PROJECT_IDEA.md`; the staged build plan is in `PLAN.md`. Read both before suggesting architecture changes.

The folder may still be named `new-f1-project`; the user plans to rename it to `slipstream` (this breaks `.venv`, so it must be rebuilt from `requirements.txt`).

## About the user

- Learning most of this stack as they build. Comfortable with Python; new to C#, TypeScript, React at scale, Parquet/DuckDB, Vercel, and the Anthropic TypeScript SDK. Also uses Java day to day: map new C#/TS concepts to Java and Python equivalents (JUnit, Jackson, Maven, pytest) and call out where they differ.
- Wants step-by-step plans, one new technology at a time, with clear "done when" checks. Prefers to write the code themselves from hints; work test-first (red, then green), and ask of each test "what wrong implementation would still pass this?".
- Use they/them for the user.

## Decisions made

- **Option A:** Python keeps the working recorder and downloader (`f1_client.py`) for capture. The reducer, normalizer and replay engine are built in **C#** against the captured sessions. Ingestion (sign-in, live client, downloader) is ported to C# last. Python also stays for notebooks and FastF1 cross-checks.
- Fallback: build the reducer in Python first and port, if C# stalls.
- Frontend: TypeScript + React. Recommended Vite over Next.js unless a real need appears (user's doc says Next.js; not final). Plotly.js or D3 for charts.
- BoxBoxBot panel is a TypeScript agent in the browser, bring-your-own-key. See `PROJECT_IDEA.md` for the key-handling safeguards.
- Name: "Slipstream" (the user suggested it; availability on GitHub/npm/NuGet not yet checked). Avoid "F1"/"Formula 1" in the display name (trademark); describe it as "unofficial".

## Files

- `f1_client.py`: auth, historical fetch, live SignalR client, recorder, session downloader. Commands:
  - `python f1_client.py history <year> "<race-day date> <Weekend Name>" "<session date> <Session>"`
  - `python f1_client.py record <out.jsonl>` (lossless live capture; appends, so rename old files first)
  - `python f1_client.py download <year> "<weekend>" "<session>" <out_dir>`
  - `python f1_client.py live <out.txt>` (older, lossy)
- Needs env vars `F1_USERNAME` and `F1_PASSWORD` (F1 account with an active F1TV subscription). **Never read or print `credentials.json` or the token cache.**
- Captures (raw live, JSONL): `mexico_fp1_data.jsonl` (started 15 min late), `mexico_fp2_data.jsonl` (post-session snapshot only), `mexico_fp3_data.jsonl`, `mexico_qualifying_data.jsonl`, `mexico_race_data.jsonl`.
- Archive downloads: `fp1_raw/`, `fp2_raw/`, `fp3_raw/`, `qualifying_raw/`, `race_raw/`.
- `.gitignore` excludes secrets, all raw data/archives, derived Parquet/DuckDB, and `data/` (generated comparison output). Git repo with remote `origin` (`xjuliabutlerx/slipstream` on GitHub).
- Do not commit or redistribute raw F1 data. Tests use synthetic payloads, never real captured lines.

**C# solution (`Slipstream.slnx`, .NET 10, xUnit):**
- `src/Slipstream.Core/` (class library):
  - `ZDecoder`: `.z` payload (base64, raw deflate via `DeflateStream`, UTF-8 JSON) to `JsonNode`.
  - `Reducer.Apply(state, delta)`: delta merge; mutates objects in place, returns the result. Rules: deep-merge objects; `_deleted` removes sibling keys; `_kf` is never stored; a dict delta on a list is `{"<index>": item_delta}` (index == Count appends, beyond that throws `InvalidDataException`); new keys are built by merging into an empty object so the rules apply to first messages too.
  - `SessionReducer`: state per topic. `Feed(topic, payload)`; a payload with `_kf: true` is a keyframe and **replaces** the topic's state instead of merging. `GetState` returns the live object.
  - `TimestampParser`: `Parse` (archive offset `[D.]HH:MM:SS.mmm` to `TimeSpan`) and `ParseUtc` (absolute `Utc`, 6 or 7 fraction digits).
  - `ArchiveLineParser` / `ArchiveReader`: archive `.jsonStream` lines and files (lazy `IEnumerable`).
  - `LiveReader`: `ParseLine` (one captured line to messages; usable for a socket later) and `ReadFile` (JSONL capture). A snapshot yields one message per topic with null `Utc`; `.z` payloads pass through as strings.
  - `FeedMessage`, `ArchiveLine`, `SessionClock(StartUtc)`, `ChannelSample(SessionTime, Car, Channel, Value, Unit)` (records). `ChannelSample.Value` is a `double` for every channel.
  - `CarDataNormalizer.Normalize(payload, clock)`: decoded `CarData.z` document to `ChannelSample`s. Channel names/units: `0` rpm, `2` speed km/h, `3` n_gear, `4` throttle %, `5` brake on/off. Throttle is capped at 100; brake is binary (raw > 0 gives 1). Unknown channels are skipped, not an error. Session time is `Utc` minus the clock start.
  - `CarDataFile.Read(path, clock)`: `ArchiveReader` + `ZDecoder` + `CarDataNormalizer`, lazy.
  - `SessionWindow`: `ClockFrom(sessionInfo)` (the clock starts at the scheduled `StartDate` + `GmtOffset`, as UTC) and `WindowFrom(sessionInfo, preRoll, postRoll)` (UTC range: start minus preRoll, `EndDate` plus postRoll). A missing `StartDate`, `EndDate` or `GmtOffset` throws `FormatException` (no default offset: a wrong offset would silently shift everything). `GmtOffset` may start with `+`, which `TimeSpan.Parse` rejects, so it is trimmed.
  - `SampleWindow.Within(samples, from, to)`: keeps samples whose session time is in `[from, to]` (both ends inclusive); negative session times are valid (pre-roll).
- `src/Slipstream.Cli/`: `dotnet run --project src/Slipstream.Cli -c Release -- <archive|live> <input> <outDir>` writes each topic's final state as JSON (skips `.z` topics). `carData <archive_dir> <outDir>` reduces `SessionInfo.jsonStream`, builds the clock and window (90 min pre-roll, 30 min post-roll), and prints the sample count plus first and last sample (`outDir` is unused for now).
- `tests/Slipstream.Core.Tests/`: xUnit tests (58 at last count); run with `dotnet test`. `TestData` builds synthetic `.z` payloads (`EncodeZ`), car-data JSON and archive lines; `TempFile` writes a BOM + CRLF file and deletes it on dispose.
- `tools/reference_state.py <archive_dir> <out_dir>`: independent Python reducer, the reference answer. `tools/compare_states.py <ref_dir> <other_dir>`: compares per-topic state as parsed JSON and reports the first difference per topic.
- Typical check: `python3 -I tools/reference_state.py race_raw data/reference/race`, then the CLI in `archive` mode to `data/csharp/race`, then compare. `live` mode output goes to `data/csharp-live/<session>`.

## How the F1 endpoints work

**Auth (token = F1TV subscription JWT):** scrape `clientId` and `acrValues` from `https://account.formula1.com/scripts/main.min.js` (`davinci:{...}` object) -> `GET id.formula1.com/as/authorize` (`response_mode=pi.flow`, PKCE S256, header `X-Requested-With: ping-sdk`) -> `POST` credentials to the returned `_links.next.href` (must start with `https://id.formula1.com/`) -> `POST /as/token` with the code -> decode the access token (no signature check) and take the `subscriptionToken` claim. No refresh token; sign in again near expiry (renew 5 min early). Reference: `fastf1/internals/f1account/` in the venv.

**Live:** `wss://livetiming.formula1.com/signalrcore` (SignalR Core). Pre-negotiate with an HTTP `OPTIONS` to `.../signalrcore/negotiate` to get the `AWSALBCORS` cookie and send it as a `Cookie` header. Token goes via `access_token_factory`. Call `Subscribe` with a topic list; updates arrive as `feed` invocations `[topic, payload, utc]`; the `Subscribe` reply (a `CompletionMessage`) holds a snapshot dict. Python uses `signalrcore`; C# should use the .NET SignalR client.

**Archive:** `https://livetiming.formula1.com/static/{year}/{race-day date}_{Weekend_Name}/{session date}_{Session}/{Page}`. The weekend folder uses the **race-day date**, not the session date. `SessionInfo.Path` gives the exact path. Spaces become underscores. Missing paths return **403**, not 404. Headers used: `User-Agent: BestHTTP`; Bearer token attached. `Index.json` lists the session's feeds. Whether the archive requires the token is untested. Wait until `ArchiveStatus` is `Complete` before downloading.

## Data format facts (verified on the Mexico City 2026 weekend)

- Archive `.jsonStream` files: UTF-8 BOM, lines separated by `\r\n`, each line is `<timestamp><payload>`. Timestamp is `[D.]HH:MM:SS.mmm`, relative to **stream start**, and gains a day prefix after 24 h (the race archive does). Do not assume 12 characters.
- `.z` topics (`CarData.z`, `Position.z`): payload is a JSON-quoted base64 string; decode with `zlib.decompress(base64.b64decode(s), -zlib.MAX_WBITS)` then UTF-8-sig JSON. Live and archive match.
  - `CarData.z`: `{"Entries":[{"Utc","Cars":{"<num>":{"Channels":{...}}}}]}`. Only channels `0,2,3,4,5` appear: `0` rpm (0 to 13,616), `2` speed km/h (max 362), `3` gear (0 = neutral, max 8), `4` throttle %, `5` brake. Meanings match another provider's published field list (not F1's docs) and the value ranges; not yet cross-checked against FastF1 on one lap. Throttle reaches 104 and brake takes only 0, 100 or 104, so cap throttle at 100 and treat brake as on/off. **No DRS channel.** Sample spacing is irregular (about 0.16 to 0.36 s, roughly 0.2 s average), so never assume a fixed rate. The Race holds 5,049,880 channel samples (1,009,976 per channel).
  - `Position.z`: `{"Position":[{"Timestamp","Entries":{"<num>":{"Status","X","Y","Z"}}}]}`. `Status` was always `OnTrack`. Units unknown.
- Plain topics are **deltas**: deep-merge into state. Also handle `"_deleted": [...]` (remove keys) and numeric-key dicts updating lists by index (e.g. `TimingAppData` `Stints`: state is a list, delta is `{"0": {...}}`; a delta list or scalar replaces). Merging archive deltas reproduced the live snapshot for 21 of 27 feeds in Practice 2.
- **`_kf: true` marks a keyframe**: a complete state for that topic, not a delta. It appears in live snapshots and in live feed lines (mid-stream, repeatedly on small topics: `Heartbeat`, `WeatherData`, `TlaRcm`, `SessionStatus`, `TrackStatus`, ...). The archive never contains `_kf`. Handling: never store the key; a keyframe replaces the topic's state. Checked on the live captures: replace-on-keyframe changes nothing in the Race, Qualifying or FP1, and in FP3 it fixes `PitLaneTimeCollection` (merge left stale cars 11 and 5; the archive and replace both give an empty `PitTimes`). One topic in one session, so keep watching for a topic moving away from the archive.
- **Snapshot is unreliable:** empty in Practice 3, missing pit data in Practice 2. Treat the first message per topic as base state (merge the first message into `{}`). FP2's capture is two snapshots taken the next day (its `Heartbeat` is Oct 31 for an Oct 30 session), so it cannot be compared with its archive.
- Live JSONL line shapes: `{"kind":"snapshot","result":{topic:state,...},...}` (one per file; two in FP2; `result` is `{}` in FP3) and `{"kind":"feed","data":[topic,payload,utc],"t_recv":...}`. `.z` topics have a base64 string payload. No other kinds, no snapshot errors.
- Values are mostly strings (lap times, positions, weather, car numbers). `New` is a string in stint feeds but a boolean in `CurrentTyres`. Empty string means no value yet.
- Live messages carry absolute UTC. **The session clock is anchored on the scheduled start from `SessionInfo`** (`StartDate` local time plus `GmtOffset`, e.g. Race `2026-11-01T14:00:00` at `-06:00:00` is 20:00:00Z), not on the first `Utc` seen. `SessionInfo` also has `EndDate` and `Path`. The scheduled start is not the real one (the Race's real `Started` was 20:03:25Z, 3 min 25 s late), so session time 0 is "scheduled start"; real `Started`/`Aborted`/... times from `StatusSeries` are events on the same axis. In the archive, line offsets are noisy (about 100 ms jitter against the `Utc` inside `Heartbeat`) and **freeze at the end of the race file** (`1.05:12:29.107` repeated while `Utc` keeps advancing). Prefer `Utc` over offsets. `Utc` has 6 or 7 fraction digits.
- `SessionStatus` is not a safe clock anchor: `Started` occurs several times (Qualifying 4, Race 3 with an `Aborted` between the last two, FP3 once), a late capture may never see one (FP1), archive status lines carry no `Utc`, and the race archive begins with a stale `Started` about 26 h before the race. That stale block is the start of **Practice 3** (the first `CarData.z` sample is 2026-10-31T17:32:49Z, Practice 3 was scheduled for 17:30Z); the race archive's car data then jumps to race day at 19:11Z, so a first-`Utc` anchor gave session times over 26 h. `SessionData.StatusSeries` carries a `Utc` per status change (including the stale ones) and is the source for events such as the red flag (Race: `Aborted` 21:06:02Z, `Started` 21:45:00Z). `ExtrapolatedClock` (`Utc`, `Remaining`, `Extrapolating`) is the broadcast session timer.
- Practice 3's first ~14 min had `SessionInfo` describing the Sunday race (F1-side glitch); `TopThree`/`LapSeries` were absent until it corrected.
- Qualifying: one stream, parts shown via `SessionStatus` cycles, `RaceControlMessages` ("Q2 WILL START AT ..."), top-level `SessionPart`/`CutOffTime` in `TimingData`, per-driver `KnockedOut`/`Cutoff`/`Stats`/`BestLapTimes`. Had a red flag and restart.
- Race: 71 laps; VSC, three Safety Car periods, a red flag (~39 min stoppage). `LapCount` (`{"CurrentLap","TotalLaps"}`) is race-only.
- Race-only archive feeds missed in the live captures: `OvertakeSeries`, `PitStop`, `PitStopSeries` (now in `TOPICS`; not yet confirmed subscribable live). `PitStop` rows can lack `Lap`. Two more race archive feeds are not in `TOPICS` and not in any live capture: `ChampionshipPrediction`, `DriverRaceInfo` (28,978 messages in the race).
- `TimingDataF1` and `TimingData` have equal final state in the FP3, Qualifying and Race archives, but the race archive has different message counts (68,291 against 39,317), so they are not byte-identical there. Can still be dropped. `TimingDataF1` is absent from the live Race, Qualifying and FP1 captures.
- `TeamRadio` carries only metadata and `.mp3` paths, only for selected clips (28 in the race, 8 cars, none for car 63). Audio content is not in the feed.
- Rough size: about 10 MB/hour of live capture; the race file is 39 MB.

Topics seen live: Heartbeat, DriverList, ExtrapolatedClock, RaceControlMessages, SessionInfo, SessionStatus, SessionData, TeamRadio, TimingAppData, TimingData, TimingDataF1, TimingStats, TopThree, TrackStatus, WeatherData, WeatherDataSeries, Position.z, CarData.z, TyreStintSeries, CurrentTyres, LapSeries, DriverTracker, PitLaneTimeCollection, TlaRcm, ArchiveStatus, ContentStreams, AudioStreams, LapCount (race only).

## Known gotchas

- The recorder opens files in append mode: rename the previous capture before starting a new one.
- Recorder prints nothing until a message arrives (every 30 s after that).
- `fetch_page` once cut timestamps at 12 characters; fixed with a regex. Keep any new parser flexible.
- Reading archive files in Python text mode converts `\r\n`; open with `newline=""` or read bytes.
- Rate limiting, mirror fallback and reconnects are not implemented in `f1_client.py` (FastF1 has them).

## Status

Stage 0 mostly done: archives downloaded, repo created with `.gitignore`, `requirements.txt` made. Remaining there (user to confirm): back up captures and archives, start the learning log.

Stage 1 done (C# reducer, decoders, readers, CLI, Python reference). Archive path: the C# final state equals the Python reference for every topic in all five sessions. Live path vs archive, with `_kf` handled: FP3 22/25, Qualifying 21/25, Race 23/31, FP1 11/25, FP2 not comparable. Every remaining difference is outside the reducer: the live capture ended before the archive (`Heartbeat`, last `SessionStatus` `Finalised` vs `Ends`, one fewer `StatusSeries` entry), `ArchiveStatus` was `Generating` at capture time, topics the recorder never subscribed to, and FP1's late start with a shorter topic list. The red-flag **pause** is a replay-engine concern (Stage 2), not a reducer one. 

Stage 2 in progress. Done: car-data channel meanings (see Data format facts), `ChannelSample`, `CarDataNormalizer`, `CarDataFile`, test helpers, the `SessionInfo`-based clock and window, `SampleWindow`, and the CLI `carData` mode. Check on the Race archive: 4,788,190 of 5,049,880 samples fall inside the window (90 min before the scheduled start to 30 min after the scheduled end); first sample at session time -00:48:55, last at 02:28:39.9. The 261,690 dropped samples are the stale Practice 3 block.

Next: write the samples to Parquet (Parquet.Net), then Position.z (units still unknown), then the replay engine with `IAsyncEnumerable`.

Replay design decisions: skip dead time (gaps with no samples). A red-flag period (`SessionStatus` `Aborted` to `Started`) is a session event, not a gap: the UI shows a "red flag" pause for a short moment, then auto-skips to the restart while the clock keeps showing the real elapsed time. The data during the stoppage stays available when scrubbing.

## Open questions

- Cross-check car-data channels against FastF1 on one lap; verify `Position` units.
- Confirm whether the static archive (and the season `Index.json` at `/static/{year}/`, which FastF1's `f1timing` backend uses for the calendar: `Sessions[]` with `Name`, `StartDate`, `GmtOffset`, `Path`) needs the Bearer token.
- Pre-roll/post-roll values (90/30 min) are a first guess; check them on the other four sessions (Qualifying and Practice may have different stale or late data).
- A session with a late or delayed start (FP1) is anchored on the schedule, not the real start; check this on the live captures.
- Confirm the five archive-only race feeds are subscribable live (`OvertakeSeries`, `PitStop`, `PitStopSeries`, `ChampionshipPrediction`, `DriverRaceInfo`), and whether `TimingDataF1` is worth subscribing to at all.
- Read F1's current terms before publishing; hosting derived chunks on a public CDN may count as redistribution.
- Name availability for "Slipstream".

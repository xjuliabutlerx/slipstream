# Slipstream (working name)

Unofficial F1 session replay and strategy tool built on F1's public live-timing feed and static archive. The idea is in `PROJECT_IDEA.md`; the staged build plan is in `PLAN.md`. Read both before suggesting architecture changes.

The folder may still be named `new-f1-project`; the user plans to rename it to `slipstream` (this breaks `.venv`, so it must be rebuilt from `requirements.txt`).

## About the user

- Learning most of this stack as they build. Comfortable with Python; new to C#, TypeScript, React at scale, Parquet/DuckDB, Vercel, and the Anthropic TypeScript SDK.
- Wants step-by-step plans, one new technology at a time, with clear "done when" checks.
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
- `.gitignore` excludes secrets, all raw data/archives and derived Parquet/DuckDB. Not yet a git repo.
- Do not commit or redistribute raw F1 data.

## How the F1 endpoints work

**Auth (token = F1TV subscription JWT):** scrape `clientId` and `acrValues` from `https://account.formula1.com/scripts/main.min.js` (`davinci:{...}` object) -> `GET id.formula1.com/as/authorize` (`response_mode=pi.flow`, PKCE S256, header `X-Requested-With: ping-sdk`) -> `POST` credentials to the returned `_links.next.href` (must start with `https://id.formula1.com/`) -> `POST /as/token` with the code -> decode the access token (no signature check) and take the `subscriptionToken` claim. No refresh token; sign in again near expiry (renew 5 min early). Reference: `fastf1/internals/f1account/` in the venv.

**Live:** `wss://livetiming.formula1.com/signalrcore` (SignalR Core). Pre-negotiate with an HTTP `OPTIONS` to `.../signalrcore/negotiate` to get the `AWSALBCORS` cookie and send it as a `Cookie` header. Token goes via `access_token_factory`. Call `Subscribe` with a topic list; updates arrive as `feed` invocations `[topic, payload, utc]`; the `Subscribe` reply (a `CompletionMessage`) holds a snapshot dict. Python uses `signalrcore`; C# should use the .NET SignalR client.

**Archive:** `https://livetiming.formula1.com/static/{year}/{race-day date}_{Weekend_Name}/{session date}_{Session}/{Page}`. The weekend folder uses the **race-day date**, not the session date. `SessionInfo.Path` gives the exact path. Spaces become underscores. Missing paths return **403**, not 404. Headers used: `User-Agent: BestHTTP`; Bearer token attached. `Index.json` lists the session's feeds. Whether the archive requires the token is untested. Wait until `ArchiveStatus` is `Complete` before downloading.

## Data format facts (verified on the Mexico City 2026 weekend)

- Archive `.jsonStream` files: UTF-8 BOM, lines separated by `\r\n`, each line is `<timestamp><payload>`. Timestamp is `[D.]HH:MM:SS.mmm`, relative to **stream start**, and gains a day prefix after 24 h (the race archive does). Do not assume 12 characters.
- `.z` topics (`CarData.z`, `Position.z`): payload is a JSON-quoted base64 string; decode with `zlib.decompress(base64.b64decode(s), -zlib.MAX_WBITS)` then UTF-8-sig JSON. Live and archive match.
  - `CarData.z`: `{"Entries":[{"Utc","Cars":{"<num>":{"Channels":{...}}}}]}`. Only channels `0,2,3,4,5` appear (look like RPM, speed km/h, gear, throttle, brake; **meanings not verified**). **No DRS channel.**
  - `Position.z`: `{"Position":[{"Timestamp","Entries":{"<num>":{"Status","X","Y","Z"}}}]}`. `Status` was always `OnTrack`. Units unknown.
- Plain topics are **deltas**: deep-merge into state. Also handle `"_deleted": [...]` (remove keys) and numeric-key dicts updating lists by index. Merging archive deltas reproduced the live snapshot for 21 of 27 feeds in Practice 2. `_kf: true` appears on full-state messages of small topics.
- **Snapshot is unreliable:** empty in Practice 3, missing pit data in Practice 2. Treat the first message per topic as base state.
- Values are mostly strings (lap times, positions, weather, car numbers). `New` is a string in stint feeds but a boolean in `CurrentTyres`. Empty string means no value yet.
- Live messages carry absolute UTC; build the session clock from `Utc` fields.
- Practice 3's first ~14 min had `SessionInfo` describing the Sunday race (F1-side glitch); `TopThree`/`LapSeries` were absent until it corrected.
- Qualifying: one stream, parts shown via `SessionStatus` cycles, `RaceControlMessages` ("Q2 WILL START AT ..."), top-level `SessionPart`/`CutOffTime` in `TimingData`, per-driver `KnockedOut`/`Cutoff`/`Stats`/`BestLapTimes`. Had a red flag and restart.
- Race: 71 laps; VSC, three Safety Car periods, a red flag (~39 min stoppage). `LapCount` (`{"CurrentLap","TotalLaps"}`) is race-only.
- Race-only archive feeds missed in the live captures: `OvertakeSeries`, `PitStop`, `PitStopSeries` (now in `TOPICS`; not yet confirmed subscribable live). `PitStop` rows can lack `Lap`.
- `TimingDataF1` is byte-identical to `TimingData` in the archive; live is near-identical. Can be dropped.
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

Stage 0 of `PLAN.md` in progress: archives downloaded, `.gitignore` written, `requirements.txt` made. Remaining: back up captures and archives, rename folder, `git init`, create the GitHub repo (description drafted), start the learning log. Next: Stage 1, the C# reducer and decoders, tested against the captured sessions (empty snapshot in FP3, red flag in Qualifying, VSC/SC/red flag in the Race) and checked against the archives.

## Open questions

- Verify car-data channel meanings and `Position` units.
- Confirm whether the static archive needs the Bearer token.
- Confirm the three race-only feeds are subscribable live.
- Read F1's current terms before publishing; hosting derived chunks on a public CDN may count as redistribution.
- Name availability for "Slipstream".

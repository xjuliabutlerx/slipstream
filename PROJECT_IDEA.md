# F1 Session Replay & Strategy Tool

> Status: exploratory idea. No repository yet.

## Summary

A web application that replays historical F1 session data through a stream modeled on how team telemetry systems are structured (named channels, units, sample rates, timestamps against a session clock), and layers strategy and driver analysis on top. It is built entirely on free public data and is intended as a portfolio project that also demonstrates real-time data engineering skills relevant to motorsport software roles.

This is not primarily a chatbot. It is a configurable, multi-panel decision-support tool. My existing project, BoxBoxBot, gets folded into the dashboard as an assistant panel rather than being the whole product.

## Goals

- Build something that feels close to a team-style telemetry and strategy system, using only public data.
- Practice a team-relevant tech stack (C#, TypeScript, Python) while shipping a working public demo.
- Provide useful quick-turnaround analysis, like what a driver or strategist would want between sessions.
- Stay free to build and host, with no shared LLM costs (users bring their own API key).

## Learning Along the Way

Most of this stack is new to me, so this project is as much a learning plan as a build plan. I expect to be slower at first and to revisit early decisions as I learn.

- **Comfortable starting point:** Python and the data/agent side (decoding, normalizing, analysis, the BoxBoxBot agent logic).
- **Limited experience:** React. I have done some frontend work, but not a large, stateful, real-time UI.
- **New to me:** TypeScript, Next.js, charting (Plotly.js or D3), C#, ASP.NET Core, SignalR from the server side, Parquet and DuckDB, deploying on Vercel, and calling an LLM API directly from the browser.
- **BoxBoxBot port:** Learning the Anthropic TypeScript SDK (browser mode, Zod-defined tools, `toolRunner`, and streaming), and porting the existing Python agent logic to it.
- **Approach:** Learn by building in small stages, one new technology at a time. Keep the working Python recorder for capture, then build the reducer, normalizer and replay engine in C# against the sessions already captured, then a simple React replay view, then port ingestion to C# and add the more advanced features. See `PLAN.md` for the staged plan.
- **Scope control:** If a new technology blocks progress, fall back to a simpler option (see the fallback note under the tech stack) instead of stalling the whole project.
- **Learning log:** Keep short notes in the repo on what I learned and what I would do differently. They double as portfolio material and README content.

## Data Sources

| Source | Use | Notes |
|---|---|---|
| F1 live timing SignalR feed | Live streaming mode | **Primary source.** F1's own live timing infrastructure, but unofficial for third-party use and undocumented. Free. May change or block without notice. |
| F1 static archive (`livetiming.formula1.com/static/...`) | Historical sessions as JSON files | **Primary source for replay.** Same data as the live feed, available after sessions. |
| FastF1 (Python) | Reference implementation and optional helper | Reads the same feed and archive. Useful for cross-checking my decoding and for merging streams, but not a dependency of the core design. |
| Jolpica (Ergast successor) | Results, standings, lap times | Optional supplement. No sensor telemetry. |
| OpenF1 API | Not used | Historical data is free, but real-time access is paid. Skipped to keep the project free. |

### Limits of the public data

- About 4 Hz sampling and a limited channel set. Captured data from the Mexico City weekend contains only five car-data channels (`0, 2, 3, 4, 5`, which look like RPM, speed, gear, throttle and brake, still to be verified) plus position. **No DRS channel appeared** in practice, qualifying or the race.
- No tire or brake temperatures, ERS data, or steering angle.
- Real team telemetry wire formats are proprietary and not public. The goal is to mimic the *structure* (channels, units, timestamps), not replicate exact packets.
- F1's servers tend to block data-center IPs, so SignalR and archive ingestion must run locally, not on cloud hosts.
- The feed streams `CarData.z` and `Position.z` as base64-encoded, zlib-compressed JSON, so the ingestion layer needs a decode step before normalizing.

### What the captured data showed (Mexico City 2026: P1, P2 snapshot, P3, Qualifying, Race)
- **27 topics stream live** (28 subscribed; `LapCount` is race-only). Beyond the original list, the archive and live feed carry strategy-relevant topics: `TyreStintSeries`, `CurrentTyres`, `LapSeries`, `PitLaneTimeCollection`, `DriverTracker`, `WeatherDataSeries` and `TlaRcm`. `TimingDataF1` duplicates `TimingData`.
- **Most topics send deltas.** A reducer must deep-merge them, apply `_deleted` instructions, and handle numeric-key updates to lists. Merging the archive's deltas reproduced the live snapshot for 21 of 27 feeds. The `.z` topics carry full values per entry.
- **The initial snapshot is unreliable.** It can be empty (Practice 3), partial, or missing data such as pit times. Treat the first message per topic as the base state.
- **Metadata can be wrong early.** For the first 14 minutes of Practice 3, `SessionInfo` described the Sunday race.
- **Time bases differ.** Archive timestamps are relative to stream start (`HH:MM:SS.mmm`); live messages carry absolute UTC. Build the session clock from the `Utc` fields.
- **Sessions are not clean intervals.** Qualifying and the race include red flags, restarts, VSC and Safety Car periods. The replay engine has to handle a clock that stops and restarts.
- **Archive quirks:** the weekend folder uses the race-day date, not the session date; missing paths return 403 rather than 404; the archive may still be `Generating` after a session ends.
- **Team radio** streams metadata and an `.mp3` path only, and only for a subset of clips.

## Core Features

### Data layer
- SignalR ingestion client that connects to the live timing feed and decodes the compressed streams. Runs locally.
- Archive loader that pulls the same session data from the static endpoints and caches it after the first download.
- Normalizer converting everything into a channel-sample schema: `session_time, car, channel, value, unit`. Live and archive input share this one code path through an adapter layer.
- Replay engine that emits samples at original timing, with adjustable speed (10x to 60x) and a scrub bar.
- Live mode as a local demo, since cloud hosts are likely blocked.

### Analysis
- Distance-aligned lap comparison (speed, throttle, brake, gear) with a delta-time line.
- Sector bests and ideal lap.
- Long-run pace and fuel-corrected tire degradation per compound.
- Undercut/overcut window detection and pit-stop projection.
- Anomaly flags (sudden pace drop, time lost in a specific corner).
- Auto-generated session brief: top takeaways, biggest time loss versus a reference, pace ranking.
- Strategy backtesting on past races (for example, "what if we pitted two laps earlier?").

### Interface
- Session picker (season, race, session), driver and reference-lap selection, strategy parameters.
- Live-updating panels driven by the replay stream.
- Jump-to-event navigation (pit stops, safety cars, large pace changes) so users do not have to sit through a full session.
- Optional commentary layer that turns model output into radio-style calls.

### Interactive "strategist" mode
The replay pauses at key moments (pit window opens, safety car, rival pits) and asks the user what they would do. It then reveals what the team actually did and what the model estimated.

Framing note: public data does not capture driver inputs well enough to simulate driving, so "race engineer / strategist" is the honest role. Driver-style lap comparison is one tool within it.

### BoxBoxBot as a dashboard panel
- BoxBoxBot lives inside the web app as an assistant panel next to the data views, instead of being a separate chat app.
- Analysis functions are exposed as tools the BoxBoxBot agent can call, so it can answer questions like "who had the best long-run pace?" from this data.
- **Runs in the same browser window, no hosted agent backend.** The agent loop is ported to TypeScript and runs client-side. Its tools read the precomputed analysis data (session briefs, pace tables, lap comparisons) that the dashboard already loads.
- **Bring Your Own Key (BYOK):** Users paste their own LLM API key in a settings screen, and the browser calls the provider directly. No shared key, so no cost to me. The key is kept in the user's browser only and is never sent to or logged by my servers.
- Without a key, the dashboard still works fully and the panel shows a "add your key to enable" prompt.
- The panel is aware of dashboard context (selected session, driver, lap, and replay position), so questions like "why did he lose time here?" refer to what is on screen.
- Possible extras: the agent can drive the dashboard (jump to an event, set a reference lap) and feed the commentary layer.

## Proposed Tech Stack

| Layer | Choice | Why |
|---|---|---|
| Data path (core) | C# (.NET): reducer, normalizer, replay engine, then ingestion; ASP.NET Core + SignalR hub | New language used in motorsport software. One language and one set of types from feed to UI. The .NET SignalR client matches the protocol of F1's own feed. Built and tested against captured sessions first; ingestion is ported last. |
| Capture and exploration | Python (the existing `f1_client.py` recorder and downloader; pandas and notebooks; FastF1 for cross-checking) | The recorder already works and keeps capturing sessions until the C# ingestion replaces it. Python stays for exploration and cross-checks. |
| Storage | Parquet + DuckDB (TimescaleDB later if desired) | Cheap, fast, no server needed for precomputed data |
| Frontend | TypeScript + React (Next.js), Plotly.js or D3 | Handles many configs and live panels |
| BoxBoxBot panel | Chat UI and agent loop in the React/TypeScript frontend, using the Anthropic TypeScript SDK (`@anthropic-ai/sdk`) in its opt-in browser mode with the user's own API key (BYOK). Tools are defined with Zod and run by the SDK's `toolRunner`. | No hosted backend or shared key. Agent tools read the precomputed analysis data in the browser. Streamlit is no longer needed. Users need an Anthropic API key. |

Notes:
- Teams don't publish their stacks, so this is based on general industry patterns, not any specific team.
- The Claude Agent SDK is not used for the in-browser BoxBoxBot panel. It runs Claude Code as a subprocess and needs a server-side runtime, so it does not fit a browser-only BYOK design. It could be explored later for a local-only tool or a separate project.
- Fallback if C# plus a new frontend is too much at once: build the reducer in Python first, use its output as the expected results for the C# version, and port after. If porting ingestion to C# stalls, the Python recorder keeps the project working.
- The C# to TypeScript boundary is the data schema. Generate TypeScript types from the C# models so the two sides cannot drift.

## Architecture

```
[Local capture]  Python recorder + archive downloader -> raw JSONL / archive files
  (replaced by C# ingestion in a later stage)
        |
        v
[C# data path]
  adapters (live file / archive) -> reducer (deltas, _deleted) -> decode .z
  -> normalize to channel schema -> analysis -> Parquet/JSON chunks
        |
        v
[Static hosting (CDN / object storage)]
        |
        v
[React frontend]  <-- client-side replay from chunks (default, free, always on)
  |   dashboard panels + BoxBoxBot assistant panel
  |                          |
  |                          v
  |             [LLM provider API] -- called directly from the browser with the user's own key (BYOK)
  ^
  |  (optional)
[C# SignalR replay service] -- streams the same data (local, or free-tier host)
```

Key design decision: because 60+ minute sessions are only a few hundred thousand samples per channel at 4 Hz, the data volume is small. The real costs are the first-time load and user patience, so precompute everything and speed up playback.

## Deployment (free)

- **Frontend:** Vercel. It cannot hold long-lived websocket connections, so it serves static assets only.
- **Default playback:** Host precomputed chunks as static files (Vercel CDN, or Cloudflare R2 free tier). The browser plays them back on a timer to simulate the stream. Free, always on, scalable.
- **Optional streaming server:** C# service on a free-tier host (Render, Fly.io, Oracle Cloud always-free). Free tiers change often, so verify current limits. Expect cold starts or sleeping.
- **Live mode:** Local only, shown via demo video. Cloud hosts will likely be blocked by F1's servers.

## Key Handling (BYOK)

BoxBoxBot runs in the browser with each user's own LLM API key, so there is no shared key and no cost to me. The tradeoff is that the page holds the user's key while it runs, so anything that can run code on the page could read it. These are the safeguards to build in from the start.

### What the risk is
- A key shipped in client code is visible to anyone, which is why browser-mode SDK options carry warnings. That is the problem for a developer embedding *their own* key. With BYOK there is no key of mine in the app.
- The remaining risk is code running on my page: injected script (XSS), a compromised npm dependency, or my own mistakes (logging, URLs, analytics). The damage would be limited to that user's key, but it is still real money on their account.
- A bug in the agent loop could also run up a user's usage.

### Safeguards
- **Storage:** Keep the key in memory by default, so it disappears on refresh. Offer "remember on this device" as an explicit opt-in, and warn about shared computers.
- **Never leaks:** Never send the key to my servers, never log it, never put it in a URL, and keep it out of error reports and analytics.
- **Locked-down page:** Add a strict Content Security Policy that only allows connections to the LLM provider's domain, so a stolen key has nowhere to go. Use no third-party analytics or ad scripts on the page.
- **Safe rendering:** Sanitize any markdown or HTML the model returns before displaying it.
- **Few dependencies:** Keep the frontend dependency list small, pin versions, and review updates.
- **Read-only tools:** The agent's tools only read the static, precomputed data, so a bad prompt cannot cause side effects.
- **Cost limits:** Cap loop iterations and output length, and show users their usage. Tell users to create a dedicated key with a spending limit where the provider supports it.
- **Transparency:** State in the README and in the settings screen that the key stays in the browser and is sent only to the provider.

## Build Order

0. Housekeeping: back up captures, download archives, create the repo with a `.gitignore` that excludes credentials and raw data.
1. C# reducer and decoders, tested against the captured sessions (including the empty-snapshot and red-flag cases) and checked against the archive.
2. C# normalizer and replay engine (channel schema, session clock, speed control), fed by live-file and archive adapters.
3. Lap comparison and analysis, explored in Python notebooks first (DuckDB over Parquet), then moved to the C# data path.
4. React (Vite) frontend with client-side replay from static chunks, deployed on Vercel.
5. Session brief and lap comparison UI (first usable product).
6. Tire degradation, undercut detection and Safety Car / red-flag handling, then strategist mode and backtesting.
7. Embed BoxBoxBot as an assistant panel in the dashboard: port the agent loop to TypeScript, add a BYOK settings screen, and give it access to the current session and replay context.
8. ASP.NET Core SignalR hub streaming the replay to the browser locally.
9. Port ingestion (sign-in flow, live client, archive downloader) to C# and retire the Python recorder.
10. Polish live mode for demos.

## What Makes It Stand Out

- A real architecture (normalizer, replay engine, store, consumers) with tests and a diagram, not just a dashboard.
- One concrete, validated insight, for example: "my model would have called the optimal stop X out of Y times" via backtesting.
- A polished README and a short demo video of a replayed race driving the live views.
- Honest framing: "ATLAS-style channel model built on public data," with limitations stated up front.

## Risks and Open Questions

- The SignalR feed and archive are the project's foundation, but they are undocumented and could change, break, or add restrictions. Keep ingestion behind an adapter and lean on cached replay data, so the app still works if the feed changes.
- F1's terms generally restrict commercial use of its data. Keep the project non-commercial, credit sources, and do not redistribute raw data. Read the current terms before publishing publicly.
- Free hosting tiers may change. Client-side playback is the fallback.
- Model quality is limited by 4 Hz data and missing channels. State this plainly in the README.
- Decide how much new tech to take on at once (C# plus frontend), since scope could balloon. Mitigation: the C# core starts against files already captured, and ingestion is ported last.
- Hosting precomputed chunks on a public CDN may count as redistributing F1 data. Read the current terms before deploying, and consider letting users generate the chunks locally.
- The sign-in flow depends on scraping F1's frontend config and on undocumented identity-provider endpoints. It can break without notice, and a subscription is required.
- BYOK keeps LLM costs off me, but a key stored in the browser is exposed to any script on the page. Keep dependencies minimal, never log keys, never send them to my own servers, and tell users to use a key with a spending limit.
- The panel relies on the Anthropic SDK's opt-in browser mode, so users need an Anthropic API key. Check the SDK docs for current browser-mode behavior before building. Supporting other providers would mean a different SDK and more code, so it is out of scope for now.
- The agent logic has to be ported from Python to TypeScript, and its tools must work on precomputed data, so heavy analysis has to happen offline (in the data path), not at question time.

## Next Steps

- [ ] Learn new languages and tech stack (one at a time; see `PLAN.md`).
- [ ] Back up the captured sessions and download the P3, Qualifying and Race archives.
- [ ] Create the repo and README skeleton, with a `.gitignore` excluding credentials and raw data.
- [x] Capture sessions from the live feed and archive locally, and list the available channels (Mexico City: P1, P2 snapshot, P3, Qualifying, Race).
- [ ] Verify the car-data channel meanings against known values.
- [ ] Define the channel-sample schema.
- [ ] Build the C# reducer and decoders with tests against the captured sessions.
- [ ] Prototype the normalizer and a simple replay loop.
- [x] Pick demo sessions: the Mexico City Race (VSC, Safety Cars, red flag) for strategy, and Qualifying for lap comparison.

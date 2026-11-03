"""Minimal F1 live-timing client: auth, live SignalR stream, historical archive.

Sketch modelled on how FastF1 does it. Unofficial endpoints; they can change.
Needs an F1 account with an active F1TV subscription.

    export F1_USERNAME=... F1_PASSWORD=...
    python f1_client.py history 2024 "2024-09-01 Italian Grand Prix" "2024-09-01 Race"
    python f1_client.py record raw_live.jsonl      # lossless live capture
    python f1_client.py download 2025 "<weekend>" "<session>" out_dir
"""
import base64
import hashlib
import json
import os
import re
import secrets
import sys
import time
import zlib
from pathlib import Path

import jwt
import platformdirs
import requests
from signalrcore.hub_connection_builder import HubConnectionBuilder
from signalrcore.messages.completion_message import CompletionMessage

BASE = "https://livetiming.formula1.com"
IDP = "https://id.formula1.com"
CONFIG_JS = "https://account.formula1.com/scripts/main.min.js"
TIMEOUT = (5, 30)
UA = ("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/155.0.0.0 Safari/537.36")
TOKEN_FILE = Path(platformdirs.user_data_dir("f1_client")) / "token.json"
EXPIRY_MARGIN = 300  # renew when less than 5 min remain

TOPICS = ["Heartbeat", "DriverList", "ExtrapolatedClock", "RaceControlMessages",
          "SessionInfo", "SessionStatus", "TeamRadio", "TimingAppData",
          "TimingStats", "TrackStatus", "WeatherData", "Position.z",
          "CarData.z", "SessionData", "TimingData", "TopThree", "LapCount",
          # feeds listed in a session's archive Index.json; not yet confirmed
          # to be subscribable live
          "TyreStintSeries", "CurrentTyres", "LapSeries", "DriverTracker", "PitLaneTimeCollection", "WeatherDataSeries",
          "TlaRcm", "ArchiveStatus", "ContentStreams", "AudioStreams",
          # race archive feeds missed in earlier live captures
          "OvertakeSeries", "PitStop", "PitStopSeries"]


class AuthError(Exception):
    pass


# --------------------------------------------------------------------------
# Auth: OAuth2 code + PKCE against PingOne (pi.flow), yields the F1TV JWT
# --------------------------------------------------------------------------
def _scrape_config(s: requests.Session) -> tuple[str, str]:
    """clientId and policy id are app settings scraped from the frontend."""
    r = s.get(CONFIG_JS, timeout=TIMEOUT)
    r.raise_for_status()
    obj = re.search(r"\bdavinci:\{([^}]*)\}", r.text)
    if not obj:
        raise AuthError("davinci config object not found")

    def field(name):
        m = re.search(rf'\b{name}:"([^"]+)"', obj.group(1))
        if not m:
            raise AuthError(f"config field {name} missing")
        return m.group(1)

    return field("clientId"), field("acrValues")


def _login(username: str, password: str) -> dict:
    ping = {"X-Requested-With": "ping-sdk"}  # makes PingOne answer in JSON
    verifier = secrets.token_urlsafe(64)
    challenge = base64.urlsafe_b64encode(
        hashlib.sha256(verifier.encode()).digest()).rstrip(b"=").decode()
    state = secrets.token_urlsafe(24)

    with requests.Session() as s:  # cookies tie the steps together
        s.headers["User-Agent"] = UA
        client_id, policy = _scrape_config(s)

        # 1. start flow -> JSON describing the form
        r = s.get(f"{IDP}/as/authorize", headers=ping, timeout=TIMEOUT, params={
            "client_id": client_id, "response_type": "code",
            "response_mode": "pi.flow", "scope": "openid", "state": state,
            "nonce": secrets.token_urlsafe(24), "code_challenge": challenge,
            "code_challenge_method": "S256", "acr_values": policy})
        r.raise_for_status()
        form = r.json()
        submit_url = form["_links"]["next"]["href"]
        if not submit_url.startswith(f"{IDP}/"):  # never post creds elsewhere
            raise AuthError(f"unexpected submit url {submit_url}")

        # 2. submit credentials -> authorization code
        r = s.post(submit_url, timeout=TIMEOUT,
                   headers={**ping, "interactionId": form["interactionId"]},
                   json={"id": form["id"], "eventName": form["eventName"],
                         "interactionId": form["interactionId"],
                         "parameters": {"eventType": "submit", "data": {
                             "actionKey": "SIGNIN",
                             "formData": {"username": username,
                                          "password": password}}}})
        data = r.json()
        if data.get("status") != "COMPLETED":
            raise AuthError(data.get("message") or "sign-in failed")
        resp = data.get("authorizeResponse") or {}
        if resp.get("state") != state:
            raise AuthError("state mismatch")

        # 3. code -> access token
        r = s.post(f"{IDP}/as/token", timeout=TIMEOUT, data={
            "grant_type": "authorization_code", "client_id": client_id,
            "code": resp["code"], "code_verifier": verifier})
        r.raise_for_status()
        access = r.json()["access_token"]

    # 4. the F1TV entitlement JWT lives in a claim of the access token
    sub = jwt.decode(access, options={"verify_signature": False}).get(
        "subscriptionToken")
    if not sub:
        raise AuthError("no subscriptionToken claim: F1TV subscription needed")
    exp = float(jwt.decode(sub, options={"verify_signature": False})["exp"])
    return {"value": sub, "expires_at": exp}


def get_token(force: bool = False) -> str:
    """Valid subscription JWT from disk cache, or sign in (no refresh tokens)."""
    if not force:
        try:
            tok = json.loads(TOKEN_FILE.read_text())
            if tok["expires_at"] - EXPIRY_MARGIN > time.time():
                return tok["value"]
        except (OSError, ValueError, KeyError):
            pass
    try:
        user, pw = os.environ["F1_USERNAME"], os.environ["F1_PASSWORD"]
    except KeyError:
        raise AuthError("set F1_USERNAME and F1_PASSWORD") from None
    tok = _login(user, pw)
    TOKEN_FILE.parent.mkdir(parents=True, exist_ok=True)
    TOKEN_FILE.touch(mode=0o600)
    TOKEN_FILE.write_text(json.dumps(tok))
    return tok["value"]


# --------------------------------------------------------------------------
# Historical: static archive over HTTP
# --------------------------------------------------------------------------
def session_path(year: int, weekend: str, session: str) -> str:
    """weekend/session are '<date> <name>', e.g. '2024-09-01 Italian Grand Prix'
    and '2024-09-01 Race'. Spaces become underscores."""
    return f"/static/{year}/{weekend}/{session}/".replace(" ", "_")


def _decode(payload: str, zipped: bool):
    if payload[:1] == "{":
        return json.loads(payload)
    payload = payload.strip('"')
    if zipped:
        raw = zlib.decompress(base64.b64decode(payload), -zlib.MAX_WBITS)
        return json.loads(raw.decode("utf-8-sig"))
    return payload


_STREAM_TS = re.compile(r"(?:\d+\.)?\d\d:\d\d:\d\d\.\d{3}")


def fetch_page(path: str, page: str, token: str | None = None):
    """page e.g. 'CarData.z.jsonStream' or 'SessionInfo.jsonStream'.

    .jsonStream -> list of (timestamp, data); .json -> parsed dict.
    """
    headers = {"User-Agent": "BestHTTP", "Accept-Encoding": "gzip, identity"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    r = requests.get(BASE + path + page, headers=headers, timeout=TIMEOUT)
    if r.status_code == 401 and token:  # token revoked early: sign in again
        return fetch_page(path, page, get_token(force=True))
    r.raise_for_status()
    text = r.content.decode("utf-8-sig")
    if "jsonStream" not in page:
        return json.loads(text)
    zipped = ".z." in page
    out = []
    for line in text.split("\r\n"):
        # 'HH:MM:SS.mmm', or 'D.HH:MM:SS.mmm' once the stream clock passes 24h
        m = _STREAM_TS.match(line)
        if m:
            try:
                out.append((m.group(), _decode(line[m.end():], zipped)))
            except ValueError:
                continue
    return out


# --------------------------------------------------------------------------
# Live: SignalR Core stream
# --------------------------------------------------------------------------
class LiveClient:
    URL = "wss://livetiming.formula1.com/signalrcore"
    NEGOTIATE = "https://livetiming.formula1.com/signalrcore/negotiate"

    def __init__(self, on_data, topics=TOPICS, timeout=60):
        self.on_data, self.topics, self.timeout = on_data, topics, timeout
        self._connected = False
        self._last = time.time()

    def _on_message(self, msg):
        self._last = time.time()
        # Subscribe reply: CompletionMessage with .result = {topic: snapshot}
        # 'feed' updates: [topic, data, timestamp]; .z topics need _decode(...)
        self.on_data(msg)

    def start(self):
        get_token()  # fail early on auth problems
        r = requests.options(self.NEGOTIATE, timeout=TIMEOUT)
        headers = {"Cookie": f"AWSALBCORS={r.cookies['AWSALBCORS']}"}
        self.conn = (HubConnectionBuilder()
                     .with_url(self.URL, options={
                         "verify_ssl": True,
                         "headers": headers,
                         # re-evaluated on (re)connect, so it renews itself
                         "access_token_factory": get_token})
                     .build())
        self.conn.on_open(lambda: setattr(self, "_connected", True))
        self.conn.on_close(lambda: setattr(self, "_connected", False))
        self.conn.on("feed", self._on_message)
        self.conn.start()
        while not self._connected:
            time.sleep(0.1)
        self.conn.send("Subscribe", [self.topics],
                       on_invocation=self._on_message)
        try:  # exit when the stream goes quiet (session over)
            while not self.timeout or time.time() - self._last < self.timeout:
                time.sleep(1)
        except KeyboardInterrupt:
            pass
        self.conn.stop()


# --------------------------------------------------------------------------
# Recording helpers, for studying the raw formats
# --------------------------------------------------------------------------
class Recorder:
    """Appends every live message as one JSON line, untouched, with a local
    receive time. Nothing is decoded, so the file can be re-parsed later."""

    def __init__(self, path: str):
        self.f = open(path, "a", buffering=1)
        self.counts: dict[str, int] = {}
        self._t_report = time.time()

    def __call__(self, msg):
        if isinstance(msg, CompletionMessage):
            rec = {"kind": "snapshot", "invocation_id": msg.invocation_id,
                   "error": msg.error, "result": msg.result}
            for topic in (msg.result or {}):
                self.counts[topic] = self.counts.get(topic, 0) + 1
        elif isinstance(msg, list):
            rec = {"kind": "feed", "data": msg}
            if msg and isinstance(msg[0], str):
                self.counts[msg[0]] = self.counts.get(msg[0], 0) + 1
        else:
            rec = {"kind": f"other:{type(msg).__name__}", "data": repr(msg)}
        rec["t_recv"] = time.time()
        self.f.write(json.dumps(rec, default=str) + "\n")
        if time.time() - self._t_report > 30:
            self._t_report = time.time()
            print(time.strftime("%H:%M:%S"), dict(sorted(self.counts.items())))


def download_session(path: str, out_dir: str, token: str | None = None):
    """Save the raw bytes of every known page of one session."""
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    headers = {"User-Agent": "BestHTTP", "Accept-Encoding": "gzip, identity"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    pages = ["Index.json", "SessionData.json", "ArchiveStatus.json",
             "SessionInfo.jsonStream", "Heartbeat.jsonStream",
             "AudioStreams.jsonStream", "DriverList.jsonStream",
             "ExtrapolatedClock.jsonStream", "RaceControlMessages.jsonStream",
             "SessionStatus.jsonStream", "TeamRadio.jsonStream",
             "TimingAppData.jsonStream", "TimingStats.jsonStream",
             "TrackStatus.jsonStream", "WeatherData.jsonStream",
             "Position.z.jsonStream", "CarData.z.jsonStream",
             "ContentStreams.jsonStream", "TimingData.jsonStream",
             "LapCount.jsonStream", "ChampionshipPrediction.jsonStream"]
    # Index.json lists the feeds F1 actually published for this session;
    # add any we don't already know about
    try:
        idx = requests.get(BASE + path + "Index.json", headers=headers,
                           timeout=TIMEOUT).content.decode("utf-8-sig")
        for name in re.findall(r'"StreamPath"\s*:\s*"([^"]+)"', idx):
            if name not in pages:
                pages.append(name)
    except requests.RequestException:
        pass
    for page in pages:
        r = requests.get(BASE + path + page, headers=headers, timeout=TIMEOUT)
        print(r.status_code, page, len(r.content))
        if r.status_code == 200:
            (out / page).write_bytes(r.content)


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    if cmd == "history":
        _, _, year, weekend, session = sys.argv[:5]
        p = session_path(int(year), weekend, session)
        tok = get_token()
        print(fetch_page(p, "SessionInfo.jsonStream", tok)[0])
        print(fetch_page(p, "CarData.z.jsonStream", tok)[:2])
    elif cmd == "live":
        with open(sys.argv[2], "a") as f:
            LiveClient(lambda m: (f.write(f"{m}\n"), f.flush())).start()
    elif cmd == "record":
        # timeout=0: stay connected through gaps between sessions
        LiveClient(Recorder(sys.argv[2]), timeout=0).start()
    elif cmd == "download":
        _, _, year, weekend, session, out_dir = sys.argv[:6]
        download_session(session_path(int(year), weekend, session), out_dir,
                         get_token())
    else:
        sys.exit(__doc__)

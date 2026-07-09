# BRIEF FOR GEMINI — Game Replay + Game ID + Game Info i18n

You are implementing a multi-part feature across two sibling repos plus a shared
proto submodule. **Make the new UI beautiful** — polished modals, clean
typography, smooth transitions, copy-to-clipboard affordances, tasteful
loading/empty/error states.

## Repos & toolchains

- **Server (C#, .NET 9):** `/usr/local/google/home/changyuz/personal/RabiRiichi/RabiRiichi/`
  - dotnet: `/usr/local/google/home/changyuz/.dotnet/dotnet`
  - Build: `dotnet build RabiRiichi.Server/RabiRiichi.Server.csproj`
  - Test: `dotnet test RabiRiichi.Tests/RabiRiichi.Tests.csproj [--filter "FullyQualifiedName~..."]` (currently **375 pass** — keep green)
  - No `Program.cs`; entry point is top-level `RabiRiichi.Server/Startup.cs`.
  - Proto submodule at `RabiRiichi/Protos` (branch `main`), compiled at build by `Grpc.Tools`.
- **Web client (Vite + React + TS, three.js):** `/usr/local/google/home/changyuz/personal/RabiRiichi/RabiRiichi-Web/`
  - **Read its `AGENTS.md` first.** Toolchain via `corepack`: `corepack npm run typecheck | lint | format:check | test -- --run`; auto-fix format with `corepack npm run format`. Build: `corepack npx tsc -b && corepack npx vite build` (the bundled `build` script calls bare `npm` for `proto:gen`, which isn't on PATH — run tsc+vite directly). Proto regen: `corepack npm run proto:gen`.
  - Proto submodule at `RabiRiichi-Web/protos` (branch `main`). `src/generated/` is git-ignored generated code — never hand-edit.
  - Currently **255 tests pass**; lint has **26 pre-existing warnings, 0 errors** — keep 0 errors and add no new warnings.

## Hard constraints

- **`npm`/`npx` are NOT on PATH — always use `corepack npm`/`corepack npx`.**
- **Denied bash:** `find`, `grep -r`/`-R`/`-rn`, `ls -R`/`-r`, `fd`, `ag`, `tree`, `rg`. Use Read, plain `grep` on specific files, LSP, or explore subagents.
- **CRITICAL proto-submodule rule:** a proto change must end with the **identical commit hash in both submodule checkouts**. Proven method: commit in `RabiRiichi/Protos`, then in `RabiRiichi-Web/protos`: `git checkout -- .` → `git fetch /usr/local/google/home/changyuz/personal/RabiRiichi/RabiRiichi/Protos main` → `git merge --ff-only <hash>`. Then `corepack npm run proto:gen` in the web repo.
- **Breaking proto changes are OK** (no reserved fields / back-compat needed).
- **Commit conventions:** Conventional Commits (`feat:`/`fix:` lowercase). **Only commit when explicitly asked.**
- **AGENTS.md (web) rules:** external-only (no internal/corporate refs); read before write; keep toolchain green; don't weaken lint/TS strict; small/DRY/modular; **domain/view-model logic must be pure & unit-tested with Vitest; do NOT write GUI/visual/snapshot tests** (user verifies visuals). Components `PascalCase.tsx`, others `camelCase.ts`, unit tests `*.test.ts` next to source.
- **Comments explain WHY not what; keep them short.**
- **LSP "property does not exist" errors after proto edits are usually STALE** — `tsc`/`dotnet build` are authoritative.

---

## LOCKED DECISIONS

1. **Game ID** = `{yyyyMMddTHHmmss}-{roomId}` (filename-safe, seconds precision), e.g. `20260709T143052-1234`. Sent via `GameInfoMsg.game_id`. Shown (with copy button) in the Game Info modal **and** the final result screen.
2. **Replay format** = a single **god-view** `GameLogMsg` persisted as protobuf binary `.pb`. **Reuse existing `EventMsg` types unchanged** — the only difference from the live stream is that tiles normally serialized as *unknown* are serialized *revealed*. Do **not** invent new message shapes beyond the scalar field additions listed below.
3. **Persistence** only when env `RABIRIICHI_GAME_SAVE_DIR` is set; files at `{dir}/{gameId}.pb`.
   - **TTL from env `RABIRIICHI_GAME_SAVE_TTL` (seconds). DEFAULT = NONE: if the var is unset/empty, replays are kept forever and the cleanup background service is NOT started at all.** Only when a positive TTL is specified do you register/run the cleanup service.
4. **Fetch** = **public** websocket RPC (no auth), game-id sanitized.
5. **Viewer**: "View Replay" button in the lobby bottom row → beautiful modal asking for a game ID → fetch → play back reusing the live event path. **All tiles face-up** on the table (visible during replay, unlike live). **Seat switching = pure re-orient (option A)**: since god-view events already carry every tile revealed, changing seat only re-points the view anchor and re-emits; no re-fetch/re-hydrate. Seat switcher UI = **current seat's player nickname + ◀ / ▶ arrow buttons**.
6. **i18n**: fix all hard-coded strings in the Game Info modal.

---

## PART A — Server: Game ID

**Files:** `Protos/Core/GameInfo.proto`, `Core/GameInfo.cs`, `Core/Game.cs`, `RabiRiichi.Server/Models/Room.cs`.

- `GameInfoMsg` (`Protos/Core/GameInfo.proto:5-11`, fields `round=1, dealer=3, honba=4, current_player=5, riichi_stick=6`): add `string game_id = 2;` (field 2 is free).
- Generate the ID where both the clock and `room.id` are known. `Room.TryStartGame` (`Room.cs:59-90`) constructs `game = new Game(config)` at `Room.cs:75` — set the game id here (or pass `room.id` into the `Game` ctor `Game.cs:30-81`). Value: `$"{DateTime.UtcNow:yyyyMMdd'T'HHmmss}-{room.id}"`.
- Expose it on `Game` (e.g. `public string gameId`) and/or `GameInfo`, and populate it in `GameInfo.ToProto()` (`GameInfo.cs:47-55`). It then flows automatically into `GameStateMsg.info` (`Communication/Sync/GameState.cs:100-109`) and every per-turn state the client already receives.

---

## PART B — Server: God-view replay capture + persistence

**Files:** `Protos/Core/GameLog.proto`, `Core/Game.cs`, `RabiRiichi.Server/Connections/ServerActionCenter.cs`, `RabiRiichi.Server/Models/Room.cs`, new `RabiRiichi.Server/Services/ReplayStore.cs` (or `ReplayServiceImpl`), new `RabiRiichi.Server/Services/ReplayCleanupService.cs` (only when TTL set), `RabiRiichi.Server/Startup.cs`, `RabiRiichi.Server/Utils/ServerUtils.cs`.

### Proto
- `GameLogMsg` (`Protos/Core/GameLog.proto:19-22`, currently `repeated PlayerLogMsg player_logs = 1; GameConfigMsg config = 2;`): add `string game_id = 3;` and `int64 created_at_unix_ms = 4;`. Reuse `PlayerLogMsg`/`SingleLogMsg` (`GameLog.proto:8-15`) — but for god-view store **one** `PlayerLogMsg` holding the single revealed event stream (index 0). (`SingleLogMsg` is `oneof { EventMsg event = 1; SinglePlayerInquiryMsg inquiry = 2; }`; store only `event`.)

### Reveal-all serialization (the core mechanic)
- Live serialization: `Game.SerializeProto<EventMsg>(ev, seat)` (`Game.cs:140-149`) threads `PLAYER_ID` (`= "pid"`, `ProtoConverters.cs:16`) into converters; visibility per tile is `ConvertGameTile(tile, playerId)` where visible ⇔ `ev.playerId == playerId` (e.g. `ConvertDrawTileEvent` `ProtoConverters.cs:426-433`; `ConvertGameTile(GameTile,int)` `ProtoConverters.cs:627-630`).
- Add a **reveal-all** path: the minimal change is a sentinel god playerId or a `revealAll` flag threaded to the visibility check so `ConvertGameTile` treats **every** tile as visible. Prefer a small, surgical change (e.g. a sentinel constant like `GOD_VIEW_PLAYER_ID = -1` that the visibility comparisons treat as "see everything", or an overload `Game.SerializeProtoRevealed<EventMsg>(ev)`). Verify all tile-bearing event converters honor it (draw/deal/kan/nuki/discard/sync-state/agari).

### Capture (single god-view stream)
- Tee every event once. The all-events chokepoint is `EventBroadcast.Send` (`Events/EventBroadcast.cs:7`, subscribed to `EventBase` at `EventPriority.Broadcast`) → `Game.SendEvent` (`Game.cs:134-138`) → `ServerActionCenter.OnEvent` (`Connections/ServerActionCenter.cs:28-30`). Since these are called per-seat, capture **once** (e.g. only when `seat == 0`) using the reveal-all serialization, appending `SingleLogMsg{ Event = revealedProto }` to a per-`Game` god-view `GameLogMsg`. Reference implementation for the shape: `RabiRiichi.Tests/Scenario/ScenarioActionCenter.cs:257-287` (it builds per-seat logs; you want ONE revealed stream). Store `config` once, plus `gameId` + `createdAtUnixMs`.
- Skip capture entirely if `RABIRIICHI_GAME_SAVE_DIR` is unset (save memory).

### Env / options
- Mirror `Auth/TokenService.cs:12` (`Environment.GetEnvironmentVariable(...)`). Add a `ReplayOptions` singleton (or statics in `ServerConstants`, `Utils/ServerUtils.cs:4-11`) reading:
  - `RABIRIICHI_GAME_SAVE_DIR` → if null/empty, replay disabled (no capture/save/fetch).
  - `RABIRIICHI_GAME_SAVE_TTL` → parse seconds. **If unset/empty/non-positive: TTL = none (keep forever, no cleanup service).** Only a positive value enables cleanup.

### Flush on game end
- In `Room.TryStartGame`'s completion `ContinueWith` (`Room.cs:77-88`) or `TryEndGame` (`Room.cs:92-110`), if save dir set, write the god-view `GameLogMsg` to `{dir}/{gameId}.pb` via `IMessage.WriteTo(Stream)` (protobuf binary). Create the dir if missing. Do file I/O off the room task queue if it could block.

### TTL cleanup (first hosted service in the codebase — ONLY if TTL specified)
- **Only register/start this when `RABIRIICHI_GAME_SAVE_TTL` is a positive value.** If TTL is none (default) or the save dir is unset, do NOT add the hosted service.
- New `ReplayCleanupService : BackgroundService` registered via `services.AddHostedService<ReplayCleanupService>()` in `Startup.cs` (~line 42, near the singletons at `Startup.cs:35-45`), guarded by the TTL check. Use a `PeriodicTimer` (e.g. hourly). Delete `{dir}/*.pb` older than the TTL (file mtime, or parse `created_at_unix_ms`). Local precedent for delayed cleanup: `Connections/Extensions.cs:87-96`.

### DI
- Register `ReplayStore`/`ReplayOptions` as singletons in `Startup.cs:35-45`. `RoomTaskQueue`/`RoomList`/services pattern is there to mirror.

---

## PART C — Server: Public replay fetch RPC

**Files:** `Protos/Server/Rpc/Request.proto`, `RabiRiichi.Server/WebSockets/WebSocketController.cs`, `RabiRiichi.Server/Connections/ProtoUtils.cs`, `ReplayStore`.

- Transport is **websocket-only** (`/ws`; `WebSocketController` is the only controller; gRPC endpoints are commented out in `Startup.cs:65-72`).
- `Protos/Server/Rpc/Request.proto`:
  - `ClientRequest` oneof (`:19-30`, currently ends `add_ai = 7; remove_room_player = 8;`): add `GetReplayRequest get_replay = 9;` and `message GetReplayRequest { string game_id = 1; }`.
  - `ServerResponse` oneof (`:33-41`): add a replay arm, e.g. `GameLogMsg replay = 6;` (import `Core/GameLog.proto`).
- Handle in `WebSocketController.HandlePublic` (`:26-43`) so it needs **no auth** (public). Mirror the existing `if (msg.ClientRequest?.X != null)` dispatch chain (see `HandlePrivate` `:45-78` for the `AddAi`/`RemoveRoomPlayer` branches at `:58-65`).
- Teach `ProtoUtils.CreateServerResponse` (`Connections/ProtoUtils.cs:16-34`) about the new `replay` arm.
- `ReplayStore.GetReplay(gameId)`: **sanitize** `gameId` — allow only `[0-9A-Za-z-]`, reject anything with path separators/`..`. Read `{dir}/{gameId}.pb`, parse `GameLogMsg`, return; return NotFound if dir unset / file missing.

---

## PART D — Proto submodule sync (do this once, after all proto edits)

1. In `RabiRiichi/Protos`: `git add -A && git commit -m "..."`; note the hash.
2. In `RabiRiichi-Web/protos`: `git checkout -- .` → `git fetch /usr/local/google/home/changyuz/personal/RabiRiichi/RabiRiichi/Protos main` → `git merge --ff-only <hash>`.
3. Verify both `git rev-parse HEAD` match.
4. `corepack npm run proto:gen` in the web repo. Verify new types exist in `src/generated/protos.d.ts` (`GetReplayRequest`, `game_id`, etc.).

---

## PART E — Client: Replay viewer

**Files:** `src/net/requests.ts`, `src/net/client.ts`, promote `src/dev/replay.ts` + `src/dev/replayDriver.ts` → a stable location (e.g. `src/replay/`), `src/ui/LobbyScreen.tsx`, new `src/ui/ReplayModal.tsx`, new replay toolbar component, `src/App.tsx`, `src/state/store.ts`, `src/scene/Tile3D.tsx` + `src/scene/Hand3D.tsx`, `src/ui/ui.css`, locales.

### Reuse the existing offline replay pipeline
- `src/dev/replay.ts`: `getEventsFromReplay(replayJson, seat)` (`:22-38`) and `createInitialRoomFromReplay(replayJson)` (`:43-63`); `replayAccountId(seat)=seat+100` (`:12-17`).
- `src/dev/replayDriver.ts`: `startReplay()` (`:46-110`) currently hardcodes `import('./fixtures/full_game.json')` (`:53`) and `seat=1` (`:54`); playback loop `runBatch()` (`:76-107`) calls `rabiriichi.dev.handleGameEvent(eventMsg)` per event with `getEventDelay` timing (`:15-36`), reads `rabiriichi.animationSpeed` (`:98`), and pauses on `concludeGameEvent` awaiting `proceedReplay()` (`:38-44`, `:86-94`). `stopReplay()` `:112-124`.
- Dev backdoor `src/net/client.ts:132-149` (`dev.setConnectionStatus/setSelf/setRoom/handleGameEvent/setWaitingForProceed`) — **promote/rename to a stable `replay` API** since it's now user-facing.

### Generalize the driver
- Change `startReplay(gameLog: IGameLogMsg, seat: number)` to accept a fetched god-view log + chosen seat (default 0). Keep the proven loop. Since the god-view log is a **single** stream (not per-seat), simplify `getEventsFromReplay` to read the one revealed stream (index 0) regardless of chosen seat.

### Fetch
- `src/net/requests.ts`: add `getReplay(ws, gameId)` mirroring `addAi`/`removeRoomPlayer` (`:108-139`) — build `{ clientRequest: { getReplay: { gameId } } }`, accessor `(resp) => resp.replay`.
- `src/net/client.ts`: add `fetchReplay(gameId)` facade mirroring `:516-532` (get ws via the public/no-auth connect path since fetch is public; check `getWSClient` `:242-275`), returning the `IGameLogMsg`.

### Replay state + rendering
- Add `isReplay` to client state + `useIsReplay()` in `src/state/store.ts` (hooks at `:99-238`). Set it true when a replay starts, false on exit.
- **All tiles face-up:** feed god-view events → reducer fills every hand with real (non-unknown) tiles, so tiles should render face-up naturally. **Verify** the tile display-state logic in `src/scene/Tile3D.tsx` and hand rendering (`src/scene/Hand3D.tsx`, `src/scene/PlayerArea3D.tsx`) renders a known tile face-up even for non-self seats; if any code forces face-down purely by seat, add a minimal `useIsReplay()` override so replay always shows faces.
- **Seat switching (option A):** god-view data is fully in state, so switching seat only re-points the view anchor — update `self.seat`/`selfSeat` (`selfSeat` derivation is in `src/net/client.ts`; `getScreenPosition(seat, selfSeat, playerCount)` in `src/scene/seat.ts`) and re-emit `onChange`. No re-fetch/re-hydrate. Confirm `GamePlayHUD` guards (`GamePlayHUD.tsx:242-250`, requires `selfPlayer.seat !== undefined` and `self.id` matches a player) still hold when switching.

### UI
- **"View Replay" button** in `.lobby-bottom-row` (`LobbyScreen.tsx:142-192`), same row as Join/Logout. Styling refs: `.lobby-bottom-row` (`ui.css:321-336`), `.room-id-input` (`ui.css:303-319`), button classes `ui-button primary-button`/`secondary-button`/`danger-button`/`mini-button`.
- **`ReplayModal.tsx`** — a **beautiful centered modal** matching `GameInfoModal.tsx:127-137` (backdrop + `onClick` stopPropagation content + header with close `&times;`; CSS `ui.css:1597-1666`). Contains: a game-ID input (reuse `.room-id-input` look), a "Play" button, a loading spinner, and a clear "not found" error state. On submit → `fetchReplay(gameId)` → generalized `startReplay(log, seat)`.
- **Replay toolbar** (overlaid during replay, `isReplay` true): current **player nickname** (from log `config`/players) with **◀ / ▶** to cycle seats, plus play/pause, speed, step, and **Exit replay** (reuse the exit-confirm modal at `GamePlayHUD.tsx:507-539`, CSS `ui.css:2475-2520`) → `stopReplay()` + return to lobby.
- **App wiring:** replay renders through the normal gate — set `connectionStatus/self/room` via the (renamed) backdoor, then stream events; the first `beginGameEvent` flips `room.info` non-null and `App.tsx:90-106` renders `GamePlayHUD` + scene automatically (the `<GameTable/>` in `<Canvas>` `App.tsx:111-126` is always mounted). Replace or keep the `?replay=1` dev hook (`App.tsx:69-80`).

---

## PART F — Client: Game ID display (with copy button)

**Files:** `src/domain/model.ts`, `src/domain/reducer.ts`, `src/ui/GameInfoModal.tsx`, `src/ui/FinalResultPanel.tsx`, `src/ui/ui.css`, locales.

- Add `gameId` to the `GameInfo` interface (`model.ts:63-73`) and populate from `info.gameId` wherever the reducer builds `info` (`handleBeginGame` `reducer.ts:205-265`, `hydrateFromGameState` `:77-193`).
- Show **"Game ID: `XXXX`"** with a **copy-to-clipboard** button (icon + transient "copied!" feedback) in:
  - The Game Info modal, near the Room ID row (`GameInfoModal.tsx:170`).
  - The final standings screen `FinalResultPanel.tsx` below the title (`:44`) — it already has `useRoom()` (`:16`).
- Add i18n key `result.gameId` (+ any copy tooltip key).

---

## PART G — Client: Game Info modal i18n

**Files:** `src/ui/GameInfoModal.tsx`, `src/locales/en.json`, `src/locales/zhs.json`.

Replace every hard-coded user-facing string (add keys under a new `info.*` namespace or extend `hud.*`; add both en + zhs). Exact lines:

| Line | String | Note |
|---|---|---|
| `146` | `Live Info` | tab label |
| `153` | `Config` | tab label |
| `160` | `Yaku & Yama` | tab label |
| `170` | `Room ID:` | label |
| `176` | `Round:` | label |
| `186` | `Seat {info.dealer}` | reuse `t('room.seat',{seat})` (already used `:193,:235`) |
| `201` | `Wall:` | label |
| `209` | `Honba:` | label |
| `216` | `Riichi:` | label |
| `228` | `Players ({n})` | `Players` word |
| `238` | `(ID: {p.id})` | label |
| `242` | `Points: ` | label (value already `t('result.points')`) |
| `245` | `N/A` | fallback |
| `247` | `Status: ` | label |
| `275` | `Seed:` | label |
| `277` | `Auto` | value fallback |
| `281` | `Next Round Ack Timeout:` | label |
| `283,291,292` | `{...}s` | seconds suffix |
| `297` | `Rules & Policies` | subtitle |

Existing keys to reuse: winds `hud.east/south/west/north` (`en.json:108-111`), `hud.windSpace/roundSuffix/honbaSuffix/riichiSuffix` (`:113-116`), `hud.dealer/activePlayer/turnJun/remainingTiles/dora`, `room.seat`, `lobby.players`, `result.points`. In-game strings live under `hud.` / `result.` (no `game.`/`info.` yet). i18n init: `src/lib/i18n.ts` (default `zhs`, fallback `en`).

---

## TESTING & QUALITY GATES

**Server** (`RabiRiichi.Tests`; note: no service-level test harness exists, model-level tests do — see `RabiRiichi.Tests/Server/Models/RoomTest.cs`):
- `ReplayStore` round-trip: capture god-view `GameLogMsg` → write → read → parse; assert opponents' concealed free tiles are **revealed** in the god-view stream (and confirm the live per-seat stream still hides them — no visibility regression).
- Game-id sanitization (reject `../`, path separators), NotFound on missing, disabled when dir unset.
- TTL cleanup deletes old files, keeps fresh ones — and **is not active when TTL is unset**.
- Keep full suite green (375+).

**Client** (Vitest, **no GUI/snapshot tests**):
- Pure logic: generalized replay parser/driver (event extraction, seat-switch anchor math), `getReplay` request/facade shape.
- Extend `src/domain/reducer.replay.test.ts` if the god-view log changes shape; verify the reducer accepts fully-revealed `beginGameEvent`/`drawTile`/`syncGameState`.
- Run: typecheck ✓, lint (0 errors, no new warnings) ✓, format ✓, test ✓, build ✓.

**Env behavior:** unset `RABIRIICHI_GAME_SAVE_DIR` → capture/save/fetch all no-op gracefully; set → `{gameId}.pb` written; TTL unset → kept forever, no cleanup service; TTL set → cleaned after TTL.

---

## SUMMARY OF ALL PROTO CHANGES (scalar-only, no new message shapes beyond requests)
- `Core/GameInfo.proto`: `+ string game_id = 2;`
- `Core/GameLog.proto`: `+ string game_id = 3; + int64 created_at_unix_ms = 4;`
- `Server/Rpc/Request.proto`: `+ message GetReplayRequest { string game_id = 1; }`, `+ get_replay = 9` in `ClientRequest`, `+ GameLogMsg replay = 6` in `ServerResponse` (import `Core/GameLog.proto`).

## FILE:LINE ANCHOR INDEX (verified during investigation)
- Game create/start: `Room.cs:59-90` (`TryStartGame`, `game=new Game` `:75`), end hooks `:77-88`/`:92-110`; `Game.cs:30-81` ctor, `Game.SendEvent :134-138`, `SerializeProto :140-149`.
- Event chokepoint: `EventBroadcast.cs:7` / `:18`; per-seat send `User.OnEvent Models/User.cs:48-53`; sink `ServerActionCenter.cs:28-30`.
- Visibility: `ProtoConverters.cs:16` (`PLAYER_ID`), `:627-630` (`ConvertGameTile`), `:426-433` (draw example); capture shape ref `Tests/Scenario/ScenarioActionCenter.cs:257-287`.
- Env pattern `Auth/TokenService.cs:12`; DI `Startup.cs:35-45`; constants `Utils/ServerUtils.cs:4-11`; delayed-cleanup precedent `Connections/Extensions.cs:87-96`.
- Proto: `GameInfo.proto:5-11`, `GameLog.proto:8-22`, `Request.proto:19-41`; `GameInfo.ToProto GameInfo.cs:47-55`; `GameState.cs:100-109`.
- Client: reducer `reducer.ts:1088-1091`/`:1118-1211`/`:77-193`/`:205-265`; bridge `client.ts:306-364`; backdoor `client.ts:132-149`; store `store.ts:99-238`; requests `requests.ts:108-139`; facade `client.ts:516-532`; App gate `App.tsx:90-106`/`:111-126`/`:69-80`; replay `dev/replay.ts:12-63`, `dev/replayDriver.ts:15-131`; lobby row `LobbyScreen.tsx:142-192`; modals `GameInfoModal.tsx:127-137`, `GamePlayHUD.tsx:507-539`; info panel/modal `GamePlayHUD.tsx:30-89` (i18n done) + `GameInfoModal.tsx` (gaps above); final screen `FinalResultPanel.tsx:41-84`; model `model.ts:63-73`; scene `scene/Tile3D.tsx`, `scene/Hand3D.tsx`, `scene/seat.ts`.

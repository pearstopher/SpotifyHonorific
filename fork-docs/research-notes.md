# Research notes (fork)

## Re-adding the playback position variable (#5)

### Background: where the original variable came from

- SpotifyHonorific started as DiscordActivityHonorific (upstream commit `4f1c4db`, 2024-10-10). There, templates got Discord.Net's `SpotifyGame` presence object as `Activity`.
- `SpotifyGame` exposes the song's position: `Activity.Elapsed` (TimeSpan, `UtcNow - StartedAt`), plus `Activity.Remaining`, `Activity.Duration`, `Activity.StartedAt`, `Activity.EndsAt` ([Discord.Net source](https://github.com/discord-net/Discord.Net/blob/de8da0d3b998d32e248e5e438039d266139e4776/src/Discord.Net.Core/Entities/Activities/SpotifyGame.cs)). `StartedAt` comes from Discord's presence timestamps, so `Elapsed` follows the real position, and it's computed on every render.
- The original's own `Context` also only had `SecsElapsed`; the position came from `Activity`.
- Commit `d436cc3` ("Change Discord to Spotify with API", 2025-11-06) moved the data source to the Spotify Web API. `Activity` became SpotifyAPI.Web's `FullTrack`, which describes the track (name, artists, `DurationMs`) but not playback state, so `Activity.Elapsed` no longer exists. Re-adding it means exposing the position from the Spotify API response instead.

### How `Context.SecsElapsed` works today

`Context.SecsElapsed` is the number of seconds the current template context has been active.

- Counts up every frame in `Updater.ProcessTitleUpdate` (`+= deltaSeconds`) while a title context exists, regardless of render throttling. Re-renders and re-sends (zone change, AFK exit) don't reset it.
- Restarts at 0 in `ProcessCurrentlyPlayingTrack` (a new song is picked up, or the context was rebuilt after a config edit) and in `ClearTitle` (pause/stop, plugin disabled, AFK pause, sustained poll failure).
- Used for time-based effects: cycling text in the default template, and the rainbow hue in `TitleRenderingService`.
- Baseline test (#4): reads 0 whenever a song starts or resumes, wherever playback is in the song.

### What Spotify data is already available

- `SpotifyPollingService.GetCurrentlyPlayingTrackAsync` returns `SpotifyPollResult(Track, ProgressMs)`. `ProgressMs` is Spotify's `progress_ms`: the position in the current track at the time of the response.
- It's only set when `IsPlaying == true` and the item is a track. When paused or stopped, the result is `(null, null)` and the updater clears the title (`PollOutcome.Stopped`), same as the original (Discord drops the Spotify activity on pause). So there's no "paused" title to freeze; the position matters only while playing.
- Poll interval: 2 s by default; with rate limit protection 5 s near track start/end, 10 s mid-track, 15 s idle. Failed polls within the grace window keep the title (`PollOutcome.HoldTitle`) without new data.
- Today `ProgressMs` is only used for the adaptive poll interval; it never reaches templates.

### How to keep it current between polls

Mirror Discord.Net: store when the song started, and compute `Elapsed` at render time.

- On each poll with `PollOutcome.Playing`: set the start time to `UtcNow - ProgressMs`. Do this in `ProcessPollResult`, **before** `ProcessCurrentlyPlayingTrack`, because that method returns early for the same track; seeks within a song must still resync.
- At render: `Elapsed = UtcNow - startedAt`. No per-frame counting; it advances on its own between polls and through held (failed) polls.
- In `ClearTitle`: reset the start time to null alongside `SecsElapsed`.
- Accuracy: within roughly one API round-trip of Spotify's position, resynced every poll. After resuming, the title reappears on the next poll (as today) already at the right position.

### Decision

**Spec: the name must match the original exactly → `Activity.Elapsed`, type `TimeSpan?`** (as in Discord.Net's `SpotifyGame`). Templates use it the same way as before, e.g. `Activity.Elapsed.Minutes`, `Activity.Elapsed.Seconds`, `Activity.Elapsed.TotalSeconds`.

- **Challenge:** `Activity` is now SpotifyAPI.Web's `FullTrack`, which can't be given new properties.
- **Approach:** at render, wrap the track in a Scriban `ScriptObject`: import the `FullTrack`'s members (so every existing `Activity.*` keeps working) and add `Elapsed`. One small shared helper, used by `TitleRenderingService` and the config window preview. If import doesn't preserve existing `Activity.*` access, fall back to a thin wrapper type.
- **Where the code goes** (~15 lines):
  1. `UpdaterContext`: an **internal** start-time property (`DateTimeOffset?`). Internal so it's hidden from templates and from the config window's reflection-based variable table.
  2. `Updater.ProcessPollResult`: set it from `result.ProgressMs` when playing.
  3. `Updater.ClearTitle`: reset to null.
  4. Shared helper building the `Activity` object with `Elapsed`; used in `TitleRenderingService.RenderTitle` and the `ConfigWindow` preview (preview shows `00:00:00`).
  5. `TemplateHelper.IsTimeDependent`: also true for templates using `Activity.Elapsed`, so they keep re-rendering without `Context`.
  6. `ConfigWindow` "Available Template Variables": one hard-coded `Activity.Elapsed` row (the `Activity` rows are hard-coded).
  7. README "Template Variables" table: one `Activity.Elapsed` row after `Activity.DurationMs`.
- **Tests:** follow the existing pattern of small `internal static` helpers tested directly (like `ComputeNextPollInterval`, `CheckRenderDue`): start time from a poll, elapsed at a given time. `TitleRenderingService` tests: existing `Activity.*` fields still render through the wrapper, and `Activity.Elapsed` renders `m:ss`. `TemplateHelper` test for `IsTimeDependent`.
- **Example template** (to confirm in #6's tests):
  ```
  {{ Activity.Elapsed.Minutes }}:{{ Activity.Elapsed.Seconds | math.format "00" }}
  ```

### Decision: `Activity.ProgressMs`, minimal change (#25, 2026-11-12) — current

- **Name:** `Activity.ProgressMs` (Int, ms), replacing `Activity.ElapsedMs`. Matches `Activity.DurationMs` and is Spotify's own name for the value (`progress_ms`).
- **Why `Activity` needs a wrapper:** `Activity` is the Spotify library's `FullTrack` (track metadata). Playback position lives on Spotify's separate "currently playing" response (`ProgressMs`, `IsPlaying`, `Item`); the plugin passes only `Item` to templates, so the position was never reachable. Nothing is hidden or removed. `FullTrack` can't take new members, so the render wraps it in a Scriban `ScriptObject` with `ProgressMs` added. (`Context.*` needs no wrapper because `UpdaterContext` is the plugin's own class.)
- **Minimised:** 11 added / 4 replaced plugin lines; helpers inlined, full type names instead of new `using` lines, README example dropped. Same behaviour. Line-by-line explanation: `fork-docs/code-changes.md`.

The sections below record earlier versions (#6 `Activity.Elapsed`, #16 `Activity.ElapsedMs`) for history.

### Decision: `Activity.ElapsedMs` instead of `Activity.Elapsed` (#16, 2026-11-12) — superseded by #25

- **Decision:** expose the playback position as a single `Activity.ElapsedMs` (Int, milliseconds), replacing the TimeSpan `Activity.Elapsed` from #6 before anything goes upstream.
- **Why:** SpotifyHonorific's `Activity` is a Spotify track with a small set of simple variables (`Activity.DurationMs` is an Int in milliseconds; there's no `System.TimeSpan` anywhere). `ElapsedMs` matches `DurationMs`, so position and duration work the same way (remaining = `Activity.DurationMs - Activity.ElapsedMs`). The original's TimeSpan members aren't reproduced.
- **Code:** one-line change in `TemplateHelper.CreateActivity` (`activity["ElapsedMs"] = (int?)(now - trackStartedAt)?.TotalMilliseconds`). Same wrapper and start-time tracking; `IsTimeDependent` already matches `Elapsed`. Config window and README rows: `Activity.ElapsedMs` | Int. The TimeSpan parity tests were removed.
- **Template example:** `{{ Activity.ElapsedMs // 60000 }}:{{ Activity.ElapsedMs // 1000 % 60 | math.format "00" }}` (`//` is Scriban's integer division).

The section below records the earlier `Activity.Elapsed` version (#6) and is kept for history.

### Parity with the original `Activity.Elapsed` (verified after #6; superseded by #16)

- **Definition:** original is Discord.Net `SpotifyGame.Elapsed => DateTimeOffset.UtcNow - StartedAt` (`TimeSpan?`). Ours is `now - TrackStartedAt` (`DateTimeOffset?` → `TimeSpan?`). Same type, same calculation.
- **Same members:** both are `System.TimeSpan` on the same runtime (both plugins use Dalamud.NET.Sdk 15.0.0), so templates see the same members: `Days`, `Hours`, `Minutes`, `Seconds`, `Milliseconds`, `Microseconds`, `Nanoseconds`, `Ticks`, `TotalDays`, `TotalHours`, `TotalMinutes`, `TotalSeconds`, `TotalMilliseconds`, `TotalMicroseconds`, `TotalNanoseconds`. Scriban differs only in version (original 7.5.0, ours 7.2.6).
- **Proof:** `TemplateHelperCreateActivityTests.CreateActivity_Elapsed_RendersLikeTheOriginalActivity` renders the same templates against a stand-in with `SpotifyGame`'s exact `StartedAt`/`Elapsed` shape and against our `Activity`, and requires identical output (raw value, `Total*`, components, arithmetic, comparisons, null checks). With no start time, member access fails with the same Scriban error in both (can't happen in play: titles only render while playing).
- **Original documentation:** the README lists no template variables. The config window's "Available Template Variables" table is generated from the activity type's properties, so it has one row `Activity.Elapsed` with its type; the `TimeSpan` members aren't listed anywhere. Ours matches: one `Activity.Elapsed` (System.TimeSpan) row in the config window, plus one README row. Decision 2026-11-12: keep the README row as is; no changes needed.
- **Not re-added:** the original `SpotifyGame` also had `StartedAt`, `EndsAt`, `Duration`, `Remaining`, `TrackTitle` and string `Artists`. Out of scope; the spec covers `Elapsed` only.
- **Manual check template** (cycles through every member every 4 s; each step fits in 32 characters):
  ```
  {{- e = Activity.Elapsed
  names = ["Elapsed", "Days", "Hours", "Minutes", "Seconds", "Ms", "Us", "Ns", "Ticks", "TotDays", "TotHours", "TotMin", "TotSec", "TotMs", "TotUs", "TotNs"]
  values = [e, e.Days, e.Hours, e.Minutes, e.Seconds, e.Milliseconds, e.Microseconds, e.Nanoseconds, e.Ticks, e.TotalDays, e.TotalHours, e.TotalMinutes, e.TotalSeconds, e.TotalMilliseconds, e.TotalMicroseconds, e.TotalNanoseconds]
  i = (Context.SecsElapsed / 4 | math.floor) % (names | array.size)
  names[i] + " " + values[i]
  -}}
  ```

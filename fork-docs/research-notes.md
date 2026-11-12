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

- On each poll with `PollOutcome.Playing`: set the position from `ProgressMs`. Do this in `ProcessPollResult`, **before** `ProcessCurrentlyPlayingTrack`, because that method returns early for the same track; seeks within a song must still resync.
- Every frame in `ProcessTitleUpdate`: advance it by `deltaSeconds`, right next to `SecsElapsed`. This counts forward between polls and through held (failed) polls.
- In `ClearTitle`: reset to 0 alongside `SecsElapsed`.
- Accuracy: within roughly one API round-trip of Spotify's position, resynced every poll. After resuming, the title reappears on the next poll (as today) already at the right position.

### Recommendation

- **Name and type:** `Context.ProgressMs`, a `Double` in milliseconds.
  - Matches Spotify's own field (`progress_ms`, already `ProgressMs` in the code) and pairs with the existing `Activity.DurationMs` for "position / duration" templates and remaining time (`Activity.DurationMs - Context.ProgressMs`).
  - Double so per-frame advancing doesn't lose fractions of a millisecond.
  - Alternative in `SecsElapsed`'s style: `Context.SecsProgress` (Double, seconds). Easy to switch before #6.
- **Where the code goes** (a few lines):
  1. `UpdaterContext`: `public double ProgressMs { get; set; }`.
  2. `Updater.ProcessPollResult`: set it from `result.ProgressMs` when playing.
  3. `Updater.ProcessTitleUpdate`: `+= deltaSeconds * 1000`.
  4. `Updater.ClearTitle`: reset to 0.
  5. README "Template Variables" table: one row after `Context.SecsElapsed`.
- **No other changes needed:**
  - `TemplateHelper.IsTimeDependent` already treats any template mentioning `Context` as time-dependent, so it keeps re-rendering.
  - The config window's "Available Template Variables" table lists `UpdaterContext` properties by reflection.
  - The preview's mock context defaults the new property to 0.
- **Tests:** follow the existing pattern of small `internal static` helpers tested directly (like `ComputeNextPollInterval`, `CheckRenderDue`), e.g. a helper for "position after a poll / after a frame". Add a `TitleRenderingService` test that a template using `Context.ProgressMs` renders the expected `m:ss`.
- **Example template** (to confirm in #6's tests):
  ```
  {{ (Context.ProgressMs / 60000) | math.floor }}:{{ ((Context.ProgressMs / 1000) % 60) | math.floor | math.format "00" }}
  ```

# Manual tests (fork)

Fork-only. Checks the unit tests can't do. All tests use the **local dev plugin** (auto-reload on). Claude runs all builds before a test; the steps below are only the in-game parts.

## Install from the fork's repo URL (#1)

1. Open `https://github.com/pearstopher/SpotifyHonorific/releases/latest/download/repo.json` in a browser. It downloads a JSON list with one SpotifyHonorific entry, version `1.18.2.x`.
2. In game, add that URL under `/xlsettings` → Experimental → Custom Plugin Repositories, and save.
3. Uninstall the official SpotifyHonorific, then install SpotifyHonorific from the plugin installer. The installed version shows `1.18.2.x`.
4. `/spotifyhonorific config` opens the config window. Your previous settings and Spotify login are still there.
5. Play a song on Spotify. Your honorific title updates as it did with the official plugin.

## Load a local dev build (#3)

Claude has already built the plugin.

1. In game, add the dev plugin path from DEVELOPMENT.md under `/xlsettings` → Experimental → Dev Plugin Locations, and save.
2. `/xlplugins` → Installed: disable (or uninstall) any other SpotifyHonorific.
3. `/xlplugins` → Dev Tools → Installed Dev Plugins: enable SpotifyHonorific and turn on auto-reload. It shows version `1.18.1.0`.
4. Play a song. Your title updates as before.
5. Ask Claude to rebuild. Within a few seconds the dev plugin reloads (a reload line appears in `/xllog`), and the title still updates.

Result 2026-11-12: pass (dev plugin reloaded at 10:31:28, title kept updating).

## Baseline: current timer `Context.SecsElapsed` (#4)

Records how the existing `Context.SecsElapsed` timer behaves, so the playback-position variable we're re-adding (from the original DiscordActivityHonorific) can be compared against it. Have the Spotify app open so you can see its position.

Setup:
1. `/spotifyhonorific config` → Active Config: click **New**. Make the new config active.
2. Set its Title Template (scriban) to:
   ```
   T {{ Context.SecsElapsed | math.floor }}
   ```
   The title shows `T` and the timer in whole seconds.

Steps (for each, write what the title shows next to Spotify's position):
1. **Start** a song from the beginning. Wait ~30 s.
2. **Pause** for ~10 s.
3. **Resume** and wait ~10 s.
4. **Seek** forward to around 2:00 in the same song. Wait ~5 s.
5. **Skip** to the next song.
6. **Start a song partway**: pause, seek to about 1:00, then resume.

Afterwards, switch Active Config back to your usual one (you can delete the test config).

Result 2026-11-12: `Context.SecsElapsed` is the number of seconds the current template context has been active. A new context starts when a song starts or resumes, so it reads 0 there, wherever playback is in the song. That suits time-based effects like cycling text; it isn't meant to show the song's position, which is what the re-added variable is for. Log matches: pausing clears the title (`ClearCharacterTitle IPC` at 10:32:57), and a new context starts on resume.

## Re-added `Activity.Elapsed` (#6) — superseded by `Activity.ElapsedMs` (#16)

Checks that `Activity.Elapsed` shows how far into the song playback is, matching Spotify. Have the Spotify app open so you can see its position. Claude has rebuilt the dev plugin.

Setup:
1. `/spotifyhonorific config` → open "Available Template Variables": `Activity.Elapsed` (System.TimeSpan) is listed.
2. Active Config: click **New**, make it active, and set its Title Template to:
   ```
   {{ Activity.Elapsed.Minutes }}:{{ Activity.Elapsed.Seconds | math.format "00" }} T{{ Context.SecsElapsed | math.floor }}
   ```
   The Live Preview shows `0:00 T0` (no error).

Steps (for each, compare the title's `m:ss` with Spotify's position; the `T` number is `Context.SecsElapsed`, for comparison):
1. **Start** a song from the beginning. Wait ~30 s. `m:ss` matches Spotify (within ~1–2 s).
2. **Pause** ~10 s, then **resume**. The title comes back at Spotify's position (not 0:00); `T` restarts at 0.
3. **Seek** forward to around 2:00. Within a few seconds `m:ss` jumps to ~2:00 and keeps counting.
4. **Seek** back to around 0:30. `m:ss` follows.
5. **Skip** to the next song. `m:ss` starts again from ~0:00.
6. **Start a song partway**: pause, seek to about 1:00, resume. The title appears at ~1:00.
7. Switch to your usual template: it still works as before.

Result 2026-11-12: pass. Minutes and seconds follow Spotify's position; `Context.SecsElapsed` (`T`) counts as before. Template parity with the original `Activity.Elapsed` (incl. `TotalSeconds`) is covered by unit tests (`CreateActivity_Elapsed_RendersLikeTheOriginalActivity`).

## `Activity.ElapsedMs` (#16)

Checks that `Activity.ElapsedMs` (milliseconds, like `Activity.DurationMs`) follows Spotify's position. Have the Spotify app open. Claude has rebuilt the dev plugin.

Setup:
1. `/spotifyhonorific config` → "Available Template Variables": `Activity.ElapsedMs` (System.Int32) is listed right after `Activity.DurationMs`; `Activity.Elapsed` is gone.
2. Set the test config's Title Template to:
   ```
   {{ Activity.ElapsedMs // 60000 }}:{{ Activity.ElapsedMs // 1000 % 60 | math.format "00" }} T{{ Context.SecsElapsed | math.floor }}
   ```
   The Live Preview shows `0:00 T0` (no error).

Steps (compare the title's `m:ss` with Spotify's position; `T` is `Context.SecsElapsed`):
1. **Start** a song from the beginning. Wait ~30 s. `m:ss` matches Spotify (within ~1–2 s).
2. **Pause** ~10 s, then **resume**. The title comes back at Spotify's position; `T` restarts at 0.
3. **Seek** to around 2:00. Within a few seconds `m:ss` jumps to ~2:00.
4. **Skip** to the next song. `m:ss` starts again from ~0:00.
5. Optional, remaining time: `{{ (Activity.DurationMs - Activity.ElapsedMs) // 1000 }}` counts down to 0 by the end of the song.


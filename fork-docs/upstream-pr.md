# Upstream PR draft

Fork-only. Draft of the pull request to `Valiice/SpotifyHonorific`. **Not submitted**: needs my OK first.

- **Branch:** `pearstopher:feature/progress-ms-variable` (one commit on top of `upstream/master`; no fork-only files, no version bump)
- **Base:** `Valiice/SpotifyHonorific` `master`
- **Commit:** `Add Activity.ProgressMs template variable for the song's playback position`

Command to submit, once approved:

```
gh pr create --repo Valiice/SpotifyHonorific --base master --head pearstopher:feature/progress-ms-variable --title "Add Activity.ProgressMs template variable for the song's playback position" --body-file <file with the description below>
```

---

## Title

Add Activity.ProgressMs template variable for the song's playback position

## Description

Adds `Activity.ProgressMs`: how far into the current track playback is, in milliseconds, as reported by Spotify. It pairs with `Activity.DurationMs`, so templates can show where you are in a song, e.g. `1:23 / 3:45`, or the time remaining.

This brings back something the original DiscordActivityHonorific offered: there, `Activity` was Discord's Spotify activity, which had `Activity.Elapsed` (the song's position). When the plugin moved to the Spotify Web API, `Activity` became the track itself, which describes the song but not playback, so the position wasn't available to templates any more. It's added here as `ProgressMs`, Spotify's own name for it, to match `DurationMs`.

`Context.SecsElapsed` is unchanged. It counts how long the current template context has been active, which is what time-based effects like the cycling default template need. `Activity.ProgressMs` follows the song instead: it starts where Spotify is when a song resumes, and follows seeks.

**How it works**
- Every poll that finds a track playing stores when the track started (`now - progress_ms`). `progress_ms` is already in each poll response, so there are no extra requests.
- At render time, `ProgressMs` = now − start time, so it keeps counting smoothly between polls and resyncs on every poll.
- `Activity` is the Spotify track object, which can't take new properties, so the render wraps it in a Scriban `ScriptObject` with the track's members plus `ProgressMs`. All existing `Activity.*` variables work as before.
- Templates using `ProgressMs` count as time-dependent, so they keep re-rendering.
- Listed in the config window's variable table and the README.

**Example**

```scriban
{{ Activity.ProgressMs // 60000 }}:{{ Activity.ProgressMs // 1000 % 60 | math.format "00" }} / {{ Activity.DurationMs // 60000 }}:{{ Activity.DurationMs // 1000 % 60 | math.format "00" }}
```

**Testing**
- Unit tests for rendering `ProgressMs` (raw value and `m:ss`), existing `Activity` variables through the wrapper, and `IsTimeDependent`. All tests pass.
- Tested in game: play, pause/resume, seek and skip; the position matches Spotify's.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

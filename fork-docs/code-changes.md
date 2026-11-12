# Code changes: `Activity.ElapsedMs`

Fork-only. Every line added or modified to re-add the song's playback position as `Activity.ElapsedMs`, shown and explained, with how each change follows the original [DiscordActivityHonorific](https://github.com/anya-hichu/DiscordActivityHonorific) (anya's plugin, "the original" below). This is the net change against upstream: `git diff upstream/master -- SpotifyHonorific SpotifyHonorific.Tests README.md`. Plugin code: 5 files, 39 lines added and 6 replaced (counting comments and blank lines). Issues: #6 (re-add the variable), #16 (`ElapsedMs`), #20 and #22 (this document, and aligning with the original).

## How the original did it

In the original, templates got Discord.Net's `SpotifyGame` presence object as `Activity`, passed straight into the render:

```csharp
var title = titleTemplate.Render(new { Activity = activity, Context = UpdaterContext }, member => member.Name);
```

`SpotifyGame` stores when the song started and works out the position whenever a template reads it:

```csharp
public DateTimeOffset? StartedAt { get; internal set; }
public TimeSpan? Elapsed => DateTimeOffset.UtcNow - StartedAt;
```

Discord sends a new presence (with a new `StartedAt`) when a song starts, resumes or is seeked, so `Elapsed` follows playback. The original re-rendered the title every 100 ms. It had no live preview, no README variable docs and no tests; its config window listed variables by reflecting over the activity type.

## How this follows it

The same two-part pattern: **store when the song started; compute the position at render time.**

1. **Every Spotify poll while a song plays** (`Updater.ProcessPollResult`): Spotify reports `progress_ms`. The updater stores the start time (`now - progress`) in `UpdaterContext.TrackStartedAt`. This plays the role of Discord's `StartedAt`, refreshed by each poll instead of each presence update.
2. **Every render** (`TitleRenderingService.RenderTitle`): `ElapsedMs = now - TrackStartedAt`, the same calculation as `SpotifyGame.Elapsed`, in whole milliseconds. The track is wrapped so templates see it as `Activity.ElapsedMs`, alongside the track's usual variables.
3. **Re-rendering**: templates using `ElapsedMs` are marked time-dependent so they keep updating.
4. **Discoverability**: config window variable table, Live Preview and README.

`Context.SecsElapsed` is untouched.

### Differences from the original, and why

| Original | Here | Why |
|---|---|---|
| `Activity.Elapsed`, a `TimeSpan` | `Activity.ElapsedMs`, an `int` in milliseconds | Design decision (#16): this plugin's `Activity` is a Spotify track whose variables are simple values (`DurationMs` is an `int` in ms; no `TimeSpan` anywhere). `ElapsedMs` matches `DurationMs`. |
| Start time from Discord presence timestamps | Start time from Spotify's `progress_ms` on each poll | SpotifyHonorific talks to the Spotify Web API, not Discord. Same idea, different source. |
| Start time lives on the activity object | Start time lives on `UpdaterContext` (internal), and `Activity` is a wrapper | `Activity` here is the Spotify library's `FullTrack`, which can't be given new members, so the track is wrapped to add `ElapsedMs`. |
| Title re-rendered every 100 ms regardless | Upstream only keeps re-rendering titles it considers time-dependent | Upstream's own optimisation (not in the original). `ElapsedMs` has to be added to its check, or such titles would freeze. |
| Variable table built by reflection | One hard-coded row | Upstream hard-codes its `Activity.*` rows. |
| No live preview, README docs or tests | Preview, README row + example, unit tests | Upstream has all three; following upstream's conventions. |

---

## Plugin code

### `SpotifyHonorific/Updaters/UpdaterContext.cs`

Holds the song's start time: the equivalent of the original's `SpotifyGame.StartedAt`.

```diff
+using System;
+
 namespace SpotifyHonorific.Updaters;

 public class UpdaterContext
 {
     public double SecsElapsed { get; set; }
+
+    // When the current track started playing, per Spotify's reported progress.
+    // Internal so it stays out of the template variables; templates see it as
+    // Activity.ElapsedMs.
+    internal DateTimeOffset? TrackStartedAt { get; set; }
 }
```

| Line | Explanation | Original |
|---|---|---|
| `using System;` | Needed for `DateTimeOffset`; the project doesn't use implicit usings. | — |
| (blank lines) | Spacing, matching the other files. | — |
| `// When the current track started…` (3 lines) | What the value is, and why it's `internal`: templates and the config window only see **public** `UpdaterContext` properties, so it doesn't become a `Context.*` variable. | The original exposed `StartedAt` publicly as `Activity.StartedAt`. Not re-added here (out of scope). |
| `internal DateTimeOffset? TrackStartedAt { get; set; }` | The stored start time. Nullable: none until the first playing poll. | Same type as `SpotifyGame.StartedAt` (`DateTimeOffset?`). |

### `SpotifyHonorific/Updaters/Updater.cs`

Sets the start time on each playing poll.

```diff
         if (outcome == PollOutcome.Playing && track != null)
         {
+            // Resync on every poll, not just new tracks, so seeks are picked up.
+            _updaterContext.TrackStartedAt = ComputeTrackStartedAt(result?.ProgressMs, DateTimeOffset.UtcNow);
             _isMusicPlaying = true;
```

| Line | Explanation | Original |
|---|---|---|
| `// Resync on every poll…` | Explains the placement: before `ProcessCurrentlyPlayingTrack`, which returns early when the song hasn't changed and would otherwise miss seeks. | In the original, every presence update brought a fresh `StartedAt`; this is the per-poll equivalent. |
| `_updaterContext.TrackStartedAt = ComputeTrackStartedAt(result?.ProgressMs, DateTimeOffset.UtcNow);` | Stores the start time from Spotify's `ProgressMs` (already fetched by every poll; previously only used to pick the poll interval). | Discord.Net set `StartedAt` from the presence. |

```diff
+    internal static DateTimeOffset? ComputeTrackStartedAt(int? progressMs, DateTimeOffset now)
+        => progressMs is { } ms ? now - TimeSpan.FromMilliseconds(ms) : null;
+
```

| Line | Explanation | Original |
|---|---|---|
| `internal static DateTimeOffset? ComputeTrackStartedAt(…)` | The calculation as a small `internal static` helper, upstream's pattern for testable logic (`ComputeNextPollInterval`, `CheckRenderDue`). `now` is a parameter so tests can fix the clock. | No equivalent: Discord did this server-side. |
| `=> progressMs is { } ms ? now - TimeSpan.FromMilliseconds(ms) : null;` | With progress: the song started `progress` ms before now. Without: no start time. | — |
| (blank line) | Spacing. | — |

Nothing is reset when the title clears. After a clear nothing renders, and the next playing poll always sets a fresh start time before the next render. That matches the original, where the old activity was simply replaced.

### `SpotifyHonorific/Utils/TemplateHelper.cs`

Marks `ElapsedMs` templates as time-dependent, and builds the wrapped `Activity`.

```diff
 using Scriban;
+using Scriban.Runtime;
 using SpotifyHonorific.Activities;
```

| Line | Explanation | Original |
|---|---|---|
| `using Scriban.Runtime;` | For `ScriptObject` and `ScriptMemberImportFlags`. | Not needed there: no wrapper. |

```diff
     /// <summary>
     /// True if the rendered title can change over time: rainbow mode animates the
-    /// color, and "Context" is the only time-varying render variable (exact member
-    /// name; Scriban's renamer exposes property names verbatim). False positives
-    /// only cause unnecessary re-renders, which is the safe direction.
+    /// color, and "Context" and "Activity.ElapsedMs" are the time-varying render
+    /// variables (exact member names; Scriban's renamer exposes property names
+    /// verbatim). False positives only cause unnecessary re-renders, which is the
+    /// safe direction.
     /// </summary>
     public static bool IsTimeDependent(ActivityConfig config)
-        => config.RainbowMode || config.TitleTemplate.Contains("Context");
+        => config.RainbowMode
+           || config.TitleTemplate.Contains("Context")
+           || config.TitleTemplate.Contains("ElapsedMs");
```

| Line | Explanation | Original |
|---|---|---|
| Doc comment (4 lines modified) | Same comment, now naming `Activity.ElapsedMs` as a time-varying variable too. | The original had no such check (it re-rendered every 100 ms). |
| `=> config.RainbowMode` / `\|\| …Contains("Context")` | Unchanged conditions, split onto separate lines. | — |
| `\|\| config.TitleTemplate.Contains("ElapsedMs")` | A template using `ElapsedMs` changes over time, so it keeps re-rendering (every 0.5 s). Without this, a template using only `ElapsedMs` would render once and freeze. Matches the exact name only, so the plain word "Elapsed" in a title doesn't trigger it. | Equivalent to the original's always-re-render, limited to templates that need it. |

```diff
+
+    /// <summary>
+    /// Builds the template's Activity: the track's own members plus ElapsedMs, how
+    /// far into the track playback is in milliseconds (matching DurationMs).
+    /// </summary>
+    public static ScriptObject CreateActivity(object track, int? elapsedMs)
+    {
+        var activity = new ScriptObject();
+        activity.Import(track, ScriptMemberImportFlags.Field | ScriptMemberImportFlags.Property, renamer: member => member.Name);
+        activity["ElapsedMs"] = elapsedMs;
+        return activity;
+    }
```

| Line | Explanation | Original |
|---|---|---|
| (blank line), doc comment | Spacing, and what `Activity` now contains. | — |
| `public static ScriptObject CreateActivity(object track, int? elapsedMs)` | Shared by real renders and the Live Preview. Takes the elapsed value already worked out, so it's a plain function. `track` is `object` because the preview may pass a mock track. | The original passed the activity object directly; a wrapper wasn't needed because `SpotifyGame` already had `Elapsed`. |
| `var activity = new ScriptObject();` | A Scriban object (a dictionary of template variables), because `FullTrack` can't be given new members. | — |
| `activity.Import(track, …Field \| …Property, renamer: member => member.Name);` | Copies the track's public fields and properties under their exact names, so every existing `Activity.*` variable works as before. `member => member.Name` is the same renamer the plugin passes to `Render`. | Same naming as the original's render (`member => member.Name`). |
| `activity["ElapsedMs"] = elapsedMs;` | Adds `Activity.ElapsedMs`. | The original's `Activity.Elapsed`, as milliseconds. |
| `return activity;` | The object templates see as `Activity`. | — |

### `SpotifyHonorific/Core/TitleRenderingService.cs`

Computes `ElapsedMs` and renders with the wrapped `Activity`.

```diff
 using SpotifyHonorific.Updaters;
+using SpotifyHonorific.Utils;
+using System;
 using System.Collections.Generic;
```

| Line | Explanation | Original |
|---|---|---|
| `using SpotifyHonorific.Utils;` | For `TemplateHelper`. | — |
| `using System;` | For `DateTimeOffset`. | — |

```diff
-        var title = template.Render(new { Activity = track, Context = context }, member => member.Name);
+        var activity = TemplateHelper.CreateActivity(track, (int?)(DateTimeOffset.UtcNow - context.TrackStartedAt)?.TotalMilliseconds);
+        var title = template.Render(new { Activity = activity, Context = context }, member => member.Name);
```

| Line | Explanation | Original |
|---|---|---|
| `var activity = TemplateHelper.CreateActivity(track, (int?)(DateTimeOffset.UtcNow - context.TrackStartedAt)?.TotalMilliseconds);` | `ElapsedMs` = now minus the stored start time, in whole milliseconds (`int`, like `DurationMs`), worked out at every render so it keeps counting between polls. `null` before any start time. | Same calculation as `SpotifyGame.Elapsed => DateTimeOffset.UtcNow - StartedAt`, done at render time like the original. |
| `var title = template.Render(new { Activity = activity, Context = context }, member => member.Name);` | Modified: `Activity = activity` (the wrapper) instead of `Activity = track`; otherwise the same call. | Identical shape to the original's `Render(new { Activity = activity, Context = UpdaterContext }, member => member.Name)`. |

### `SpotifyHonorific/Windows/ConfigWindow.cs`

Live Preview and the "Available Template Variables" table.

```diff
         var mockContext = new UpdaterContext { SecsElapsed = 0 };
+        var activity = TemplateHelper.CreateActivity(track, 0);
```

| Line | Explanation | Original |
|---|---|---|
| `var activity = TemplateHelper.CreateActivity(track, 0);` | The preview shows `ElapsedMs` as 0, just as it shows `SecsElapsed` as 0. Upstream's mock context is left unchanged. | The original had no live preview. |

```diff
-                var renderedTitle = titleTemplate.Render(new { Activity = track, Context = mockContext }, member => member.Name);
+                var renderedTitle = titleTemplate.Render(new { Activity = activity, Context = mockContext }, member => member.Name);
```

| Line | Explanation | Original |
|---|---|---|
| `var renderedTitle = …Render(new { Activity = activity, … })` | Modified: preview renders with the wrapped `Activity`, like real titles. | — |

```diff
             ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Activity.DurationMs"); ImGui.TableNextColumn(); ImGui.Text("System.Int32");
+            ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Activity.ElapsedMs"); ImGui.TableNextColumn(); ImGui.Text("System.Int32");
```

| Line | Explanation | Original |
|---|---|---|
| `…ImGui.Text("Activity.ElapsedMs"); … ImGui.Text("System.Int32");` | One row after `Activity.DurationMs`, in upstream's hard-coded format. | The original listed every activity property by reflection, including `Activity.Elapsed`, as one row with its type. Same result: one row for the variable. |

---

## README

```diff
 | `Activity.DurationMs` | Int | Total duration of the track in milliseconds. |
+| `Activity.ElapsedMs` | Int | How far into the track playback is in milliseconds, as reported by Spotify. |
```

| Line | Explanation | Original |
|---|---|---|
| `\| Activity.ElapsedMs \| Int \| …` | New row after `Activity.DurationMs`, same format. Upstream's `Context.SecsElapsed` row is left as it is. | The original's README listed no variables. |

```diff
+**Song position**
+Shows the song name and how far into it you are, e.g. `Never Gonna Give You Up 1:05`.
+
+```scriban
+{{ Activity.Name | string.truncate 26 }} {{ Activity.ElapsedMs // 60000 }}:{{ Activity.ElapsedMs // 1000 % 60 | math.format "00" }}
+```
+
```

| Line | Explanation | Original |
|---|---|---|
| Heading, description, blank line | In the style of upstream's other examples. | — |
| Opening fence (`scriban`) / closing fence | Same code-block style as the other examples. | — |
| `{{ Activity.Name \| string.truncate 26 }} …` | Song name (≤ 26 chars), space, minutes (`ElapsedMs // 60000`; `//` is Scriban's whole-number division), `:`, seconds padded to two digits. At most 32 characters, Honorific's limit. Verified by a unit test. | — |
| (blank line) | Spacing. | — |

---

## Tests

All added; no existing upstream test was changed. 277 tests pass. (The original had no tests; these follow upstream's test project.)

### `SpotifyHonorific.Tests/UpdaterTests.cs`

| Test | What it checks |
|---|---|
| `UpdaterContext_TrackStartedAt_DefaultsToNull` | No start time until a playing poll sets one. |
| `UpdaterContext_TrackStartedAt_IsNotATemplateVariable` | `GetProperties()` (what the config window lists) doesn't see `TrackStartedAt`, so no `Context.TrackStartedAt` variable appears. |
| `UpdaterTrackStartedAtTests` (fixed clock): `ComputeTrackStartedAt_FromProgress_IsNowMinusProgress` | 83,000 ms of progress → started 83 s before now. |
| `ComputeTrackStartedAt_AtTrackStart_IsNow` | Progress 0 → started now. |
| `ComputeTrackStartedAt_NoProgress_IsNull` | No progress → no start time. |
| `ComputeTrackStartedAt_SeekOrNewTrack_ResyncsToTheNewPosition` | A later poll at an earlier position (seek back or new song) replaces the start time; this is how seeks are followed. |

### `SpotifyHonorific.Tests/TemplateHelperTests.cs`

`using SpotifyAPI.Web;` is added for the test track types.

| Test | What it checks |
|---|---|
| `IsTimeDependent_TemplateReferencesActivityElapsedMs_ReturnsTrue` | A template using `ElapsedMs` (no `Context`, no rainbow) keeps re-rendering. |
| `IsTimeDependent_WordElapsedWithoutVariable_ReturnsFalse` | The plain word "Elapsed" in a template doesn't count; only the exact name `ElapsedMs`. |
| `CreateActivity_ElapsedMs_IsTheGivenValue` | `Activity.ElapsedMs` is the value passed in (83,456). |
| `CreateActivity_ElapsedMs_IsAnIntLikeDurationMs` | `ElapsedMs` and `DurationMs` are both `int` (#16). |
| `CreateActivity_NoElapsed_ElapsedMsIsNull` | No value → `null`. |
| `CreateActivity_KeepsTrackMembers` | The wrapper still carries the track's own members (`Name`, `DurationMs`). |

### `SpotifyHonorific.Tests/TitleRenderingServiceTests.cs`

New class `TitleRenderingServiceActivityElapsedMsTests`. It renders real templates through `TitleRenderingService.RenderTitle`, the path the plugin uses in game. Its `ContextAt(seconds)` helper sets `TrackStartedAt` that many seconds ago, plus 0.2 s of slack so a slow run can't tip into the next second.

| Test | What it checks |
|---|---|
| `RenderTitle_ActivityElapsedMs_RendersMilliseconds` | `{{ Activity.ElapsedMs // 1000 }}` at 83 s → `83`. |
| `RenderTitle_ReadmeSongPositionExample_Renders` | The README example at 1:05 → `Never Gonna Give You Up 1:05`. |
| `RenderTitle_RemainingTimeFromDurationMs_Renders` | `(DurationMs - ElapsedMs) // 1000` at 13 s → `199`. |
| `RenderTitle_ExistingActivityVariables_StillRender` (4 cases) | `Name`, `Artists[0].Name`, `Album.Name`, `DurationMs` unchanged through the wrapper. |
| `RenderTitle_ContextSecsElapsed_Unchanged` | `Context.SecsElapsed` still renders its own value. |

---

## Not changed

- `Context.SecsElapsed`: how it counts, when it resets, and its README row.
- Polling: no extra Spotify requests (`ProgressMs` was already in every poll).
- Everything else in the plugin.

## Further simplifications considered (not applied)

| Option | Saves | Why kept |
|---|---|---|
| Inline `ComputeTrackStartedAt` into `ProcessPollResult` | 3 code lines, 4 tests | Upstream's pattern is small `internal static` helpers with unit tests; inlining leaves the start-time maths untested. |
| Drop the README "Song position" example | 7 README lines | The original had no README docs, but upstream documents variables with examples; the example also shows how to use milliseconds. |
| Shorten the `UpdaterContext` comment to one line | 2 lines | It explains why the property is `internal`, which isn't obvious. |

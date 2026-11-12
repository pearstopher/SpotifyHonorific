# Code changes: `Activity.ElapsedMs`

Fork-only. Every line added or modified to re-add the song's playback position as `Activity.ElapsedMs`, shown and explained. This is the net change against upstream (`git diff upstream/master..master -- SpotifyHonorific SpotifyHonorific.Tests README.md`): 9 files, 231 lines added, 7 lines modified. Nothing else in the plugin changed. Issues: #6 (re-add the variable), #16 (expose it as `ElapsedMs`), #20 (this document).

## How it fits together

1. **Every Spotify poll while a song plays** (`Updater.ProcessPollResult`): Spotify reports `progress_ms`, how far into the song it is. The updater turns that into the moment the song started (`now - progress`) and stores it in `UpdaterContext.TrackStartedAt`.
2. **Every title render** (`TitleRenderingService.RenderTitle`): the track is wrapped in a template object that has all the track's normal variables plus `ElapsedMs = now - TrackStartedAt`, in whole milliseconds. Because it's computed at render time, it keeps counting between polls; because the start time is refreshed on every poll, it follows seeks.
3. **When the title clears** (`Updater.ClearTitle`, e.g. pause): the start time is reset.
4. **Re-rendering**: templates that use `ElapsedMs` are marked time-dependent, so they keep re-rendering every 0.5 s even if they don't use `Context`.
5. **Discoverability**: the config window's variable table, the Live Preview and the README all show `Activity.ElapsedMs` (Int), next to `Activity.DurationMs`.

`Context.SecsElapsed` is untouched.

---

## Plugin code

### `SpotifyHonorific/Updaters/UpdaterContext.cs`

Stores the song's start time next to `SecsElapsed`.

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

| Line | Explanation |
|---|---|
| `using System;` | Needed for `DateTimeOffset`. The file had no usings before; the project doesn't use implicit usings. |
| (blank line) | Separates the using from the namespace, matching the other files. |
| (blank line) | Separates the new property from `SecsElapsed`. |
| `// When the current track started…` | Says what the value is: the moment the track started, worked out from Spotify's progress. |
| `// Internal so it stays out of…` | Explains why it's `internal`: templates and the config window only see **public** properties of `UpdaterContext`, so this doesn't become a new `Context.*` variable. |
| `// Activity.ElapsedMs.` | Points to where templates actually see it. |
| `internal DateTimeOffset? TrackStartedAt { get; set; }` | The stored start time. `DateTimeOffset` is an absolute point in time. Nullable (`?`) because there's no start time until a playing poll arrives, or after the title clears. |

### `SpotifyHonorific/Updaters/Updater.cs`

Sets the start time on each playing poll, provides the calculation, and resets it when the title clears.

**1. On each playing poll** (`ProcessPollResult`):

```diff
         if (outcome == PollOutcome.Playing && track != null)
         {
+            // Resync on every poll, not just new tracks, so seeks are picked up.
+            _updaterContext.TrackStartedAt = ComputeTrackStartedAt(result?.ProgressMs, DateTimeOffset.UtcNow);
             _isMusicPlaying = true;
             _tracksPlayedThisSession.Add(track.Id);
             ProcessCurrentlyPlayingTrack(track);
```

| Line | Explanation |
|---|---|
| `// Resync on every poll…` | Explains the placement. It's placed before `ProcessCurrentlyPlayingTrack`, which returns early when the song hasn't changed. Putting it there would only update on new songs and miss seeks within a song. |
| `_updaterContext.TrackStartedAt = ComputeTrackStartedAt(result?.ProgressMs, DateTimeOffset.UtcNow);` | Recomputes the start time from Spotify's `ProgressMs` (already fetched by every poll; previously only used for the adaptive poll interval) and the current UTC time. `result?.` is null-safe, although `result` is always set when the outcome is `Playing`. |

**2. The calculation** (new helper, next to the other `internal static` helpers):

```diff
+    internal static DateTimeOffset? ComputeTrackStartedAt(int? progressMs, DateTimeOffset now)
+        => progressMs is { } ms ? now - TimeSpan.FromMilliseconds(ms) : null;
+
```

| Line | Explanation |
|---|---|
| `internal static DateTimeOffset? ComputeTrackStartedAt(int? progressMs, DateTimeOffset now)` | Pure function: takes the progress and the current time, returns the start time. `internal static` so the unit tests can call it directly, the same pattern as `ComputeNextPollInterval` and `CheckRenderDue`. `now` is a parameter (not read inside) so tests can use a fixed clock. |
| `=> progressMs is { } ms ? now - TimeSpan.FromMilliseconds(ms) : null;` | If Spotify gave a progress value, the song started `progress` milliseconds before `now`. If not, there's no start time (`null`). |
| (blank line) | Spacing before the next method. |

**3. When the title clears** (`ClearTitle`):

```diff
         _updaterContext.SecsElapsed = 0;
+        _updaterContext.TrackStartedAt = null;
         _titleState.Clear();
```

| Line | Explanation |
|---|---|
| `_updaterContext.TrackStartedAt = null;` | Forgets the start time when the title is cleared (pause, stop, plugin disabled, AFK pause, long poll failure), alongside the existing `SecsElapsed` reset. The next playing poll sets a fresh one. |

### `SpotifyHonorific/Utils/TemplateHelper.cs`

Builds the template's `Activity` with `ElapsedMs`, and marks templates using it as time-dependent.

```diff
 using Scriban;
+using Scriban.Runtime;
 using SpotifyHonorific.Activities;
+using System;
 using System.Linq;
```

| Line | Explanation |
|---|---|
| `using Scriban.Runtime;` | For `ScriptObject` and `ScriptMemberImportFlags`, used to build the wrapped `Activity`. |
| `using System;` | For `DateTimeOffset`. |

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
+           || config.TitleTemplate.Contains("Elapsed");
```

| Line | Explanation |
|---|---|
| Doc comment (4 lines modified) | Same comment, updated to say `Activity.ElapsedMs` is now also a time-varying variable. The wording about false positives is kept. |
| `=> config.RainbowMode` | Unchanged condition, now on its own line. |
| `\|\| config.TitleTemplate.Contains("Context")` | Unchanged condition. |
| `\|\| config.TitleTemplate.Contains("Elapsed")` | New: a template mentioning `Elapsed` (i.e. `Activity.ElapsedMs`) changes over time, so it must keep re-rendering. Without this, a template using only `ElapsedMs` (no `Context`) would render once and freeze, because the updater skips re-rendering titles it considers static. Matching `"Elapsed"` also matches `SecsElapsed`, which already contains `Context`, so nothing changes for existing templates; extra matches only cause harmless re-renders. |

```diff
+
+    /// <summary>
+    /// Builds the template's Activity: the track's own members plus ElapsedMs, how
+    /// far into the track playback is in milliseconds (matching DurationMs).
+    /// </summary>
+    public static ScriptObject CreateActivity(object track, DateTimeOffset? trackStartedAt, DateTimeOffset now)
+    {
+        var activity = new ScriptObject();
+        activity.Import(track, ScriptMemberImportFlags.Field | ScriptMemberImportFlags.Property, renamer: member => member.Name);
+        activity["ElapsedMs"] = (int?)(now - trackStartedAt)?.TotalMilliseconds;
+        return activity;
+    }
```

| Line | Explanation |
|---|---|
| (blank line) | Spacing after `IsTimeDependent`. |
| `/// <summary>` … `/// </summary>` | Doc comment: what `Activity` now contains and that `ElapsedMs` is milliseconds like `DurationMs`. |
| `public static ScriptObject CreateActivity(object track, DateTimeOffset? trackStartedAt, DateTimeOffset now)` | Shared by the real title render and the config window preview. `track` is `object` because the preview passes a mock anonymous object when nothing is playing, not a real `FullTrack`. `now` is a parameter so tests can fix the clock. |
| `{` | Method body. |
| `var activity = new ScriptObject();` | A Scriban object: a dictionary of template variables. Needed because `Activity` is SpotifyAPI's `FullTrack`, a library class that can't be given new properties. |
| `activity.Import(track, ScriptMemberImportFlags.Field \| ScriptMemberImportFlags.Property, renamer: member => member.Name);` | Copies every public field and property of the track into the object under its exact name (`Name`, `Artists`, `Album`, `DurationMs`, `Popularity`, …), so every existing `Activity.*` variable keeps working. Methods aren't imported (not needed). `renamer: member => member.Name` keeps names as-is, the same renamer the plugin already passes to `Render`. |
| `activity["ElapsedMs"] = (int?)(now - trackStartedAt)?.TotalMilliseconds;` | Adds `Activity.ElapsedMs`: time since the song started, in milliseconds, as a whole number (`int`, the same type as `DurationMs`; the cast drops fractions of a millisecond). `null` if there's no start time. |
| `return activity;` | The object templates see as `Activity`. |
| `}` | End of method. |

### `SpotifyHonorific/Core/TitleRenderingService.cs`

Renders real titles with the wrapped `Activity`.

```diff
 using SpotifyHonorific.Updaters;
+using SpotifyHonorific.Utils;
+using System;
 using System.Collections.Generic;
```

| Line | Explanation |
|---|---|
| `using SpotifyHonorific.Utils;` | For `TemplateHelper`. |
| `using System;` | For `DateTimeOffset`. |

```diff
-        var title = template.Render(new { Activity = track, Context = context }, member => member.Name);
+        var activity = TemplateHelper.CreateActivity(track, context.TrackStartedAt, DateTimeOffset.UtcNow);
+        var title = template.Render(new { Activity = activity, Context = context }, member => member.Name);
```

| Line | Explanation |
|---|---|
| `var activity = TemplateHelper.CreateActivity(track, context.TrackStartedAt, DateTimeOffset.UtcNow);` | Builds the `Activity` for this render, with `ElapsedMs` computed from the stored start time and the current time. |
| `var title = template.Render(new { Activity = activity, Context = context }, member => member.Name);` | Modified: same render call as before, with `Activity = activity` (the wrapped track) instead of `Activity = track`. `Context` and the renamer are unchanged. |

### `SpotifyHonorific/Windows/ConfigWindow.cs`

Makes the Live Preview and the "Available Template Variables" table show `ElapsedMs`.

**Live Preview** (`DrawTemplatePreview`):

```diff
         var isMock = playbackState.CurrentTrack == null;
         var track = isMock ? (object)CreateMockSpotifyTrack() : playbackState.CurrentTrack!;
-        var mockContext = new UpdaterContext { SecsElapsed = 0 };
+        var now = DateTimeOffset.UtcNow;
+        var mockContext = new UpdaterContext { SecsElapsed = 0, TrackStartedAt = now };
+        var activity = TemplateHelper.CreateActivity(track, mockContext.TrackStartedAt, now);
```

| Line | Explanation |
|---|---|
| `var now = DateTimeOffset.UtcNow;` | One timestamp for the preview, used twice below. |
| `var mockContext = new UpdaterContext { SecsElapsed = 0, TrackStartedAt = now };` | Modified: the preview's mock context now also says the song "started now". The preview already shows `SecsElapsed` as 0; this makes `ElapsedMs` show 0 too, instead of empty, so a template like `{{ Activity.ElapsedMs // 1000 }}` previews as `0` without errors. |
| `var activity = TemplateHelper.CreateActivity(track, mockContext.TrackStartedAt, now);` | Builds the same wrapped `Activity` as real renders, from the current track or the mock track. |

```diff
-                var renderedTitle = titleTemplate.Render(new { Activity = track, Context = mockContext }, member => member.Name);
+                var renderedTitle = titleTemplate.Render(new { Activity = activity, Context = mockContext }, member => member.Name);
```

| Line | Explanation |
|---|---|
| `var renderedTitle = …Render(new { Activity = activity, … })` | Modified: the preview renders with the wrapped `Activity` instead of the raw track, matching real titles. |

**Variable table** (`DrawTemplateVariablesTable`):

```diff
             ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Activity.DurationMs"); ImGui.TableNextColumn(); ImGui.Text("System.Int32");
+            ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Activity.ElapsedMs"); ImGui.TableNextColumn(); ImGui.Text("System.Int32");
             ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Activity.Popularity"); ImGui.TableNextColumn(); ImGui.Text("System.Int32");
```

| Line | Explanation |
|---|---|
| `…ImGui.Text("Activity.ElapsedMs"); … ImGui.Text("System.Int32");` | Adds a row to "Available Template Variables", right after `Activity.DurationMs`, in the same format as the other hard-coded `Activity.*` rows. (The `Context.*` rows are generated by reflection from public properties, which is why `TrackStartedAt` is `internal`.) |

---

## README

```diff
 | `Activity.DurationMs` | Int | Total duration of the track in milliseconds. |
+| `Activity.ElapsedMs` | Int | How far into the track playback is in milliseconds, as reported by Spotify. |
 | `Activity.Popularity` | Int | The popularity score (0-100). |
```

| Line | Explanation |
|---|---|
| `\| Activity.ElapsedMs \| Int \| …` | New row in the "Template Variables" table, after `Activity.DurationMs` and in the same format. Upstream's `Context.SecsElapsed` row is left as it is. |

```diff
+**Song position**
+Shows the song name and how far into it you are, e.g. `Never Gonna Give You Up 1:05`.
+
+```scriban
+{{ Activity.Name | string.truncate 26 }} {{ Activity.ElapsedMs // 60000 }}:{{ Activity.ElapsedMs // 1000 % 60 | math.format "00" }}
+```
+
```

| Line | Explanation |
|---|---|
| `**Song position**` | New example heading, in the style of the existing examples. |
| `Shows the song name…` | One-line description with sample output. |
| (blank line) | Matches the spacing of the other examples. |
| Opening fence (`scriban`) | Opens the code block, same language tag as the others. |
| `{{ Activity.Name \| string.truncate 26 }} …` | Song name cut to 26 characters, a space, then minutes (`ElapsedMs // 60000`; `//` is Scriban's whole-number division), `:`, and seconds (`ElapsedMs // 1000 % 60`, padded to two digits with `math.format "00"`). At most 26 + 1 + 5 = 32 characters, Honorific's limit. Verified by a unit test. |
| Closing fence | Closes the code block. |
| (blank line) | Spacing before the next section. |

---

## Tests

All added; no existing test was changed. The suite has 276 tests, all passing.

### `SpotifyHonorific.Tests/UpdaterTests.cs`

Added to the existing `UpdaterTests` class:

```csharp
[Fact]
public void UpdaterContext_TrackStartedAt_DefaultsToNull()
{
    new UpdaterContext().TrackStartedAt.Should().BeNull();
}
```
A new context has no start time until a playing poll sets one.

```csharp
[Fact]
public void UpdaterContext_TrackStartedAt_IsNotATemplateVariable()
{
    // The config window lists public UpdaterContext properties as template
    // variables; the start time is exposed only as Activity.ElapsedMs.
    typeof(UpdaterContext).GetProperties().Select(p => p.Name)
        .Should().NotContain(nameof(UpdaterContext.TrackStartedAt));
}
```
Guards the `internal` choice: `GetProperties()` (what the config window uses) must not see `TrackStartedAt`, so no `Context.TrackStartedAt` variable appears.

New class `UpdaterTrackStartedAtTests`, testing `Updater.ComputeTrackStartedAt` with a fixed clock:

```csharp
public class UpdaterTrackStartedAtTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
```
A fixed "now" so results are exact.

| Test | What it checks |
|---|---|
| `ComputeTrackStartedAt_FromProgress_IsNowMinusProgress` | 83,000 ms of progress → the song started 83 s before now. |
| `ComputeTrackStartedAt_AtTrackStart_IsNow` | Progress 0 → started exactly now. |
| `ComputeTrackStartedAt_NoProgress_IsNull` | No progress from Spotify → no start time. |
| `ComputeTrackStartedAt_SeekOrNewTrack_ResyncsToTheNewPosition` | A first poll at 2:00, then a poll 2 s later at 0:05 (seek back or new song): the new start time is now − 3 s, later than the first one. Each poll replaces the old value, which is how seeks are followed. |

### `SpotifyHonorific.Tests/TemplateHelperTests.cs`

```diff
+using SpotifyAPI.Web;
```
For `FullTrack`, `SimpleArtist` and `SimpleAlbum`, used to build test tracks.

Added to `TemplateHelperIsTimeDependentTests`:

```csharp
[Fact]
public void IsTimeDependent_TemplateReferencesActivityElapsedMs_ReturnsTrue()
{
    var config = new ActivityConfig
    {
        TitleTemplate = "{{ Activity.Name }} {{ Activity.ElapsedMs // 1000 }}",
        RainbowMode = false
    };
    TemplateHelper.IsTimeDependent(config).Should().BeTrue();
}
```
A template that uses `ElapsedMs` but not `Context`, with rainbow off, is treated as time-dependent, so it keeps re-rendering.

New class `TemplateHelperCreateActivityTests`, testing `CreateActivity` with a fixed clock and a test track (`Song` by `Artist`, album `Album`, 213,000 ms):

| Test | What it checks |
|---|---|
| `CreateActivity_ElapsedMs_IsMillisecondsSinceTrackStarted` | Started 83,456 ms before now → `ElapsedMs` is exactly `83456`. |
| `CreateActivity_ElapsedMs_IsAnIntLikeDurationMs` | `ElapsedMs` and `DurationMs` are both `int`, the design decision in #16. |
| `CreateActivity_NoStartTime_ElapsedMsIsNull` | No start time → `ElapsedMs` is null. |
| `CreateActivity_KeepsTrackMembers` | The wrapper still carries the track's own members (`Name`, `DurationMs`). |

### `SpotifyHonorific.Tests/TitleRenderingServiceTests.cs`

New class `TitleRenderingServiceActivityElapsedMsTests`: renders real templates through `TitleRenderingService.RenderTitle`, the same path the plugin uses in game.

```csharp
    private static UpdaterContext ContextAt(double secondsIntoTrack) => new()
    {
        TrackStartedAt = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(secondsIntoTrack + 0.2),
    };
```
Makes a context "N seconds into the song". The extra 0.2 s keeps the value safely inside that second, so a slow test run can't tip the result to the next second.

```csharp
    private string? Render(string template, UpdaterContext context)
        => _service.RenderTitle(new ActivityConfig { TitleTemplate = template }, MakeTrack(), context);
```
Renders a template against a test track (`Never Gonna Give You Up` by `Rick Astley`, 213,000 ms). The constructor and `MakeTrack` set up the service and track the same way the existing test classes do.

| Test | What it checks |
|---|---|
| `RenderTitle_ActivityElapsedMs_RendersMilliseconds` | `{{ Activity.ElapsedMs // 1000 }}` at 83 s renders `83`. |
| `RenderTitle_ReadmeSongPositionExample_Renders` | The README "Song position" example at 1:05 renders exactly `Never Gonna Give You Up 1:05`. |
| `RenderTitle_RemainingTimeFromDurationMs_Renders` | `{{ (Activity.DurationMs - Activity.ElapsedMs) // 1000 }}` at 13 s renders `199`: the two variables work together for remaining time. |
| `RenderTitle_ExistingActivityVariables_StillRender` (4 cases) | `Activity.Name`, `Activity.Artists[0].Name`, `Activity.Album.Name` and `Activity.DurationMs` render exactly as before through the wrapper. |
| `RenderTitle_ContextSecsElapsed_Unchanged` | `Context.SecsElapsed` still renders its own value (12.7 → `12`), independent of `ElapsedMs`. |

---

## Not changed

- `Context.SecsElapsed`: how it counts, when it resets, and its README row.
- Polling: no extra Spotify requests. `ProgressMs` was already in every poll result.
- `ActivityConfig` defaults, the filter-template preview, title serialisation, and everything else in the plugin.

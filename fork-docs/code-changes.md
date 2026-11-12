# Code changes: `Activity.ProgressMs`

Fork-only. Every line added or modified to re-add the song's playback position as `Activity.ProgressMs`, shown and explained, with how each line follows the original [DiscordActivityHonorific](https://github.com/anya-hichu/DiscordActivityHonorific) (anya's plugin, "the original" below). This is the net change against upstream: `git diff upstream/master -- SpotifyHonorific README.md`.

- **Plugin code:** 5 files, 11 lines added, 4 lines replaced. No new `using` lines, no new methods.
- **README:** 1 line added.

Issues: #6 (re-add the variable), #16 (milliseconds), #22 (align with the original), #25 (`ProgressMs`, minimal change).

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

Discord sends a new presence, with a new `StartedAt`, when a song starts, resumes or is seeked. The original re-rendered the title every 100 ms. It had no live preview and no README variable list; its config window listed variables by reflecting over the activity type.

## How this follows it

The same pattern: **store when the song started; compute the position at render time.**

1. Each Spotify poll while playing stores the start time (`now - progress_ms`). This is the equivalent of Discord's `StartedAt`.
2. Each render computes `ProgressMs = now - start`, the same calculation as `Elapsed`, and puts it on `Activity` next to the track's own variables.

### Differences from the original, and why

| Original | Here | Why |
|---|---|---|
| `Activity.Elapsed`, a `TimeSpan` | `Activity.ProgressMs`, an `int` in milliseconds | Design decision: this plugin's track variables are simple values, and `Activity.DurationMs` is an `int` in ms. `ProgressMs` matches it, and is Spotify's own name for this value (`progress_ms`). |
| Start time from Discord presence timestamps | Start time from Spotify's `progress_ms` on each poll | This plugin talks to the Spotify Web API, not Discord. |
| Start time and `Elapsed` live on the activity object | Start time on `UpdaterContext` (internal); `Activity` is a small wrapper | `Activity` here is the Spotify library's `FullTrack`, which can't be given new members. |
| Re-rendered every 100 ms regardless | `ProgressMs` added to upstream's time-dependence check | Upstream only keeps re-rendering titles it considers time-dependent; without this, a `ProgressMs` title would freeze. |
| Variable table by reflection | One hard-coded row | Upstream hard-codes its `Activity.*` rows. |
| No live preview or README list | Preview shows 0, one README row | Following upstream's conventions. |

---

## Plugin code

### `SpotifyHonorific/Updaters/UpdaterContext.cs`

```diff
     public double SecsElapsed { get; set; }
+    internal System.DateTimeOffset? TrackStartedAt { get; set; }
 }
```

| Line | Explanation | Original |
|---|---|---|
| `internal System.DateTimeOffset? TrackStartedAt { get; set; }` | When the current song started, from Spotify's progress. `internal`, so templates and the config window (which only see public properties) don't get a new `Context.*` variable. `System.` written out instead of adding a `using` line. Nullable: none until the first playing poll. | Same type as `SpotifyGame.StartedAt`. The original kept it on the activity; here the context is the object the plugin owns and passes to every render. |

### `SpotifyHonorific/Updaters/Updater.cs`

```diff
         if (outcome == PollOutcome.Playing && track != null)
         {
+            _updaterContext.TrackStartedAt = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(result!.ProgressMs ?? 0);
             _isMusicPlaying = true;
```

| Line | Explanation | Original |
|---|---|---|
| `_updaterContext.TrackStartedAt = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(result!.ProgressMs ?? 0);` | On every poll that finds a song playing, the start time is "now minus how far in Spotify says we are". Placed before `ProcessCurrentlyPlayingTrack` (which returns early when the song hasn't changed), so it runs on every poll and picks up seeks. `ProgressMs` was already fetched by every poll and only used for the poll interval. `result!` is safe: the outcome is only `Playing` when there's a result. `?? 0` treats a missing progress as the start of the song. | Discord.Net set `StartedAt` from each presence update; this is the per-poll equivalent. |

Nothing is reset when the title clears: nothing renders after a clear, and the next playing poll always sets a fresh start time first. In the original, the old activity was likewise just replaced.

### `SpotifyHonorific/Core/TitleRenderingService.cs`

```diff
-        var title = template.Render(new { Activity = track, Context = context }, member => member.Name);
+        var activity = new Scriban.Runtime.ScriptObject { ["ProgressMs"] = (int?)(System.DateTimeOffset.UtcNow - context.TrackStartedAt)?.TotalMilliseconds };
+        Scriban.Runtime.ScriptObjectExtensions.Import(activity, track, renamer: member => member.Name);
+        var title = template.Render(new { Activity = activity, Context = context }, member => member.Name);
```

| Line | Explanation | Original |
|---|---|---|
| `var activity = new Scriban.Runtime.ScriptObject { ["ProgressMs"] = … };` | Creates the template's `Activity`: a Scriban object (a dictionary of template variables), starting with `ProgressMs` = now minus the start time, in whole milliseconds (`int`, like `DurationMs`). Computed at every render, so it keeps counting between polls. `null` before the first playing poll. | The same calculation as `SpotifyGame.Elapsed => DateTimeOffset.UtcNow - StartedAt`, also done at render time. The wrapper is needed only because `FullTrack` can't hold a new property; the original's object already had `Elapsed`. |
| `Scriban.Runtime.ScriptObjectExtensions.Import(activity, track, renamer: member => member.Name);` | Copies the track's public members into the same object under their exact names, so `Activity.Name`, `Activity.Artists`, `Activity.DurationMs` and the rest work exactly as before. `Import` is an extension method; it's called by its full name to avoid a new `using` line. | Same naming as the original's render (`member => member.Name`). |
| `var title = template.Render(new { Activity = activity, Context = context }, member => member.Name);` | Modified: `Activity = activity` instead of `Activity = track`; otherwise the same call. | Identical shape to the original's render call. |

### `SpotifyHonorific/Utils/TemplateHelper.cs`

```diff
     /// True if the rendered title can change over time: rainbow mode animates the
-    /// color, and "Context" is the only time-varying render variable (exact member
+    /// color, and "Context" and "ProgressMs" are the time-varying render variables (exact member
     /// name; Scriban's renamer exposes property names verbatim). False positives
     /// only cause unnecessary re-renders, which is the safe direction.
     /// </summary>
     public static bool IsTimeDependent(ActivityConfig config)
-        => config.RainbowMode || config.TitleTemplate.Contains("Context");
+        => config.RainbowMode || config.TitleTemplate.Contains("Context") || config.TitleTemplate.Contains("ProgressMs");
```

| Line | Explanation | Original |
|---|---|---|
| Comment line | Updated so it no longer says `Context` is the only time-varying variable. | — |
| `=> … \|\| config.TitleTemplate.Contains("ProgressMs");` | A template using `ProgressMs` changes over time, so the updater keeps re-rendering it (every 0.5 s). Without this, a template using only `ProgressMs` would render once and freeze. Matches the exact name only. | The original had no such check; it re-rendered every 100 ms. This gives the same result for templates that need it. |

### `SpotifyHonorific/Windows/ConfigWindow.cs`

**Live Preview:**

```diff
         var mockContext = new UpdaterContext { SecsElapsed = 0 };
+        var activity = new Scriban.Runtime.ScriptObject { ["ProgressMs"] = 0 };
+        Scriban.Runtime.ScriptObjectExtensions.Import(activity, track, renamer: member => member.Name);
```

```diff
-                var renderedTitle = titleTemplate.Render(new { Activity = track, Context = mockContext }, member => member.Name);
+                var renderedTitle = titleTemplate.Render(new { Activity = activity, Context = mockContext }, member => member.Name);
```

| Line | Explanation | Original |
|---|---|---|
| `var activity = new Scriban.Runtime.ScriptObject { ["ProgressMs"] = 0 };` | The preview's `Activity` shows `ProgressMs` as 0, just as the preview shows `SecsElapsed` as 0. | The original had no live preview. |
| `Scriban.Runtime.ScriptObjectExtensions.Import(activity, track, …);` | Same as in the real render: the current (or mock) track's variables. | — |
| `var renderedTitle = …Render(new { Activity = activity, … })` | Modified: the preview renders with the same kind of `Activity` as real titles. | — |

**Variable table:**

```diff
             …ImGui.Text("Activity.DurationMs"); ImGui.TableNextColumn(); ImGui.Text("System.Int32");
+            ImGui.TableNextRow(); ImGui.TableNextColumn(); ImGui.Text("Activity.ProgressMs"); ImGui.TableNextColumn(); ImGui.Text("System.Int32");
```

| Line | Explanation | Original |
|---|---|---|
| `…ImGui.Text("Activity.ProgressMs"); … ImGui.Text("System.Int32");` | One row after `Activity.DurationMs` in "Available Template Variables", in upstream's hard-coded format. | The original's table listed `Activity.Elapsed` as one row (by reflection). Same result: one row for the variable. |

---

## README

```diff
 | `Activity.DurationMs` | Int | Total duration of the track in milliseconds. |
+| `Activity.ProgressMs` | Int | How far into the track playback is in milliseconds. |
```

| Line | Explanation | Original |
|---|---|---|
| `\| Activity.ProgressMs \| Int \| …` | One row after `Activity.DurationMs`, same format. | The original's README listed no variables. |

using Scriban;
using Scriban.Runtime;
using SpotifyHonorific.Activities;
using System.Linq;

namespace SpotifyHonorific.Utils;

public static class TemplateHelper
{
    /// <summary>
    /// Extracts error messages from a Scriban template and joins them with semicolons.
    /// </summary>
    public static string GetTemplateErrors(Template template)
        => string.Join("; ", template.Messages.Select(m => m.Message));

    /// <summary>
    /// True if the rendered title can change over time: rainbow mode animates the
    /// color, and "Context" and "Activity.ElapsedMs" are the time-varying render
    /// variables (exact member names; Scriban's renamer exposes property names
    /// verbatim). False positives only cause unnecessary re-renders, which is the
    /// safe direction.
    /// </summary>
    public static bool IsTimeDependent(ActivityConfig config)
        => config.RainbowMode
           || config.TitleTemplate.Contains("Context")
           || config.TitleTemplate.Contains("ElapsedMs");

    /// <summary>
    /// Builds the template's Activity: the track's own members plus ElapsedMs, how
    /// far into the track playback is in milliseconds (matching DurationMs).
    /// </summary>
    public static ScriptObject CreateActivity(object track, int? elapsedMs)
    {
        var activity = new ScriptObject();
        activity.Import(track, ScriptMemberImportFlags.Field | ScriptMemberImportFlags.Property, renamer: member => member.Name);
        activity["ElapsedMs"] = elapsedMs;
        return activity;
    }
}

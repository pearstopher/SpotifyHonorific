using System;

namespace SpotifyHonorific.Updaters;

public class UpdaterContext
{
    public double SecsElapsed { get; set; }

    // When the current track started playing, per Spotify's reported progress.
    // Internal so it stays out of the template variables; templates see it as
    // Activity.ElapsedMs.
    internal DateTimeOffset? TrackStartedAt { get; set; }
}

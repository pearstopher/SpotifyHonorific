using FluentAssertions;
using SpotifyAPI.Web;
using SpotifyHonorific.Activities;
using SpotifyHonorific.Utils;

namespace SpotifyHonorific.Tests;

public class TemplateHelperIsTimeDependentTests
{
    [Fact]
    public void IsTimeDependent_StaticTemplate_ReturnsFalse()
    {
        var config = new ActivityConfig
        {
            TitleTemplate = "♪{{ Activity.Name | string.truncate 28 }}♪",
            RainbowMode = false
        };
        TemplateHelper.IsTimeDependent(config).Should().BeFalse();
    }

    [Fact]
    public void IsTimeDependent_TemplateReferencesContext_ReturnsTrue()
    {
        var config = new ActivityConfig
        {
            TitleTemplate = "{{ if (Context.SecsElapsed % 30) < 10 }}A{{ else }}B{{ end }}",
            RainbowMode = false
        };
        TemplateHelper.IsTimeDependent(config).Should().BeTrue();
    }

    [Fact]
    public void IsTimeDependent_RainbowModeWithStaticTemplate_ReturnsTrue()
    {
        var config = new ActivityConfig
        {
            TitleTemplate = "♪{{ Activity.Name }}♪",
            RainbowMode = true
        };
        TemplateHelper.IsTimeDependent(config).Should().BeTrue();
    }

    [Fact]
    public void IsTimeDependent_EmptyTemplateNoRainbow_ReturnsFalse()
    {
        var config = new ActivityConfig { TitleTemplate = string.Empty, RainbowMode = false };
        TemplateHelper.IsTimeDependent(config).Should().BeFalse();
    }

    [Fact]
    public void IsTimeDependent_TemplateReferencesActivityElapsed_ReturnsTrue()
    {
        var config = new ActivityConfig
        {
            TitleTemplate = "{{ Activity.Name }} {{ Activity.Elapsed.Minutes }}",
            RainbowMode = false
        };
        TemplateHelper.IsTimeDependent(config).Should().BeTrue();
    }

    [Fact]
    public void IsTimeDependent_LowercaseContext_ReturnsFalse()
    {
        // Scriban exposes exact member names (renamer: member => member.Name),
        // so only the exact spelling "Context" is a time-varying reference.
        var config = new ActivityConfig
        {
            TitleTemplate = "{{ Activity.Name }} in context",
            RainbowMode = false
        };
        TemplateHelper.IsTimeDependent(config).Should().BeFalse();
    }
}

public class TemplateHelperCreateActivityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private static FullTrack MakeTrack() => new()
    {
        Id = "track1",
        Name = "Song",
        Artists = [new SimpleArtist { Name = "Artist" }],
        Album = new SimpleAlbum { Name = "Album" },
        DurationMs = 213_000,
    };

    [Fact]
    public void CreateActivity_Elapsed_IsTimeSinceTrackStarted()
    {
        var activity = TemplateHelper.CreateActivity(MakeTrack(), Now - TimeSpan.FromSeconds(83), Now);

        activity["Elapsed"].Should().Be(TimeSpan.FromSeconds(83));
    }

    [Fact]
    public void CreateActivity_NoStartTime_ElapsedIsNull()
    {
        var activity = TemplateHelper.CreateActivity(MakeTrack(), null, Now);

        activity["Elapsed"].Should().BeNull();
    }

    [Fact]
    public void CreateActivity_KeepsTrackMembers()
    {
        var activity = TemplateHelper.CreateActivity(MakeTrack(), Now, Now);

        activity["Name"].Should().Be("Song");
        activity["DurationMs"].Should().Be(213_000);
    }
}

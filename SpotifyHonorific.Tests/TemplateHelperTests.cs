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
    public void IsTimeDependent_TemplateReferencesActivityElapsedMs_ReturnsTrue()
    {
        var config = new ActivityConfig
        {
            TitleTemplate = "{{ Activity.Name }} {{ Activity.ElapsedMs // 1000 }}",
            RainbowMode = false
        };
        TemplateHelper.IsTimeDependent(config).Should().BeTrue();
    }

    [Fact]
    public void IsTimeDependent_WordElapsedWithoutVariable_ReturnsFalse()
    {
        // Only the exact variable name ElapsedMs is time-varying.
        var config = new ActivityConfig
        {
            TitleTemplate = "Elapsed: {{ Activity.Name }}",
            RainbowMode = false
        };
        TemplateHelper.IsTimeDependent(config).Should().BeFalse();
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
    private static FullTrack MakeTrack() => new()
    {
        Id = "track1",
        Name = "Song",
        Artists = [new SimpleArtist { Name = "Artist" }],
        Album = new SimpleAlbum { Name = "Album" },
        DurationMs = 213_000,
    };

    [Fact]
    public void CreateActivity_ElapsedMs_IsTheGivenValue()
    {
        var activity = TemplateHelper.CreateActivity(MakeTrack(), 83_456);

        activity["ElapsedMs"].Should().Be(83_456);
    }

    [Fact]
    public void CreateActivity_ElapsedMs_IsAnIntLikeDurationMs()
    {
        var activity = TemplateHelper.CreateActivity(MakeTrack(), 83_000);

        activity["ElapsedMs"].Should().BeOfType<int>();
        activity["DurationMs"].Should().BeOfType<int>();
    }

    [Fact]
    public void CreateActivity_NoElapsed_ElapsedMsIsNull()
    {
        var activity = TemplateHelper.CreateActivity(MakeTrack(), null);

        activity["ElapsedMs"].Should().BeNull();
    }

    [Fact]
    public void CreateActivity_KeepsTrackMembers()
    {
        var activity = TemplateHelper.CreateActivity(MakeTrack(), 0);

        activity["Name"].Should().Be("Song");
        activity["DurationMs"].Should().Be(213_000);
    }
}

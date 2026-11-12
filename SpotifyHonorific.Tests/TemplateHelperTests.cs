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

    // Same shape as Discord.Net's SpotifyGame, which DiscordActivityHonorific
    // passed to templates as Activity:
    //   public DateTimeOffset? StartedAt { get; }
    //   public TimeSpan? Elapsed => DateTimeOffset.UtcNow - StartedAt;
    // with the clock fixed so both sides see the same instant.
    private sealed class OriginalSpotifyActivity(DateTimeOffset? startedAt, DateTimeOffset now)
    {
        public DateTimeOffset? StartedAt { get; } = startedAt;
        public TimeSpan? Elapsed => now - StartedAt;
    }

    private static string RenderWith(object activity, string template)
        => Scriban.Template.Parse(template).Render(new { Activity = activity }, member => member.Name);

    [Theory]
    [InlineData("{{ Activity.Elapsed }}")]
    [InlineData("{{ Activity.Elapsed.TotalSeconds }}")]
    [InlineData("{{ Activity.Elapsed.TotalSeconds | math.floor }}")]
    [InlineData("{{ Activity.Elapsed.TotalMinutes }}")]
    [InlineData("{{ Activity.Elapsed.TotalMilliseconds }}")]
    [InlineData("{{ Activity.Elapsed.Hours }}:{{ Activity.Elapsed.Minutes }}:{{ Activity.Elapsed.Seconds }}.{{ Activity.Elapsed.Milliseconds }}")]
    [InlineData("{{ (Activity.Elapsed.TotalSeconds / 60) | math.floor }}:{{ (Activity.Elapsed.TotalSeconds % 60) | math.floor }}")]
    [InlineData("{{ Activity.Elapsed.Hours > 0 ? \"h\" : \"m\" }}")]
    [InlineData("{{ Activity.Elapsed.TotalSeconds > 60 }}")]
    [InlineData("{{ Activity.Elapsed == null }}")]
    public void CreateActivity_Elapsed_RendersLikeTheOriginalActivity(string template)
    {
        var startedAt = Now - TimeSpan.FromMilliseconds(3_723_456); // 1:02:03.456 in

        var original = RenderWith(new OriginalSpotifyActivity(startedAt, Now), template);
        var current = RenderWith(TemplateHelper.CreateActivity(MakeTrack(), startedAt, Now), template);

        current.Should().Be(original);
    }

    [Theory]
    [InlineData("[{{ Activity.Elapsed }}]")]
    [InlineData("{{ Activity.Elapsed == null }}")]
    public void CreateActivity_NoStartTime_RendersLikeTheOriginalActivity(string template)
    {
        var original = RenderWith(new OriginalSpotifyActivity(null, Now), template);
        var current = RenderWith(TemplateHelper.CreateActivity(MakeTrack(), null, Now), template);

        current.Should().Be(original);
    }

    [Fact]
    public void CreateActivity_NoStartTime_MemberAccessFailsLikeTheOriginalActivity()
    {
        const string template = "{{ Activity.Elapsed.TotalSeconds }}";

        var original = () => RenderWith(new OriginalSpotifyActivity(null, Now), template);
        var current = () => RenderWith(TemplateHelper.CreateActivity(MakeTrack(), null, Now), template);

        var originalError = original.Should().Throw<Scriban.Syntax.ScriptRuntimeException>().Which.OriginalMessage;
        current.Should().Throw<Scriban.Syntax.ScriptRuntimeException>().Which.OriginalMessage.Should().Be(originalError);
    }

    [Fact]
    public void CreateActivity_KeepsTrackMembers()
    {
        var activity = TemplateHelper.CreateActivity(MakeTrack(), Now, Now);

        activity["Name"].Should().Be("Song");
        activity["DurationMs"].Should().Be(213_000);
    }
}

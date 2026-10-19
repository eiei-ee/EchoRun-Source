using NUnit.Framework;

public sealed class CityTransitAudioTests
{
    [TestCase(GameState.Menu, 1f, false)]
    [TestCase(GameState.Paused, 1f, false)]
    [TestCase(GameState.GameOver, 1f, false)]
    [TestCase(GameState.Playing, 0f, false)]
    [TestCase(GameState.Playing, 1f, true)]
    public void NonPlayingPausedAndMutedStatesAreSilent(GameState state, float timeScale, bool muted)
    {
        Assert.That(CityTransitAudio.ResolveVolume(state, timeScale, .12f, 1f, 1f, muted), Is.Zero);
    }

    [Test]
    public void PlayingVolumeUsesBothExistingUserVolumeControls()
    {
        Assert.That(CityTransitAudio.ResolveVolume(GameState.Playing, 1f, .12f, .5f, .25f, false),
            Is.EqualTo(.015f).Within(.00001f));
        Assert.That(CityTransitAudio.ResolveVolume(GameState.Playing, 1f, .12f, 1f, 0f, false), Is.Zero);
        Assert.That(CityTransitAudio.ResolveVolume(GameState.Playing, 1f, .12f, 0f, 1f, false), Is.Zero);
    }
}

using UnityEngine;

// One spatial source on the middle carriage. Playback follows the game's
// existing state and user sound settings; the supplied clip owns the timbre.
public sealed class CityTransitAudio : MonoBehaviour
{
    public AudioSource source;
    [Range(0f, 1f)] public float baseVolume = .12f;
    private bool _paused;

    private void OnEnable()
    {
        StopSource();
    }

    private void Update()
    {
        if (source == null || !source.enabled || source.clip == null) return;
        GameManager game = GameManager.Instance;
        AudioManager audio = AudioManager.Instance;
        GameState state = game != null ? game.State : GameState.Menu;
        source.volume = ResolveVolume(state, Time.timeScale, baseVolume,
            audio != null ? audio.EffectiveMasterVolume : 0f,
            audio != null ? audio.sfxVolume : 0f, audio == null || audio.IsMuted);

        if (state == GameState.Menu || state == GameState.GameOver)
        {
            StopSource();
            return;
        }
        if (source.volume <= 0f)
        {
            if (source.isPlaying)
            {
                source.Pause();
                _paused = true;
            }
            return;
        }
        if (_paused)
        {
            source.UnPause();
            _paused = false;
        }
        else if (!source.isPlaying)
            source.Play();
    }

    public static float ResolveVolume(GameState state, float timeScale,
        float level, float master, float effects, bool muted)
    {
        if (state != GameState.Playing || timeScale <= 0f || muted) return 0f;
        return Mathf.Clamp01(level) * Mathf.Clamp01(master) * Mathf.Clamp01(effects);
    }

    private void OnDisable()
    {
        StopSource();
    }

    private void StopSource()
    {
        if (source != null) source.Stop();
        _paused = false;
    }
}

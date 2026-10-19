using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    public event System.Action<int> FootstepEmitted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (Instance != null || FindObjectOfType<AudioManager>() != null) return;
        new GameObject("Audio Manager").AddComponent<AudioManager>();
    }

    [Header("Audio Sources")]
    private AudioSource _sfxSource;
    private AudioSource _musicSource;
    private AudioLowPassFilter _musicFilter;
    private bool _hasFocus = true;
    private bool _applicationPaused;
    private bool _musicSuspended;
    private float _musicEnvelope;
    private float _duckRemaining;
    private const float MusicHeadroom = 0.55f;
    private const float EffectsHeadroom = 0.7f;
    private AudioSource _slideLoopSource;
    private AudioSource _impactSource;
    private AudioSource _dodgeResultSource;
    private AudioSource _speedWindLoopSource;

    [Header("SFX Clips (assign .wav/.mp3/.ogg, or leave null for procedural beeps)")]
    public AudioClip jumpClip;
    public AudioClip slideClip;
    public AudioClip slideLoopClip;
    public AudioClip slideExitClip;
    public AudioClip landClip;
    public AudioClip coinClip;
    public AudioClip dodgeObstacleClip;
    public AudioClip deathClip;
    public AudioClip footstepClip;
    public AudioClip[] footstepClips;
    public AudioClip collisionClip;
    public AudioClip impactRecoverClip;
    public AudioClip impactFatalClip;
    public AudioClip counterSuccessClip;
    public AudioClip speedWindLoopClip;
    public AudioClip uiClickClip;
    public AudioClip uiConfirmClip;
    public AudioClip uiErrorClip;

    [Header("Music")]
    public AudioClip bgmClip;
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public bool muted;

    public bool IsMuted => muted;
    public float EffectiveMasterVolume => muted || !_hasFocus || _applicationPaused
        ? 0f : Mathf.Clamp01(masterVolume);

    [Header("Footsteps")]
    public float footstepInterval = 0.35f;
    [Range(0f, 1f)] public float slideLoopVolume = 0.34f;
    [Range(0.12f, 0.35f)] public float minimumFootstepInterval = 0.2f;
    [Range(1f, 20f)] public float speedFeedbackResponse = 7f;
    [Range(0f, 0.2f)] public float speedWindLoopVolume = 0.12f;
    private float _footstepTimer;
    private float _currentFootstepInterval;
    private float _speedFeedback01;
    private bool _isPlayingFootsteps;
    private bool _footstepsPausedForAction;
    private int _footstepIndex;
    private float _lastCoinPickupTime = -10f;
    private int _coinChainIndex;
    private int _lastImpactFrame = -1;
    private bool _lastImpactWasFatal;

    public bool AreFootstepsPausedForAction => _footstepsPausedForAction;
    public bool IsSlideLoopPlaying => _slideLoopSource != null
                                      && _slideLoopSource.isPlaying;
    public bool IsSpeedWindPlaying => _speedWindLoopSource != null
                                      && _speedWindLoopSource.isPlaying;
    public float CurrentSpeedFeedback01 => _speedFeedback01;
    public float CurrentFootstepInterval => _currentFootstepInterval;

    // Procedural audio cache
    private Dictionary<string, AudioClip> _procClips = new Dictionary<string, AudioClip>();
    private const int SampleRate = 22050;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _hasFocus = Application.isFocused;
        EchoRunSaveSystem.EnsureInitialized();

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.loop = false;
        _sfxSource.spatialBlend = 0f;

        // Filters affect every source on their GameObject: keep music isolated.
        var musicObject = new GameObject("Music Bus");
        musicObject.transform.SetParent(transform, false);
        _musicSource = musicObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.spatialBlend = 0f;
        _musicFilter = musicObject.AddComponent<AudioLowPassFilter>();
        _musicFilter.cutoffFrequency = 2400f;
        _musicFilter.lowpassResonanceQ = 1f;

        _slideLoopSource = gameObject.AddComponent<AudioSource>();
        _slideLoopSource.playOnAwake = false;
        _slideLoopSource.loop = true;
        _slideLoopSource.spatialBlend = 0f;

        _impactSource = gameObject.AddComponent<AudioSource>();
        _impactSource.playOnAwake = false;
        _impactSource.loop = false;
        _impactSource.spatialBlend = 0f;

        _dodgeResultSource = gameObject.AddComponent<AudioSource>();
        _dodgeResultSource.playOnAwake = false;
        _dodgeResultSource.loop = false;
        _dodgeResultSource.spatialBlend = 0f;

        _speedWindLoopSource = gameObject.AddComponent<AudioSource>();
        _speedWindLoopSource.playOnAwake = false;
        _speedWindLoopSource.loop = true;
        _speedWindLoopSource.spatialBlend = 0f;
        _currentFootstepInterval = ResolveTargetFootstepInterval(
            footstepInterval, minimumFootstepInterval, 0f);
        ApplyOutputVolumes();
    }

    void Start()
    {
        LoadBundledAudio();
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        sfxVolume = PlayerPrefs.GetFloat("SfxVolume", 1f);
        muted = PlayerPrefs.GetInt("AudioMuted", 0) != 0;
        ApplyOutputVolumes();

        if (speedWindLoopClip == null)
            GetProceduralClip("speedWind");
        WarmActionFallbacks();

        AudioClip music = bgmClip != null ? bgmClip : GetProceduralClip("bgm");
        if (music == null) return;
        _musicSource.clip = music;
        if (_hasFocus && !_applicationPaused) _musicSource.Play();
        else _musicSuspended = true;
    }

    void Update()
    {
        UpdateMusic(Time.unscaledDeltaTime);
        AdvanceSpeedFeedback(Time.deltaTime);
        if (!ShouldEmitFootsteps(_isPlayingFootsteps,
                _footstepsPausedForAction)) return;

        _footstepTimer += Time.deltaTime;
        if (_footstepTimer >= _currentFootstepInterval)
        {
            _footstepTimer = 0f;
            int footstepSequence = _footstepIndex++;
            AudioClip step = footstepClips != null && footstepClips.Length > 0
                ? footstepClips[footstepSequence % footstepClips.Length]
                : footstepClip;
            PlaySFX(step, 0.28f, "footstep");
            FootstepEmitted?.Invoke(footstepSequence);
        }
    }

    public void StartFootsteps()
    {
        StopSlideLoopSource();
        ResetSpeedFeedbackState();
        _footstepsPausedForAction = false;
        _isPlayingFootsteps = true;
        _footstepTimer = 0f;
    }

    public void StopFootsteps()
    {
        StopSlideLoopSource();
        _isPlayingFootsteps = false;
        _footstepsPausedForAction = false;
        _footstepTimer = 0f;
        ResetSpeedFeedbackState();
    }

    public void PauseFootstepsForAction()
    {
        if (_footstepsPausedForAction) return;
        _footstepsPausedForAction = true;
        _footstepTimer = 0f;
    }

    public void ResumeFootstepsAfterAction()
    {
        if (!_footstepsPausedForAction) return;
        _footstepsPausedForAction = false;
        _footstepTimer = 0f;
    }

    public void BeginSlideLoop()
    {
        PauseFootstepsForAction();
        if (_slideLoopSource == null) return;

        AudioClip loop = slideLoopClip != null
            ? slideLoopClip
            : GetProceduralClip("slideLoop");
        if (loop == null) return;
        if (!ShouldStartSlideLoop(_slideLoopSource.isPlaying,
                _slideLoopSource.clip == loop)) return;

        _slideLoopSource.Stop();
        _slideLoopSource.clip = loop;
        _slideLoopSource.Play();
    }

    public void EndSlideLoop()
    {
        StopSlideLoopSource();
        ResumeFootstepsAfterAction();
    }

    private void StopSlideLoopSource()
    {
        if (_slideLoopSource != null && _slideLoopSource.isPlaying)
            _slideLoopSource.Stop();
    }

    public void SetSpeedFeedback(float speed01)
    {
        _speedFeedback01 = ResolveStoredSpeedFeedback(
            _isPlayingFootsteps, speed01);
        ApplySpeedWindOutputVolume();
        RefreshSpeedWindLoop();
    }

    private void AdvanceSpeedFeedback(float deltaTime)
    {
        float target = ResolveTargetFootstepInterval(footstepInterval,
            minimumFootstepInterval, _speedFeedback01);
        _currentFootstepInterval = SmoothFootstepInterval(
            _currentFootstepInterval, target, speedFeedbackResponse,
            deltaTime);

        if (_speedWindLoopSource != null)
            _speedWindLoopSource.pitch = ResolveSpeedWindPitch(
                _speedFeedback01);
        ApplySpeedWindOutputVolume();
    }

    private void ResetSpeedFeedbackState()
    {
        _speedFeedback01 = 0f;
        _currentFootstepInterval = ResolveTargetFootstepInterval(
            footstepInterval, minimumFootstepInterval, 0f);
        StopSpeedWindLoop();
    }

    private void RefreshSpeedWindLoop()
    {
        if (_speedWindLoopSource == null) return;
        if (!ShouldPlaySpeedWind(_isPlayingFootsteps,
                _footstepsPausedForAction, _speedFeedback01))
        {
            StopSpeedWindLoop();
            return;
        }

        AudioClip loop = speedWindLoopClip != null
            ? speedWindLoopClip
            : GetProceduralClip("speedWind");
        if (loop == null) return;
        _speedWindLoopSource.pitch = ResolveSpeedWindPitch(
            _speedFeedback01);
        if (_speedWindLoopSource.clip != loop)
        {
            _speedWindLoopSource.Stop();
            _speedWindLoopSource.clip = loop;
        }
        if (!_speedWindLoopSource.isPlaying)
            _speedWindLoopSource.Play();
    }

    private void StopSpeedWindLoop()
    {
        if (_speedWindLoopSource == null) return;
        if (_speedWindLoopSource.isPlaying)
            _speedWindLoopSource.Stop();
        _speedWindLoopSource.volume = 0f;
    }

    public void PlayJump()  => PlaySFX(jumpClip, 1f, "jump");
    public void PlaySlide() => PlaySFX(slideClip, 0.6f, "slide");
    public void PlayLand(float intensity01 = 1f)
    {
        PlaySFX(landClip, ResolveLandVolumeScale(intensity01), "land");
    }

    public void PlaySlideExit()
    {
        PlaySFX(slideExitClip, 0.48f, "slideExit");
    }
    public void PlayCoin()
    {
        _coinChainIndex = Time.unscaledTime - _lastCoinPickupTime <= 0.34f
            ? Mathf.Min(4, _coinChainIndex + 1)
            : 0;
        _lastCoinPickupTime = Time.unscaledTime;
        PlaySFX(coinClip, 0.64f, "memoryPulse" + _coinChainIndex);
    }
    public void PlayDodgeObstacle()
    {
        PlayDodgeResultSFX(dodgeObstacleClip, 0.6f, "dodge");
    }
    public void PlayDeath() => PlayImpactResult(true);
    public void PlayCollision() => PlayImpactResult(false);
    public void PlayCounterSuccess()
    {
        DuckMusic();
        // Contract rewrite is the semantic result of the pass. Replace only
        // the generic dodge channel, never jump/land/coin/UI one-shots.
        if (_dodgeResultSource != null) _dodgeResultSource.Stop();
        PlayDodgeResultSFX(
            counterSuccessClip, 0.68f, "counterSuccess");
    }
    public void PlayImpactResult(bool fatal)
    {
        bool playedThisFrame = _lastImpactFrame == Time.frameCount;
        if (!ShouldPlayImpact(playedThisFrame, _lastImpactWasFatal, fatal))
            return;

        bool replace = ShouldReplaceImpact(playedThisFrame,
            _lastImpactWasFatal, fatal);
        if (replace && _impactSource != null)
            _impactSource.Stop();

        _lastImpactFrame = Time.frameCount;
        _lastImpactWasFatal = fatal;
        DuckMusic(fatal ? 0.8f : 0.45f);
        AudioClip clip = fatal
            ? (impactFatalClip != null ? impactFatalClip : deathClip)
            : (impactRecoverClip != null ? impactRecoverClip : collisionClip);
        PlayImpactSFX(clip, fatal ? 0.9f : 0.72f,
            fatal ? "death" : "impactRecover");
    }
    public void PlayUIClick() => PlaySFX(uiClickClip, 0.55f, "dodge");
    public void PlayUIConfirm() => PlaySFX(uiConfirmClip, 0.65f, "coin");
    public void PlayUIError() => PlaySFX(uiErrorClip, 0.65f, "death");

    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        ApplyOutputVolumes();
        SaveAudioSettings(false);
    }

    public void SetSfxVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        ApplyOutputVolumes();
        SaveAudioSettings(false);
    }

    public void SetMasterVolume(float v)
    {
        masterVolume = Mathf.Clamp01(v);
        ApplyOutputVolumes();
        SaveAudioSettings(false);
    }

    public void SetMuted(bool value)
    {
        muted = value;
        ApplyOutputVolumes();
        SaveAudioSettings(false);
    }

    public static float ResolveOutputVolume(float master, float channel,
        bool isMuted)
    {
        return isMuted ? 0f : Mathf.Clamp01(master) * Mathf.Clamp01(channel);
    }

    public static bool ShouldEmitFootsteps(bool requested, bool actionPaused)
    {
        return requested && !actionPaused;
    }

    public static float ResolveLandVolumeScale(float intensity01)
    {
        return Mathf.Lerp(0.42f, 0.78f, Mathf.Clamp01(intensity01));
    }

    public static bool ShouldStartSlideLoop(bool isPlaying,
        bool hasRequestedClip)
    {
        return !isPlaying || !hasRequestedClip;
    }

    public static float ResolveStoredSpeedFeedback(bool runActive,
        float speed01)
    {
        return runActive ? Mathf.Clamp01(speed01) : 0f;
    }

    public static float ResolveTargetFootstepInterval(float baseInterval,
        float minimumInterval, float speed01)
    {
        float safeBase = Mathf.Max(0.12f, baseInterval);
        float safeMinimum = Mathf.Clamp(minimumInterval, 0.12f, safeBase);
        float speedCurve = Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01(speed01));
        return Mathf.Lerp(safeBase, safeMinimum, speedCurve);
    }

    public static float SmoothFootstepInterval(float current,
        float target, float response, float deltaTime)
    {
        float safeDelta = Mathf.Max(0f, deltaTime);
        float blend = 1f - Mathf.Exp(-Mathf.Max(0f, response) * safeDelta);
        return Mathf.Lerp(current, target, blend);
    }

    public static bool ShouldPlaySpeedWind(bool runActive,
        bool actionPaused, float speed01)
    {
        // Action pause gates footsteps only. Wind describes world speed.
        _ = actionPaused;
        return runActive && Mathf.Clamp01(speed01) > 0.001f;
    }

    public static float ResolveSpeedWindVolumeScale(float speed01,
        float maximumVolume)
    {
        float speedCurve = Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01(speed01));
        return Mathf.Clamp(maximumVolume, 0f, 0.2f) * speedCurve;
    }

    public static float ResolveSpeedWindPitch(float speed01)
    {
        return Mathf.Lerp(0.86f, 1.16f, Mathf.Clamp01(speed01));
    }

    public static bool ShouldPlayImpact(bool playedThisFrame,
        bool previousWasFatal, bool nextIsFatal)
    {
        return !playedThisFrame || (!previousWasFatal && nextIsFatal);
    }

    public static bool ShouldReplaceImpact(bool playedThisFrame,
        bool previousWasFatal, bool nextIsFatal)
    {
        return playedThisFrame && !previousWasFatal && nextIsFatal;
    }

    private void ApplyOutputVolumes()
    {
        float master = EffectiveMasterVolume;
        if (_musicSource != null)
            _musicSource.volume = master * Mathf.Clamp01(musicVolume)
                * MusicHeadroom * _musicEnvelope;
        if (_sfxSource != null)
            _sfxSource.volume = master * Mathf.Clamp01(sfxVolume) * EffectsHeadroom;
        if (_impactSource != null)
            _impactSource.volume = master * Mathf.Clamp01(sfxVolume) * EffectsHeadroom;
        if (_dodgeResultSource != null)
            _dodgeResultSource.volume = master * Mathf.Clamp01(sfxVolume) * EffectsHeadroom;
        if (_slideLoopSource != null)
            _slideLoopSource.volume = ResolveOutputVolume(
                master, sfxVolume * slideLoopVolume * EffectsHeadroom, muted);
        ApplySpeedWindOutputVolume();
    }

    private void ApplySpeedWindOutputVolume()
    {
        if (_speedWindLoopSource == null) return;
        float windVolume = ResolveSpeedWindVolumeScale(_speedFeedback01,
            speedWindLoopVolume);
        _speedWindLoopSource.volume = ResolveOutputVolume(
            EffectiveMasterVolume, sfxVolume * windVolume * EffectsHeadroom, muted);
    }

    private void UpdateMusic(float deltaTime)
    {
        if (!_hasFocus || _applicationPaused) return;
        GameManager game = GameManager.Instance;
        GameState state = game != null ? game.State : GameState.Menu;
        bool challenge = AIShadowRunner.Instance != null
            && AIShadowRunner.Instance.HasActiveOpponent;
        bool finished = game != null && game.LastEndReason == RunEndReason.FinishReached;
        float level = state == GameState.Playing ? (challenge ? 1f : 0.76f)
            : state == GameState.Paused ? 0.3f
            : state == GameState.GameOver ? (finished ? 0.62f : 0.38f) : 0.48f;
        float cutoff = state == GameState.Playing ? (challenge ? 18000f : 8500f)
            : state == GameState.Paused ? 1800f
            : state == GameState.GameOver ? (finished ? 6500f : 2000f) : 3200f;
        _duckRemaining = Mathf.Max(0f, _duckRemaining - deltaTime);
        if (_duckRemaining > 0f) level *= 0.45f;
        // Unscaled time lets settings and pause transitions finish while gameplay is frozen.
        float response = level < _musicEnvelope ? 10f : 2.5f;
        _musicEnvelope = Mathf.Lerp(_musicEnvelope, level,
            1f - Mathf.Exp(-response * Mathf.Max(0f, deltaTime)));
        if (_musicFilter != null)
            _musicFilter.cutoffFrequency = Mathf.Lerp(_musicFilter.cutoffFrequency,
                cutoff, 1f - Mathf.Exp(-3f * Mathf.Max(0f, deltaTime)));
        ApplyOutputVolumes();
    }

    private void DuckMusic(float duration = 0.4f)
    {
        _duckRemaining = Mathf.Max(_duckRemaining, duration);
    }

    void OnApplicationFocus(bool hasFocus)
    {
        _hasFocus = hasFocus;
        RefreshApplicationAudio();
    }

    void OnApplicationPause(bool paused)
    {
        _applicationPaused = paused;
        RefreshApplicationAudio();
    }

    private void RefreshApplicationAudio()
    {
        bool suspended = !_hasFocus || _applicationPaused;
        if (suspended)
        {
            if (_musicSource != null && _musicSource.isPlaying) _musicSource.Pause();
            _musicSuspended = true;
            _musicEnvelope = 0f;
            _duckRemaining = 0f;
            // Do not replay old action tails when returning to the application.
            _sfxSource?.Stop();
            _impactSource?.Stop();
            _dodgeResultSource?.Stop();
        }
        else if (_musicSuspended && _musicSource != null && _musicSource.clip != null)
        {
            _musicSource.UnPause();
            if (!_musicSource.isPlaying) _musicSource.Play();
            _musicSuspended = false;
        }
        ApplyOutputVolumes();
    }

    private void SaveAudioSettings(bool flush)
    {
        EchoRunSaveSystem.SaveAudio(masterVolume, musicVolume, sfxVolume,
            muted, flush);
    }

    void PlaySFX(AudioClip clip, float volumeScale = 1f, string procKey = null)
    {
        if (clip == null && !string.IsNullOrEmpty(procKey))
            clip = GetProceduralClip(procKey);

        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip, volumeScale);
    }

    private void PlayImpactSFX(AudioClip clip, float volumeScale,
        string procKey)
    {
        if (clip == null && !string.IsNullOrEmpty(procKey))
            clip = GetProceduralClip(procKey);

        if (clip == null || _impactSource == null) return;
        _impactSource.PlayOneShot(clip, volumeScale);
    }

    private void PlayDodgeResultSFX(AudioClip clip, float volumeScale,
        string procKey)
    {
        if (clip == null && !string.IsNullOrEmpty(procKey))
            clip = GetProceduralClip(procKey);
        if (clip == null || _dodgeResultSource == null) return;
        _dodgeResultSource.PlayOneShot(clip, volumeScale);
    }

    private void WarmActionFallbacks()
    {
        if (jumpClip == null) GetProceduralClip("jump");
        if (slideClip == null) GetProceduralClip("slide");
        if (slideLoopClip == null) GetProceduralClip("slideLoop");
        if (slideExitClip == null) GetProceduralClip("slideExit");
        if (landClip == null) GetProceduralClip("land");
        if (impactRecoverClip == null) GetProceduralClip("impactRecover");
        if (counterSuccessClip == null) GetProceduralClip("counterSuccess");
    }

    private void LoadBundledAudio()
    {
        if (bgmClip == null) bgmClip = Resources.Load<AudioClip>("Audio/bgm_transit");
        // The legacy bundled coin clip is intentionally not auto-loaded. The
        // in-world object is memory data, so the default is a restrained
        // glass/digital pulse rather than a traditional coin chime.
        if (collisionClip == null) collisionClip = Resources.Load<AudioClip>("Audio/collision");
        if (slideLoopClip == null) slideLoopClip = Resources.Load<AudioClip>("Audio/slide_loop");
        if (slideExitClip == null) slideExitClip = Resources.Load<AudioClip>("Audio/slide_exit");
        if (landClip == null) landClip = Resources.Load<AudioClip>("Audio/land");
        if (impactRecoverClip == null) impactRecoverClip = Resources.Load<AudioClip>("Audio/impact_recover");
        if (impactFatalClip == null) impactFatalClip = Resources.Load<AudioClip>("Audio/impact_fatal");
        if (counterSuccessClip == null) counterSuccessClip = Resources.Load<AudioClip>("Audio/counter_success");
        if (uiClickClip == null) uiClickClip = Resources.Load<AudioClip>("Audio/ui_click");
        if (uiConfirmClip == null) uiConfirmClip = Resources.Load<AudioClip>("Audio/ui_confirm");
        if (uiErrorClip == null) uiErrorClip = Resources.Load<AudioClip>("Audio/ui_error");
        if (footstepClips == null || footstepClips.Length == 0)
        {
            AudioClip first = Resources.Load<AudioClip>("Audio/footstep_01");
            AudioClip second = Resources.Load<AudioClip>("Audio/footstep_02");
            if (first != null && second != null) footstepClips = new[] { first, second };
            else if (first != null) footstepClips = new[] { first };
        }
    }

    AudioClip GetProceduralClip(string key)
    {
        if (_procClips.TryGetValue(key, out AudioClip cached))
            return cached;

        if (key.StartsWith("memoryPulse"))
        {
            int chain = 0;
            int.TryParse(key.Substring("memoryPulse".Length), out chain);
            AudioClip memoryPulse = GenerateMemoryPulse(Mathf.Clamp(chain, 0, 4));
            _procClips[key] = memoryPulse;
            return memoryPulse;
        }

        AudioClip proc = key switch
        {
            "bgm"      => GenerateTransitLoop(),
            "footstep" => GenerateFootstep(),
            "jump"     => GenerateToneSweep(260f, 620f, 0.18f),
            "slide"    => GenerateNoiseBurst(0.18f, 0.18f),
            "slideLoop" => GenerateSlideLoop(),
            "slideExit" => GenerateNoiseBurst(0.11f, 0.12f),
            "land"      => GenerateNoiseBurst(0.13f, 0.16f),
            "impactRecover" => GenerateToneSweep(210f, 128f, 0.18f),
            "counterSuccess" => GenerateToneSweep(520f, 980f, 0.22f),
            "speedWind" => GenerateSpeedWindLoop(),
            "coin"     => GenerateMemoryPulse(0),
            "dodge"    => GenerateToneSweep(520f, 240f, 0.14f),
            "death"    => GenerateToneSweep(360f, 52f, 0.48f),
            _           => GenerateNoiseBurst(0.06f, 0.12f),
        };
        _procClips[key] = proc;
        return proc;
    }

    AudioClip GenerateMemoryPulse(int chain)
    {
        const float duration = 0.16f;
        int samples = Mathf.CeilToInt(SampleRate * duration);
        float[] data = new float[samples];
        float pulseFrequency = 205f + chain * 18f;
        float glassFrequency = 940f + chain * 72f;
        for (int index = 0; index < samples; index++)
        {
            float t = (float)index / SampleRate;
            float normalized = Mathf.Clamp01(t / duration);
            float attack = Mathf.Clamp01(normalized / 0.025f);
            float pulseEnvelope = attack * Mathf.Pow(1f - normalized, 2.2f);
            float glassEnvelope = attack * Mathf.Exp(-20f * t);
            float pulse = Mathf.Sin(2f * Mathf.PI
                * (pulseFrequency - normalized * 36f) * t) * 0.22f;
            float glass = Mathf.Sin(2f * Mathf.PI * glassFrequency * t)
                * 0.12f;
            float digital = Mathf.Sin(2f * Mathf.PI
                * (glassFrequency * 1.51f) * t) * 0.035f;
            data[index] = pulse * pulseEnvelope
                + (glass + digital) * glassEnvelope;
        }
        AudioClip clip = AudioClip.Create("memory_pulse_" + chain,
            samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip GenerateSpeedWindLoop()
    {
        const float duration = 1f;
        int samples = Mathf.CeilToInt(SampleRate * duration);
        float[] data = new float[samples];
        for (int index = 0; index < samples; index++)
        {
            float normalized = index / (float)samples;
            float phase = 2f * Mathf.PI * normalized;
            float air = Mathf.Sin(phase * 37f) * 0.42f
                        + Mathf.Sin(phase * 71f) * 0.27f
                        + Mathf.Sin(phase * 113f) * 0.17f
                        + Mathf.Sin(phase * 197f) * 0.08f;
            float breath = 0.78f + Mathf.Sin(phase * 3f) * 0.16f;
            data[index] = air * breath * 0.08f;
        }
        AudioClip clip = AudioClip.Create("proc_speed_wind_loop", samples,
            1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip GenerateSlideLoop()
    {
        const float duration = 0.6f;
        int samples = Mathf.CeilToInt(SampleRate * duration);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float normalized = i / (float)samples;
            float phase = 2f * Mathf.PI * normalized;
            float texture = Mathf.Sin(phase * 83f) * 0.42f
                            + Mathf.Sin(phase * 151f) * 0.24f
                            + Mathf.Sin(phase * 233f) * 0.13f;
            float scrapePulse = 0.72f + Mathf.Sin(phase * 5f) * 0.18f;
            data[i] = texture * scrapePulse * 0.12f;
        }
        AudioClip clip = AudioClip.Create("proc_slide_loop", samples, 1,
            SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip GenerateToneSweep(float freqStart, float freqEnd, float duration)
    {
        int samples = Mathf.CeilToInt(SampleRate * duration);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float freq = Mathf.Lerp(freqStart, freqEnd, t / duration);
            float normalized = Mathf.Clamp01(t / duration);
            float attack = Mathf.Clamp01(normalized / 0.08f);
            float envelope = attack * Mathf.Pow(1f - normalized, 1.6f);
            float phase = 2f * Mathf.PI * freq * t;
            data[i] = (Mathf.Sin(phase) + Mathf.Sin(phase * 2f) * 0.18f)
                      * envelope * 0.32f;
        }
        AudioClip clip = AudioClip.Create("proc_tone_" + duration, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip GenerateNoiseBurst(float duration, float level)
    {
        int samples = Mathf.CeilToInt(SampleRate * duration);
        float[] data = new float[samples];
        var random = new System.Random(7319 + samples);
        float filtered = 0f;
        for (int i = 0; i < samples; i++)
        {
            float normalized = i / (float)Mathf.Max(1, samples - 1);
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            filtered = Mathf.Lerp(filtered, white, 0.22f);
            float envelope = Mathf.Sin(Mathf.PI * normalized)
                             * Mathf.Pow(1f - normalized, 0.7f);
            data[i] = filtered * envelope * level;
        }
        AudioClip clip = AudioClip.Create("proc_noise_" + duration, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip GenerateFootstep()
    {
        const float duration = 0.11f;
        int samples = Mathf.CeilToInt(SampleRate * duration);
        float[] data = new float[samples];
        var random = new System.Random(1907);
        float filtered = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;
            float normalized = t / duration;
            float envelope = Mathf.Pow(1f - Mathf.Clamp01(normalized), 3f);
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            filtered = Mathf.Lerp(filtered, white, 0.14f);
            float body = Mathf.Sin(2f * Mathf.PI * 82f * t) * 0.62f;
            data[i] = (body + filtered * 0.38f) * envelope * 0.34f;
        }
        AudioClip clip = AudioClip.Create("proc_footstep", samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    AudioClip GenerateTransitLoop()
    {
        const float duration = 16f;
        int samples = Mathf.CeilToInt(SampleRate * duration);
        float[] data = new float[samples];
        int[] arpeggio = { 0, 7, 12, 7, 3, 10, 12, 15 };

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;
            float beatPhase = Mathf.Repeat(t * 2f, 1f);
            float beatEnvelope = Mathf.Exp(-beatPhase * 11f);
            float pulse = Mathf.Sin(2f * Mathf.PI * 58f * t) * beatEnvelope;

            float pad = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.46f
                        + Mathf.Sin(2f * Mathf.PI * 82.5f * t) * 0.22f
                        + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.12f;

            float stepPosition = Mathf.Repeat(t * 2f, 1f);
            int step = Mathf.FloorToInt(t * 2f) % arpeggio.Length;
            float noteFrequency = 220f * Mathf.Pow(2f, arpeggio[step] / 12f);
            float noteEnvelope = Mathf.Sin(Mathf.PI * stepPosition);
            noteEnvelope *= noteEnvelope * Mathf.Pow(1f - stepPosition, 0.35f);
            float signal = Mathf.Sin(2f * Mathf.PI * noteFrequency * t)
                           + Mathf.Sin(2f * Mathf.PI * noteFrequency * 2f * t) * 0.12f;

            float longBreath = 0.72f + Mathf.Sin(2f * Mathf.PI * t / 8f) * 0.18f;
            data[i] = Mathf.Clamp((pad * 0.105f * longBreath)
                                  + (pulse * 0.055f)
                                  + (signal * noteEnvelope * 0.045f), -0.28f, 0.28f);
        }

        AudioClip clip = AudioClip.Create("proc_transit_loop", samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        if (_slideLoopSource != null)
        {
            _slideLoopSource.Stop();
            _slideLoopSource.clip = null;
        }
        if (_impactSource != null)
            _impactSource.Stop();
        if (_dodgeResultSource != null)
            _dodgeResultSource.Stop();
        if (_speedWindLoopSource != null)
        {
            _speedWindLoopSource.Stop();
            _speedWindLoopSource.clip = null;
        }
        foreach (AudioClip clip in _procClips.Values)
            if (clip != null) Destroy(clip);
        _procClips.Clear();
        Instance = null;
    }
}

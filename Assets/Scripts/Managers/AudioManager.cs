using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays looping background music per scene and one-shot SFX.
/// Lives on the persistent [Managers] object alongside <see cref="GameManager"/>.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music")]
    [SerializeField] AudioClip forestMusic;
    [SerializeField] AudioClip villageMusic;
    [SerializeField] AudioClip battleMusic;
    [Range(0f, 1f)]
    [SerializeField] float musicVolume = 0.55f;
    [SerializeField] float musicFadeDuration = 1.25f;

    [Header("Scene Groups")]
    [SerializeField] string[] forestScenes = { "Forest_Road", "Forest_Boss", "Forest" };
    [SerializeField] string[] villageScenes = { "Village", "Aether_Outpost" };
    [SerializeField] string[] ignoredScenes = { "Bootstrap", "Testing" };

    [Header("SFX")]
    [SerializeField] AudioClip[] hitSounds;
    [Range(0f, 1f)]
    [SerializeField] float sfxVolume = 1f;

    AudioSource musicSource;
    AudioSource sfxSource;
    Coroutine musicFadeRoutine;
    AudioClip currentMusic;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        EnsureAudioSources();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void EnsureAudioSources()
    {
        musicSource = GetComponent<AudioSource>();
        if (musicSource == null)
            musicSource = gameObject.AddComponent<AudioSource>();

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = musicVolume;

        var sources = GetComponents<AudioSource>();
        sfxSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.volume = sfxVolume;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsIgnoredScene(scene.name))
            return;

        AudioClip clip = ResolveMusicForScene(scene.name);
        if (clip != null)
            PlayMusic(clip);
    }

    bool IsIgnoredScene(string sceneName)
    {
        foreach (string ignored in ignoredScenes)
        {
            if (ignored == sceneName)
                return true;
        }

        return false;
    }

    AudioClip ResolveMusicForScene(string sceneName)
    {
        if (sceneName == SceneNames.Battle)
            return battleMusic;

        if (ContainsScene(forestScenes, sceneName))
            return forestMusic;

        if (ContainsScene(villageScenes, sceneName))
            return villageMusic;

        return null;
    }

    static bool ContainsScene(string[] scenes, string sceneName)
    {
        if (scenes == null)
            return false;

        foreach (string scene in scenes)
        {
            if (scene == sceneName)
                return true;
        }

        return false;
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || musicSource == null)
            return;

        if (currentMusic == clip && musicSource.isPlaying)
            return;

        currentMusic = clip;

        if (musicFadeRoutine != null)
            StopCoroutine(musicFadeRoutine);

        musicFadeRoutine = StartCoroutine(FadeToMusic(clip));
    }

    IEnumerator FadeToMusic(AudioClip clip)
    {
        float duration = Mathf.Max(0f, musicFadeDuration);

        if (musicSource.isPlaying && duration > 0f)
        {
            float startVolume = musicSource.volume;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                musicSource.volume = Mathf.Lerp(startVolume, 0f, t / duration);
                yield return null;
            }
        }

        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.volume = 0f;
        musicSource.Play();

        if (duration <= 0f)
        {
            musicSource.volume = musicVolume;
            musicFadeRoutine = null;
            yield break;
        }

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(0f, musicVolume, t / duration);
            yield return null;
        }

        musicSource.volume = musicVolume;
        musicFadeRoutine = null;
    }

    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(clip, sfxVolume * Mathf.Clamp01(volumeScale));
    }

    public void PlayHitSound()
    {
        if (hitSounds == null || hitSounds.Length == 0)
            return;

        AudioClip clip = hitSounds[Random.Range(0, hitSounds.Length)];
        PlaySfx(clip);
    }
}

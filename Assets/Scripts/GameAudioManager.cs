using UnityEngine;
using UnityEngine.SceneManagement;

public class GameAudioManager : MonoBehaviour
{
    public static GameAudioManager Instance { get; private set; }

    [Header("Background Music")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip homeMusic;

    [Header("Scene")]
    [SerializeField] private string homeSceneName = "HomeScreen";

    private const string SoundKey = "SoundEnabled";

    public bool SoundEnabled { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SoundEnabled = PlayerPrefs.GetInt(SoundKey, 1) == 1;

        ApplySoundSetting();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        HandleSceneMusic(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HandleSceneMusic(scene);
    }

    private void HandleSceneMusic(Scene scene)
    {
        // HOME SCENE
        if (scene.name == homeSceneName)
        {
            if (SoundEnabled)
                PlayHomeMusic();
            else
                StopMusic();

            return;
        }

        // GAMEPLAY / OTHER SCENES
        // Home background music must not continue here.
        StopMusic();
    }

    private void PlayHomeMusic()
    {
        if (musicSource == null || homeMusic == null)
            return;

        if (musicSource.clip != homeMusic)
            musicSource.clip = homeMusic;

        musicSource.loop = true;

        if (!musicSource.isPlaying)
            musicSource.Play();
    }

    private void StopMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
            musicSource.Stop();
    }

    public void ToggleSound()
    {
        SetSound(!SoundEnabled);
    }

    public void SetSound(bool enabled)
    {
        SoundEnabled = enabled;

        PlayerPrefs.SetInt(SoundKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

        ApplySoundSetting();

        // Update music immediately depending on current scene.
        HandleSceneMusic(SceneManager.GetActiveScene());
    }

    private void ApplySoundSetting()
    {
        // This controls ALL Unity sounds:
        // Music + button sounds + gameplay sound effects.
        AudioListener.volume = SoundEnabled ? 1f : 0f;
    }

    public bool IsSoundOn()
    {
        return SoundEnabled;
    }
}
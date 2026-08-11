using UnityEngine;
using UnityEngine.Serialization;

public class SoundManager : MonoBehaviour
{
    private GameManager gameManager;

    [Header("Legacy Sources")]
    public AudioSource bgMusic;
    public AudioSource Click;
    public AudioSource SoundCoin1;
    public AudioSource SoundCoin2;

    [FormerlySerializedAs("BigwinCoin1")]
    public AudioSource bigWinFallback;

    [Header("Random Sound Arrays")]
    [Tooltip("Randomly selected when a death profile enables Big Win Sound. Leave empty to use Big Win Fallback.")]
    public AudioSource[] bigWinSounds;

    [Tooltip("Randomly selected when a boss-arrival event starts.")]
    public AudioSource[] bossArrivalSounds;

    [Tooltip("Randomly selected when a boss batch or the final target-boss group is cleared.")]
    public AudioSource[] bossDefeatSounds;

    [Header("Weapon Impact Sounds")]
    [Tooltip("Used when a Rocket or other large net impact is created. Leave empty to play no impact sound.")]
    public AudioSource netBoomFallback;

    [Tooltip("Optional Net Boom variations. A valid source is selected randomly; the fallback is used when this array is empty.")]
    public AudioSource[] netBoomSounds;

    private int lastBigWinIndex = -1;
    private int lastBossArrivalIndex = -1;
    private int lastBossDefeatIndex = -1;
    private int lastNetBoomIndex = -1;

    private void Start()
    {
        gameManager = GameManager.Instance;
    }

    public void PlayClickSound()
    {
        PlaySafe(Click);
    }

    public void PlaySoundCoin1()
    {
        PlaySafe(SoundCoin1);
    }

    public void PlaySoundCoin2()
    {
        PlaySafe(SoundCoin2);
    }

    /// <summary>
    /// Plays a specific big-win source when index is valid; otherwise chooses
    /// a random valid source without immediately repeating when possible.
    /// </summary>
    public void PlayBigWinSound(int requestedIndex = -1)
    {
        AudioSource selected = GetRequestedOrRandom(
            bigWinSounds,
            requestedIndex,
            ref lastBigWinIndex
        );

        if (selected == null)
        {
            selected = bigWinFallback;
        }

        PlaySafe(selected);
    }

    public void PlayBossArrivalSound(int requestedIndex = -1)
    {
        AudioSource selected = GetRequestedOrRandom(
            bossArrivalSounds,
            requestedIndex,
            ref lastBossArrivalIndex
        );

        PlaySafe(selected);
    }

    public void PlayBossDefeatSound(int requestedIndex = -1)
    {
        AudioSource selected = GetRequestedOrRandom(
            bossDefeatSounds,
            requestedIndex,
            ref lastBossDefeatIndex
        );

        PlaySafe(selected);
    }

    /// <summary>
    /// Plays the impact sound used by Big Rocket and large Net Boom effects.
    /// The method remains safe when no AudioSource has been assigned, allowing
    /// older scenes to compile and run before the new Inspector fields are set.
    /// </summary>
    public void PlayNetBoomSound(int requestedIndex = -1)
    {
        AudioSource selected = GetRequestedOrRandom(
            netBoomSounds,
            requestedIndex,
            ref lastNetBoomIndex
        );

        if (selected == null)
        {
            selected = netBoomFallback;
        }

        PlaySafe(selected);
    }

    // Existing code can keep calling this old method name.
    public void PlaySoundBigwinCoin1()
    {
        PlayBigWinSound();
    }

    private static void PlaySafe(AudioSource source)
    {
        if (source == null || source.clip == null)
        {
            return;
        }

        source.Stop();
        source.time = 0f;
        source.Play();
    }

    private static AudioSource GetRequestedOrRandom(
        AudioSource[] sources,
        int requestedIndex,
        ref int lastIndex
    )
    {
        if (sources == null || sources.Length == 0)
        {
            return null;
        }

        if (requestedIndex >= 0 &&
            requestedIndex < sources.Length &&
            sources[requestedIndex] != null &&
            sources[requestedIndex].clip != null)
        {
            lastIndex = requestedIndex;
            return sources[requestedIndex];
        }

        int validCount = 0;

        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] != null && sources[i].clip != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return null;
        }

        int randomValid = Random.Range(0, validCount);
        int selectedIndex = -1;

        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null || sources[i].clip == null)
            {
                continue;
            }

            if (randomValid == 0)
            {
                selectedIndex = i;
                break;
            }

            randomValid--;
        }

        if (validCount > 1 && selectedIndex == lastIndex)
        {
            for (int i = 1; i <= sources.Length; i++)
            {
                int candidateIndex = (selectedIndex + i) % sources.Length;

                if (sources[candidateIndex] != null &&
                    sources[candidateIndex].clip != null)
                {
                    selectedIndex = candidateIndex;
                    break;
                }
            }
        }

        lastIndex = selectedIndex;
        return selectedIndex >= 0 ? sources[selectedIndex] : null;
    }
    [System.Obsolete("Use bigWinFallback instead.")]
    public AudioSource BigwinCoin1
    {
        get { return bigWinFallback; }
        set { bigWinFallback = value; }
    }

}

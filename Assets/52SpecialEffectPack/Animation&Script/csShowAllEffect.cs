using UnityEngine;
using UnityEngine.UI;

public class csShowAllEffect : MonoBehaviour
{
    [Header("Effects")]
    public string[] EffectName;
    public Transform[] Effect;

    [Header("UI")]
    public Text Text1;

    [Header("Current Effect")]
    public int i = 0;

    private void Start()
    {
        if (!HasEffects())
            return;

        i = Mathf.Clamp(i, 0, Effect.Length - 1);
        SpawnCurrentEffect();
        UpdateLabel();
    }

    private void Update()
    {
        if (!HasEffects())
            return;

        UpdateLabel();

        // Previous effect
        if (Input.GetKeyDown(KeyCode.Z))
        {
            i--;

            if (i < 0)
                i = Effect.Length - 1;

            SpawnCurrentEffect();
        }

        // Next effect
        if (Input.GetKeyDown(KeyCode.X))
        {
            i++;

            if (i >= Effect.Length)
                i = 0;

            SpawnCurrentEffect();
        }

        // Replay current effect
        if (Input.GetKeyDown(KeyCode.C))
        {
            SpawnCurrentEffect();
        }
    }

    private void SpawnCurrentEffect()
    {
        if (!HasEffects())
            return;

        Transform effectPrefab = Effect[i];

        if (effectPrefab == null)
        {
            Debug.LogWarning(
                $"csShowAllEffect: Effect element {i} is empty.",
                this
            );
            return;
        }

        Instantiate(
            effectPrefab,
            Vector3.zero,
            Quaternion.identity
        );
    }

    private void UpdateLabel()
    {
        if (Text1 == null)
            return;

        string effectName = "Unnamed Effect";

        if (EffectName != null &&
            i >= 0 &&
            i < EffectName.Length &&
            !string.IsNullOrEmpty(EffectName[i]))
        {
            effectName = EffectName[i];
        }

        Text1.text = $"{i + 1}: {effectName}";
    }

    private bool HasEffects()
    {
        return Effect != null && Effect.Length > 0;
    }
}
using UnityEngine;

/// <summary>
/// Legacy asset compatibility for projects that imported an earlier setup
/// package. The current 46-fish runtime reads FishScript directly; this
/// profile remains only so older .asset files and scripts still compile.
/// </summary>
[CreateAssetMenu(
    menuName = "Fish Arcade/Legacy Fish Stats Profile",
    fileName = "FishStatsProfile"
)]
public sealed class FishStatsProfile : ScriptableObject
{
    public FishTier tier = FishTier.Small;
    public bool isEpicBoss;
    public string displayName;

    [Min(1f)] public float maximumHp = 1f;
    [Min(0f)] public float rewardPoints;
    [Min(0.05f)] public float moveSpeed = 1f;

    public FishDeathProfile deathProfile;
    public FishMovementProfile movementProfile;

    [Min(0f)] public float spawnWeight = 1f;
    [Range(1, 32)] public int maximumAlive = 8;
    [Min(0f)] public float respawnCooldown = 1.5f;

    public bool useLifetimeExit = true;
    public Vector2 screenLifetimeRange = new Vector2(22f, 34f);
    public FishScript.ParadeParticipationMode paradeParticipation =
        FishScript.ParadeParticipationMode.AutoByTier;
    public bool paradeEligible = true;
    [Min(0f)] public float paradeSelectionWeight = 1f;

    private void OnValidate()
    {
        maximumHp = Mathf.Max(1f, maximumHp);
        rewardPoints = Mathf.Max(0f, rewardPoints);
        moveSpeed = Mathf.Max(0.05f, moveSpeed);
        spawnWeight = Mathf.Max(0f, spawnWeight);
        maximumAlive = Mathf.Clamp(maximumAlive, 1, 32);
        respawnCooldown = Mathf.Max(0f, respawnCooldown);
        paradeSelectionWeight = Mathf.Max(0f, paradeSelectionWeight);

        float minimumLifetime = Mathf.Max(
            1f,
            Mathf.Min(screenLifetimeRange.x, screenLifetimeRange.y)
        );
        float maximumLifetime = Mathf.Max(
            minimumLifetime,
            Mathf.Max(screenLifetimeRange.x, screenLifetimeRange.y)
        );
        screenLifetimeRange = new Vector2(
            minimumLifetime,
            maximumLifetime
        );

        bool eventFish =
            tier == FishTier.Special ||
            tier == FishTier.MiniBoss ||
            tier == FishTier.MainBoss ||
            isEpicBoss;

        if (eventFish)
        {
            paradeEligible = false;

            if (paradeParticipation ==
                FishScript.ParadeParticipationMode.AutoByTier)
            {
                paradeSelectionWeight = 0f;
            }
        }

        if (tier == FishTier.MainBoss || isEpicBoss)
        {
            maximumAlive = 1;
            useLifetimeExit = false;
        }
    }
}

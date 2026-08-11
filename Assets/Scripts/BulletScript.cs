using UnityEngine;

public class BulletScript : MonoBehaviour
{
    public int BulletId;
    public int acitiveGun;

    [SerializeField] private float netTime = 0.3f;
    [SerializeField] private float damage = 1f;

    private readonly Color redcolor =
        new Color(1.0f, 0.58f, 0.58f, 9.88f);

    private GameManager GM;
    private FishScript lockedTarget;
    private int lockedTargetLifeVersion;
    private bool targetExclusive;

    public void ApplyArcadePreset(float damageAmount)
    {
        damage = Mathf.Max(1f, damageAmount);
        netTime = 0.30f;
    }

    public void ConfigureShot(
        int shooterId,
        int gunLevel,
        FishScript target,
        int targetLifeVersion,
        bool exclusiveTarget
    )
    {
        BulletId = shooterId;
        acitiveGun = gunLevel;
        lockedTarget = target;
        lockedTargetLifeVersion = targetLifeVersion;
        targetExclusive = exclusiveTarget && target != null;
    }

    private void Start()
    {
        GM = GameManager.Instance;
    }

    private void Update()
    {
        if (targetExclusive && !IsLockedTargetValid())
        {
            gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // A Rocket prefab may be copied from a normal bullet prefab. Even if
        // this legacy component is accidentally re-enabled in the Inspector,
        // it must never consume the target-exclusive Rocket.
        if (GetComponentInParent<BigRocketBullet>() != null)
        {
            return;
        }

        FishScript fish = other.GetComponentInParent<FishScript>();
        if (fish == null)
        {
            return;
        }

        // Auto Shot bullets pass through every non-target fish. Manual shots
        // retain the original first-fish collision behavior.
        if (targetExclusive)
        {
            if (fish != lockedTarget || !IsLockedTargetValid())
            {
                return;
            }
        }
        else if (!fish.IsAliveTarget)
        {
            return;
        }

        SpriteRenderer fishSpriteRenderer =
            fish.GetComponent<SpriteRenderer>();

        if (fishSpriteRenderer == null)
        {
            fishSpriteRenderer =
                fish.GetComponentInChildren<SpriteRenderer>();
        }

        if (fishSpriteRenderer != null)
        {
            fishSpriteRenderer.color = redcolor;
            fish.ResetFishColor(fishSpriteRenderer);
        }

        fish.TakeDamage(
            fishSpriteRenderer,
            damage,
            BulletId,
            acitiveGun
        );

        float fishCenterY = other.bounds.center.y;
        Vector3 netPosition = new Vector3(
            transform.position.x,
            fishCenterY,
            transform.position.z
        );

        if (GM == null)
        {
            GM = GameManager.Instance;
        }

        if (GM != null)
        {
            GM.SpawnNet(acitiveGun, netPosition, netTime);
        }

        gameObject.SetActive(false);
    }

    private bool IsLockedTargetValid()
    {
        Camera mainCamera = Camera.main;

        return lockedTarget != null &&
               lockedTarget.TargetLifeVersion == lockedTargetLifeVersion &&
               lockedTarget.IsAliveTarget &&
               lockedTarget.IsTargetVisibleTo(mainCamera);
    }

    private void OnBecameInvisible()
    {
        if (GetComponentInParent<BigRocketBullet>() != null)
        {
            return;
        }

        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        lockedTarget = null;
        lockedTargetLifeVersion = 0;
        targetExclusive = false;
    }
}

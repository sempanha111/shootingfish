using UnityEngine;

/// <summary>
/// Target-exclusive homing Rocket using enabled trigger colliders. Non-target
/// fish and obstacles are ignored by this component, so the Rocket continues
/// flying until its exact locked target is reached.
/// The version check prevents a pooled fish from being hit after reuse.
/// </summary>
public class BigRocketBullet : MonoBehaviour
{
    private WeaponsScripts owner;
    private FishScript target;
    private int targetLifeVersion;
    private float damage;
    private int gunLevel;
    private float moveSpeed;
    private float turnDegreesPerSecond;
    private float expireAtTime;
    private float impactDistance;
    private Rigidbody2D body;
    private bool impactResolved;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    public void Configure(
        WeaponsScripts shotOwner,
        FishScript lockedTarget,
        int lockedTargetLifeVersion,
        float shotDamage,
        int activeGunLevel,
        float speed,
        float homingTurnDegreesPerSecond,
        float lifetime,
        float closeImpactDistance
    )
    {
        owner = shotOwner;
        target = lockedTarget;
        targetLifeVersion = lockedTargetLifeVersion;
        damage = Mathf.Max(0.05f, shotDamage);
        gunLevel = Mathf.Max(1, activeGunLevel);
        moveSpeed = Mathf.Max(0.1f, speed);
        turnDegreesPerSecond = Mathf.Max(
            1f,
            homingTurnDegreesPerSecond
        );
        expireAtTime = Time.time + Mathf.Max(0.5f, lifetime);
        impactDistance = Mathf.Max(0.05f, closeImpactDistance);
        impactResolved = false;

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    private void FixedUpdate()
    {
        if (Time.time >= expireAtTime)
        {
            Deactivate();
            return;
        }

        if (!IsTargetStillValid() && !TryRetarget())
        {
            Deactivate();
            return;
        }

        Vector3 targetPosition = target.GetTargetCenterWorld();
        Vector2 direction = targetPosition - transform.position;

        if (direction.sqrMagnitude <= impactDistance * impactDistance)
        {
            ResolveTargetImpact(targetPosition);
            return;
        }

        float desiredAngle = Mathf.Atan2(direction.y, direction.x) *
                             Mathf.Rad2Deg - 90f;
        float nextAngle = Mathf.MoveTowardsAngle(
            transform.eulerAngles.z,
            desiredAngle,
            turnDegreesPerSecond * Time.fixedDeltaTime
        );

        if (body != null)
        {
            body.MoveRotation(nextAngle);
            Vector2 forward = Quaternion.Euler(0f, 0f, nextAngle) *
                              Vector2.up;
            body.velocity = forward * moveSpeed;
        }
        else
        {
            transform.rotation = Quaternion.Euler(0f, 0f, nextAngle);
            transform.position += transform.up *
                                  moveSpeed *
                                  Time.fixedDeltaTime;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (impactResolved || other == null)
        {
            return;
        }

        FishScript hitFish = other.GetComponentInParent<FishScript>();

        // Exclusive fallback collision: obstacles and every non-target fish
        // are deliberately ignored, so the Rocket continues toward its lock.
        if (hitFish != target ||
            !IsTargetStillValid())
        {
            return;
        }

        ResolveTargetImpact(other.bounds.center);
    }

    private bool TryRetarget()
    {
        if (owner == null)
        {
            return false;
        }

        FishScript replacement;
        int replacementVersion;

        if (!owner.TryGetAutomaticRocketRetarget(
                target,
                out replacement,
                out replacementVersion
            ))
        {
            return false;
        }

        target = replacement;
        targetLifeVersion = replacementVersion;
        return IsTargetStillValid();
    }

    private bool IsTargetStillValid()
    {
        Camera mainCamera = Camera.main;

        return target != null &&
               target.TargetLifeVersion == targetLifeVersion &&
               target.IsAliveTarget &&
               target.IsTargetVisibleTo(mainCamera);
    }

    private void ResolveTargetImpact(Vector3 impactPosition)
    {
        if (impactResolved || !IsTargetStillValid())
        {
            return;
        }

        impactResolved = true;
        if (owner != null)
        {
            owner.ResolveRocketImpact(
                target,
                targetLifeVersion,
                impactPosition,
                damage,
                gunLevel
            );
        }
        else
        {
            // Safe fallback if the owning weapon was disabled during flight.
            SpriteRenderer targetSprite = target.GetComponent<SpriteRenderer>();

            if (targetSprite == null)
            {
                targetSprite = target.GetComponentInChildren<SpriteRenderer>();
            }

            target.TakeDamage(targetSprite, damage, 0, gunLevel);
        }

        Deactivate();
    }

    private void Deactivate()
    {
        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        owner = null;
        target = null;
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        impactResolved = false;

        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }
}

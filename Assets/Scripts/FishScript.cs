using System.Collections;
using UnityEngine;

public class FishScript : MonoBehaviour
{
    public enum SwimStyle
    {
        Straight,
        Wave,
        Curious,
        OrbitCenter,
        FollowLeader,
        FastTideExit,
        FigureEight,
        BossDash,
        CuteDarting,
        BossArena
    }

    public enum GimmickType
    {
        None,
        BombCrab,
        LightningChain
    }

    [Header("Fish Stats")]
    [SerializeField] public float Hp;
    [SerializeField] public float CoinFish;
    [SerializeField] public float MoveSpeed;
    public int id;

    [Header("Special Gimmicks")]
    public GimmickType gimmickType = GimmickType.None;
    [SerializeField] private float gimmickRadius = 4f;
    [SerializeField] private float gimmickDamage = 50f;

    private float baseHp;
    private float baseCoinFish;
    private float baseMoveSpeed;

    private Rigidbody2D rb2d;
    private SpriteRenderer fishSprite;
    private Animator animator;
    private GameManager gameManager;

    private Coroutine movementCoroutine;
    private Coroutine hitFlashCoroutine;
    private Transform leaderTransform;
    private Vector3 followOffset;

    private bool hasEnteredScreen;
    private bool forceBossReward;
    private bool persistentTargetBoss;
    private bool bossDefeatReported;
    private SwapFishScript spawnDirector;

    private void Awake()
    {
        baseHp = Mathf.Max(1f, Hp);
        baseCoinFish = Mathf.Max(0f, CoinFish);
        baseMoveSpeed = Mathf.Max(0.05f, MoveSpeed);

        rb2d = GetComponent<Rigidbody2D>();
        fishSprite = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (gameManager == null)
        {
            gameManager = GameManager.Instance;
        }

        ResetRuntimeSpawnState();

        if (fishSprite != null)
        {
            fishSprite.color = Color.white;
            AutoAssignSortingOrder();
        }

        // Keep your special darting fish behaviour.
        // A boss role configured by SwapFishScript will replace this movement.
        if (id == 11)
        {
            SetMovementStyle(SwimStyle.CuteDarting);
        }
    }

    private void OnDisable()
    {
        StopMovement();

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = null;
        }

        if (gameManager != null && gameManager.fishInScreenList.Contains(this))
        {
            gameManager.fishInScreenList.Remove(this);
        }
    }

    private void Update()
    {
        if (fishSprite != null && rb2d != null)
        {
            fishSprite.flipY = rb2d.velocity.x < 0f;
        }
    }

    // Called whenever this object is enabled or reused from the pool.
    public void ResetRuntimeSpawnState()
    {
        Hp = baseHp;
        CoinFish = baseCoinFish;
        MoveSpeed = baseMoveSpeed;

        hasEnteredScreen = false;
        forceBossReward = false;
        persistentTargetBoss = false;
        bossDefeatReported = false;
        spawnDirector = null;
        leaderTransform = null;
        followOffset = Vector3.zero;

        if (rb2d != null)
        {
            rb2d.velocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
        }
    }

    // Runtime scaling does not modify the prefab's original Inspector values.
    public void ApplyRuntimeScaling(float hpMultiplier, float rewardMultiplier, float speedMultiplier)
    {
        Hp = Mathf.Max(1f, baseHp * Mathf.Max(0.05f, hpMultiplier));
        CoinFish = Mathf.Max(0f, baseCoinFish * Mathf.Max(0f, rewardMultiplier));
        MoveSpeed = Mathf.Max(0.05f, baseMoveSpeed * Mathf.Max(0.05f, speedMultiplier));
    }

    public void ConfigureAsLevelBoss(SwapFishScript director)
    {
        spawnDirector = director;
        forceBossReward = true;
        persistentTargetBoss = true;
        bossDefeatReported = false;
        AutoAssignSortingOrder();
    }

    public void ConfigureAsMiniBoss()
    {
        spawnDirector = null;
        forceBossReward = true;
        persistentTargetBoss = false;
        bossDefeatReported = false;
        AutoAssignSortingOrder();
    }

    public void TakeDamage(SpriteRenderer targetSprite, float damage, int bulletId, int activeGunLevel)
    {
        if (!gameObject.activeInHierarchy || Hp <= 0f)
        {
            return;
        }

        Hp -= Mathf.Max(0f, damage);

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
        }

        hitFlashCoroutine = StartCoroutine(FlashHitColor(targetSprite));
        FishSystem(bulletId, activeGunLevel);
    }

    private IEnumerator FlashHitColor(SpriteRenderer targetSprite)
    {
        if (targetSprite == null)
        {
            yield break;
        }

        targetSprite.color = new Color(1f, 0.4f, 0.4f, 1f);
        yield return new WaitForSeconds(0.08f);

        if (targetSprite != null)
        {
            targetSprite.color = Color.white;
        }
    }

    public void ResetFishColor(SpriteRenderer targetSprite)
    {
        if (targetSprite == null || !gameObject.activeInHierarchy)
        {
            return;
        }

        StartCoroutine(ResetFishColorRoutine(targetSprite));
    }

    private IEnumerator ResetFishColorRoutine(SpriteRenderer targetSprite)
    {
        yield return new WaitForSeconds(0.1f);

        if (targetSprite != null)
        {
            targetSprite.color = Color.white;
        }
    }

    private void AutoAssignSortingOrder()
    {
        if (fishSprite == null)
        {
            return;
        }

        if (IsBoss())
        {
            fishSprite.sortingOrder = 2000;
        }
        else if (id > 12)
        {
            fishSprite.sortingOrder = Random.Range(500, 900);
        }
        else
        {
            fishSprite.sortingOrder = Random.Range(10, 200);
        }
    }

    public void StopMovement()
    {
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
            movementCoroutine = null;
        }

        if (rb2d != null)
        {
            rb2d.velocity = Vector2.zero;
        }
    }

    public void SetMovementStyle(
        SwimStyle style,
        Transform leader = null,
        Vector3 offset = default(Vector3)
    )
    {
        StopMovement();

        switch (style)
        {
            case SwimStyle.Straight:
                movementCoroutine = StartCoroutine(StraightRoutine());
                break;

            case SwimStyle.Wave:
                movementCoroutine = StartCoroutine(WaveRoutine());
                break;

            case SwimStyle.Curious:
                movementCoroutine = StartCoroutine(CuriousRoutine());
                break;

            case SwimStyle.OrbitCenter:
                movementCoroutine = StartCoroutine(OrbitCenterRoutine());
                break;

            case SwimStyle.FollowLeader:
                leaderTransform = leader;
                followOffset = offset;
                movementCoroutine = StartCoroutine(FollowLeaderRoutine());
                break;

            case SwimStyle.FastTideExit:
                movementCoroutine = StartCoroutine(FastTideExitRoutine());
                break;

            case SwimStyle.FigureEight:
                movementCoroutine = StartCoroutine(FigureEightRoutine());
                break;

            case SwimStyle.BossDash:
                movementCoroutine = StartCoroutine(BossDashRoutine());
                break;

            case SwimStyle.CuteDarting:
                movementCoroutine = StartCoroutine(CuteDartingRoutine());
                break;

            case SwimStyle.BossArena:
                movementCoroutine = StartCoroutine(BossArenaRoutine());
                break;
        }
    }

    private IEnumerator StraightRoutine()
    {
        while (true)
        {
            Vector2 forwardVelocity = transform.right * MoveSpeed;
            Vector2 bobVelocity = transform.up * (Mathf.Sin(Time.time * 3f) * 0.4f);
            rb2d.velocity = forwardVelocity + bobVelocity;
            UpdateAnimationSpeed();
            yield return null;
        }
    }

    private IEnumerator WaveRoutine()
    {
        float waveSpeed = Random.Range(2.5f, 4.5f);
        float waveSize = Random.Range(1.8f, 3.2f);

        while (true)
        {
            Vector2 forwardVelocity = transform.right * MoveSpeed;
            Vector2 waveVelocity = transform.up * (Mathf.Sin(Time.time * waveSpeed) * waveSize);
            rb2d.velocity = forwardVelocity + waveVelocity;
            UpdateAnimationSpeed();
            yield return null;
        }
    }

    private IEnumerator CuriousRoutine()
    {
        float normalSpeed = MoveSpeed;

        while (true)
        {
            MoveSpeed = normalSpeed;
            rb2d.velocity = transform.right * MoveSpeed;
            UpdateAnimationSpeed();
            yield return new WaitForSeconds(Random.Range(1.5f, 3.5f));

            rb2d.velocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);

            float turnAmount = Random.Range(30f, 60f) * (Random.value > 0.5f ? 1f : -1f);
            Quaternion startRotation = transform.rotation;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, transform.eulerAngles.z + turnAmount);
            float progress = 0f;

            while (progress < 1f)
            {
                progress += Time.deltaTime * 2.5f;
                transform.rotation = Quaternion.Lerp(startRotation, targetRotation, progress);
                yield return null;
            }
        }
    }

    private IEnumerator OrbitCenterRoutine()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float radius = Vector3.Distance(transform.position, Vector3.zero);

        if (radius < 1f)
        {
            radius = 3f;
        }

        while (true)
        {
            angle += MoveSpeed * 0.5f * Time.deltaTime;
            Vector3 targetPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            Vector3 direction = (targetPosition - transform.position).normalized;

            rb2d.velocity = direction * MoveSpeed;
            FaceDirection(direction);
            UpdateAnimationSpeed();
            yield return null;
        }
    }

    private IEnumerator FigureEightRoutine()
    {
        float timer = Random.Range(0f, Mathf.PI * 2f);

        while (true)
        {
            timer += Time.deltaTime * Mathf.Max(0.4f, MoveSpeed * 0.2f);

            Vector3 targetPosition = new Vector3(
                Mathf.Sin(timer) * 6f,
                Mathf.Sin(timer * 2f) * 3f,
                transform.position.z
            );

            Vector3 direction = (targetPosition - transform.position).normalized;
            rb2d.velocity = direction * MoveSpeed;
            FaceDirection(direction);
            UpdateAnimationSpeed();
            yield return null;
        }
    }

    private IEnumerator BossDashRoutine()
    {
        float normalSpeed = MoveSpeed;

        while (true)
        {
            Vector3 target = GetRandomArenaPoint(0.72f, 0.68f);
            Vector3 direction = (target - transform.position).normalized;

            MoveSpeed = normalSpeed * 0.55f;
            rb2d.velocity = direction * MoveSpeed;
            FaceDirection(direction);
            yield return new WaitForSeconds(Random.Range(2.5f, 4f));

            rb2d.velocity = Vector2.zero;
            yield return new WaitForSeconds(0.8f);

            target = GetRandomArenaPoint(0.75f, 0.70f);
            direction = (target - transform.position).normalized;
            FaceDirection(direction);

            MoveSpeed = normalSpeed * 2.2f;
            float dashTimer = 0f;

            while (dashTimer < 0.8f)
            {
                rb2d.velocity = direction * MoveSpeed;
                dashTimer += Time.deltaTime;
                UpdateAnimationSpeed();
                yield return null;
            }

            MoveSpeed = normalSpeed;
        }
    }

    private IEnumerator CuteDartingRoutine()
    {
        float normalSpeed = MoveSpeed;

        while (true)
        {
            float randomAngle = Random.Range(0f, 360f);
            Quaternion startRotation = transform.rotation;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, randomAngle);
            float turnTime = 0f;

            while (turnTime < 0.4f)
            {
                turnTime += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, turnTime / 0.4f);
                yield return null;
            }

            MoveSpeed = normalSpeed * 2.8f;
            rb2d.velocity = transform.right * MoveSpeed;
            yield return new WaitForSeconds(0.35f);

            MoveSpeed = normalSpeed * 3.2f;
            rb2d.velocity = transform.right * MoveSpeed;
            yield return new WaitForSeconds(0.30f);

            MoveSpeed = normalSpeed * 0.5f;
            rb2d.velocity = transform.right * MoveSpeed;
            yield return new WaitForSeconds(0.80f);

            MoveSpeed = normalSpeed * 2.7f;
            rb2d.velocity = transform.right * MoveSpeed;
            yield return new WaitForSeconds(0.40f);

            MoveSpeed = normalSpeed * 0.3f;
            rb2d.velocity = transform.right * MoveSpeed;
            yield return new WaitForSeconds(1.20f);
        }
    }

    // New persistent movement for a target boss.
    // It selects safe points inside the camera and never intentionally exits the screen.
    private IEnumerator BossArenaRoutine()
    {
        float normalSpeed = MoveSpeed;

        while (true)
        {
            Vector3 targetPosition = GetRandomArenaPoint(0.72f, 0.68f);

            while (gameObject.activeInHierarchy &&
                   Vector3.Distance(transform.position, targetPosition) > 0.35f)
            {
                Vector3 direction = (targetPosition - transform.position).normalized;
                rb2d.velocity = direction * normalSpeed;
                FaceDirection(direction);
                UpdateAnimationSpeed();
                yield return null;
            }

            rb2d.velocity = Vector2.zero;
            yield return new WaitForSeconds(Random.Range(0.45f, 1.1f));

            if (Random.value < 0.35f)
            {
                Vector3 dashTarget = GetRandomArenaPoint(0.78f, 0.72f);
                Vector3 dashDirection = (dashTarget - transform.position).normalized;
                float dashTimer = 0f;

                FaceDirection(dashDirection);

                while (dashTimer < 0.55f)
                {
                    rb2d.velocity = dashDirection * normalSpeed * 1.8f;
                    dashTimer += Time.deltaTime;
                    UpdateAnimationSpeed();
                    yield return null;
                }
            }
        }
    }

    private IEnumerator FollowLeaderRoutine()
    {
        while (leaderTransform != null && leaderTransform.gameObject.activeSelf)
        {
            Vector3 targetPosition = leaderTransform.position + leaderTransform.TransformDirection(followOffset);
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 4.5f);
            transform.rotation = Quaternion.Slerp(transform.rotation, leaderTransform.rotation, Time.deltaTime * 5f);

            if (leaderTransform.TryGetComponent<FishScript>(out FishScript leaderScript))
            {
                MoveSpeed = leaderScript.MoveSpeed;
            }

            UpdateAnimationSpeed();
            yield return null;
        }

        SetMovementStyle(SwimStyle.Straight);
    }

    private IEnumerator FastTideExitRoutine()
    {
        float exitSpeed = Mathf.Max(MoveSpeed, baseMoveSpeed) * 4f;
        Vector3 exitDirection = transform.right;

        while (true)
        {
            rb2d.velocity = exitDirection * exitSpeed;
            UpdateAnimationSpeed();
            yield return null;
        }
    }

    // private void FishSystem(int bulletId, int activeGunLevel)
    // {
    //     if (Hp > 0f || gameManager == null)
    //     {
    //         return;
    //     }

    //     int safeGunLevel = Mathf.Clamp(
    //         activeGunLevel,
    //         1,
    //         gameManager.weaponsScripts.Bet.Length
    //     );

    //     float bet = gameManager.weaponsScripts.Bet[safeGunLevel - 1];
    //     float coinAmount = Mathf.Round((CoinFish * bet) * 100f) / 100f;

    //     if (gimmickType != GimmickType.None)
    //     {
    //         TriggerGimmickExplosion(bulletId, safeGunLevel);
    //     }

    //     if (IsBoss())
    //     {
    //         Vector3 scatteredPosition = transform.position + (Vector3)Random.insideUnitCircle * 1.5f;
    //         gameManager.animatiorManager.PlayCoin(scatteredPosition, 1, bulletId);
    //         gameManager.animatiorManager.playParticle(transform.position);
    //         gameManager.SoundManager.PlaySoundBigwinCoin1();
    //     }
    //     else
    //     {
    //         if (id == 15)
    //         {
    //             gameManager.animatiorManager.playParticle(transform.position);
    //             gameManager.SoundManager.PlaySoundBigwinCoin1();
    //         }

    //         gameManager.DisplayTextManagerScript.Display("+" + coinAmount, transform.position);
    //         gameManager.coinManager.coinAnima(this, bulletId);
    //     }

    //     if (bulletId == 0)
    //     {
    //         gameManager.CalulateTotalCoinWithCoinFish(coinAmount);
    //         gameManager.SoundManager.PlaySoundCoin1();
    //     }
    //     else
    //     {
    //         gameManager.CalulateNPCCoinFish(coinAmount, bulletId);
    //     }

    //     if (persistentTargetBoss && !bossDefeatReported && spawnDirector != null)
    //     {
    //         bossDefeatReported = true;
    //         spawnDirector.NotifyLevelBossDefeated(this);
    //     }

    //     gameObject.SetActive(false);
    // }



    private void FishSystem(int bulletId, int activeGunLevel)
    {
        if (Hp > 0f || gameManager == null)
        {
            return;
        }

        float bet = 1f;

        // Find the correct bet multiplier based on WHO shot the bullet
        if (bulletId == 0) // Player
        {
            int safeGunLevel = Mathf.Clamp(activeGunLevel, 1, gameManager.weaponsScripts.Bet.Length);
            bet = gameManager.weaponsScripts.Bet[safeGunLevel - 1];
        }
        else if (bulletId == 1) // NPC 1
        {
            int safeGunLevel = Mathf.Clamp(activeGunLevel, 1, gameManager.gun1.Bet.Length);
            bet = gameManager.gun1.Bet[safeGunLevel - 1];
        }
        else if (bulletId == 2) // NPC 2
        {
            int safeGunLevel = Mathf.Clamp(activeGunLevel, 1, gameManager.gun2.Bet.Length);
            bet = gameManager.gun2.Bet[safeGunLevel - 1];
        }
        else if (bulletId == 3) // NPC 3
        {
            int safeGunLevel = Mathf.Clamp(activeGunLevel, 1, gameManager.gun3.Bet.Length);
            bet = gameManager.gun3.Bet[safeGunLevel - 1];
        }

        // Calculate the final multiplied coin amount
        float coinAmount = Mathf.Round((CoinFish * bet) * 100f) / 100f;

        if (gimmickType != GimmickType.None)
        {
            TriggerGimmickExplosion(bulletId, activeGunLevel); // Pass activeGunLevel here too!
        }

        if (IsBoss())
        {
            Vector3 scatteredPosition = transform.position + (Vector3)Random.insideUnitCircle * 1.5f;
            gameManager.animatiorManager.PlayCoin(scatteredPosition, 1, bulletId);
            gameManager.animatiorManager.playParticle(transform.position);
            gameManager.SoundManager.PlaySoundBigwinCoin1();
        }
        else
        {
            if (id == 15)
            {
                gameManager.animatiorManager.playParticle(transform.position);
                gameManager.SoundManager.PlaySoundBigwinCoin1();
            }

            gameManager.DisplayTextManagerScript.Display("+" + coinAmount, transform.position);
            gameManager.coinManager.coinAnima(this, bulletId);
        }

        if (bulletId == 0)
        {
            gameManager.CalulateTotalCoinWithCoinFish(coinAmount);
            gameManager.SoundManager.PlaySoundCoin1();
        }
        else
        {
            gameManager.CalulateNPCCoinFish(coinAmount, bulletId);
        }

        if (persistentTargetBoss && !bossDefeatReported && spawnDirector != null)
        {
            bossDefeatReported = true;
            spawnDirector.NotifyLevelBossDefeated(this);
        }

        gameObject.SetActive(false);
    }




    private void TriggerGimmickExplosion(int bulletId, int activeGunLevel)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, gimmickRadius);
        int chainCount = 0;

        foreach (Collider2D hit in hits)
        {
            if (hit.gameObject == gameObject)
            {
                continue;
            }

            if (!hit.TryGetComponent<FishScript>(out FishScript victim))
            {
                continue;
            }

            if (gimmickType == GimmickType.BombCrab)
            {
                victim.TakeDamage(victim.fishSprite, gimmickDamage, bulletId, activeGunLevel);
            }
            else if (gimmickType == GimmickType.LightningChain && chainCount < 5)
            {
                victim.TakeDamage(victim.fishSprite, gimmickDamage * 1.5f, bulletId, activeGunLevel);
                chainCount++;
            }
        }
    }

    public bool IsBoss()
    {
        if (forceBossReward)
        {
            return true;
        }

        // Compatibility with your existing boss prefab IDs.
        return id == 37 || id == 11 || id == 36 || id == 39 || id == 41 ||
               id == 13 || id == 45 || id == 43 || id == 40;
    }

    private void UpdateAnimationSpeed()
    {
        if (animator == null)
        {
            return;
        }

        animator.speed = Mathf.Clamp(MoveSpeed / 3f, 0.5f, 3f);
    }

    private void FaceDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private Vector3 GetRandomArenaPoint(float widthPercent, float heightPercent)
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null || !mainCamera.orthographic)
        {
            return new Vector3(
                Random.Range(-5f, 5f),
                Random.Range(-3f, 3f),
                transform.position.z
            );
        }

        float halfHeight = mainCamera.orthographicSize * Mathf.Clamp01(heightPercent);
        float halfWidth = mainCamera.orthographicSize * mainCamera.aspect * Mathf.Clamp01(widthPercent);
        Vector3 cameraCenter = mainCamera.transform.position;

        return new Vector3(
            cameraCenter.x + Random.Range(-halfWidth, halfWidth),
            cameraCenter.y + Random.Range(-halfHeight, halfHeight),
            transform.position.z
        );
    }

    private void OnBecameVisible()
    {
        hasEnteredScreen = true;

        if (gameManager != null && !gameManager.fishInScreenList.Contains(this))
        {
            gameManager.fishInScreenList.Add(this);
        }
    }

    private void OnBecameInvisible()
    {
        if (!hasEnteredScreen || !gameObject.activeInHierarchy)
        {
            return;
        }

        if (persistentTargetBoss)
        {
            Vector3 safeCenter = Camera.main != null
                ? new Vector3(
                    Camera.main.transform.position.x,
                    Camera.main.transform.position.y,
                    transform.position.z
                )
                : Vector3.zero;

            transform.position = safeCenter;
            hasEnteredScreen = false;
            SetMovementStyle(SwimStyle.BossArena);
            return;
        }

        gameObject.SetActive(false);
    }
}

using System.Collections.Generic;
using UnityEngine;

public class Shoot : MonoBehaviour
{
    private readonly List<GameObject> bulletPool = new List<GameObject>();
    private readonly HashSet<GameObject> missingBulletScriptWarnings =
        new HashSet<GameObject>();

    public Transform BullepositonToClone;
    private GameManager GM;

    private void Start()
    {
        GM = GameManager.Instance;
    }

    public bool TryShootOnce(
        Transform gunTransform,
        int activeGun,
        int shooterId,
        out float shotCost
    )
    {
        return TryShootOnce(
            gunTransform,
            activeGun,
            shooterId,
            out shotCost,
            null,
            0,
            false
        );
    }

    /// <summary>
    /// Fires one pooled bullet. When targetExclusive is true, the bullet
    /// ignores every fish except the supplied locked target. This is used by
    /// player Auto Shot and Target Lock so intervening fish do not consume a shot.
    /// </summary>
    public bool TryShootOnce(
        Transform gunTransform,
        int activeGun,
        int shooterId,
        out float shotCost,
        FishScript lockedTarget,
        int lockedTargetLifeVersion,
        bool targetExclusive
    )
    {
        shotCost = 0f;

        if (GM == null)
        {
            GM = GameManager.Instance;
        }

        if (GM == null || gunTransform == null ||
            GM.prefab_Bullet == null || GM.prefab_Bullet.Length == 0)
        {
            return false;
        }

        if (targetExclusive &&
            (lockedTarget == null ||
             !lockedTarget.IsAliveTarget ||
             lockedTarget.TargetLifeVersion != lockedTargetLifeVersion))
        {
            return false;
        }

        int index = Mathf.Clamp(
            activeGun - 1,
            0,
            GM.prefab_Bullet.Length - 1
        );

        GameObject prefab = GM.prefab_Bullet[index];
        if (prefab == null)
        {
            return false;
        }

        GameObject bullet = GetBullet(prefab);
        if (bullet == null)
        {
            return false;
        }

        BulletScript script =
            bullet.GetComponentInChildren<BulletScript>(true);

        if (targetExclusive && script == null)
        {
            if (missingBulletScriptWarnings.Add(prefab))
            {
                Debug.LogWarning(
                    "[Auto Shot] Bullet prefab has no BulletScript and cannot " +
                    "use target-exclusive collision: " + prefab.name,
                    prefab
                );
            }

            return false;
        }

        if (!GM.TryPayForShot(shooterId, activeGun, out shotCost))
        {
            return false;
        }

        bullet.transform.position = gunTransform.position;
        bullet.transform.rotation = gunTransform.rotation;

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (script != null)
        {
            script.ConfigureShot(
                shooterId,
                activeGun,
                lockedTarget,
                lockedTargetLifeVersion,
                targetExclusive
            );
        }

        bullet.SetActive(true);

        if (rb != null)
        {
            rb.velocity = gunTransform.up * 20f;
        }

        return true;
    }

    public void ShootOnce(
        Transform gunTransform,
        int activeGun,
        int shooterId
    )
    {
        float ignored;
        TryShootOnce(
            gunTransform,
            activeGun,
            shooterId,
            out ignored
        );
    }

    private GameObject GetBullet(GameObject prefab)
    {
        string targetName = prefab.name + "(Clone)";

        for (int i = 0; i < bulletPool.Count; i++)
        {
            GameObject candidate = bulletPool[i];

            if (candidate != null &&
                !candidate.activeSelf &&
                candidate.name == targetName)
            {
                return candidate;
            }
        }

        GameObject created = Instantiate(prefab, BullepositonToClone);
        bulletPool.Add(created);
        return created;
    }
}

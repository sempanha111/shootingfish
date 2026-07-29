using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Shoot : MonoBehaviour
{
    private List<GameObject> ListBullet = new List<GameObject>();
    public Transform BullepositonToClone;
    private GameManager GM;

    void Start()
    {
        GM = GameManager.Instance;
    }

    public void ShootOnce(Transform gunTransform, int ActiveGun, int Id)
    {
        if (GM == null || GM.prefab_Bullet == null || GM.prefab_Bullet.Length == 0) return;

        // 1. Clamp index to prevent IndexOutOfRangeException
        int index = Mathf.Clamp(ActiveGun - 1, 0, GM.prefab_Bullet.Length - 1);
        GameObject selectedPrefab = GM.prefab_Bullet[index];

        if (selectedPrefab == null) return;

        // 2. Use the prefab's actual name + "(Clone)" for safe object pooling
        string targetName = selectedPrefab.name + "(Clone)";
        GameObject bulletclone = ListBullet.FirstOrDefault(o => o != null && !o.activeSelf && o.name == targetName);

        if (bulletclone == null)
        {
            GameObject bullet = Instantiate(selectedPrefab, BullepositonToClone);
            ListBullet.Add(bullet);
            bulletclone = bullet;
        }

        // 3. Position and activate
        bulletclone.transform.position = gunTransform.position;
        bulletclone.transform.rotation = gunTransform.rotation;
        bulletclone.SetActive(true);

        // 4. Apply physics and data
        Rigidbody2D rb = bulletclone.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = gunTransform.up * 20f;
        }

        BulletScript bs = bulletclone.GetComponent<BulletScript>();
        if (bs != null)
        {
            bs.BulletId = Id;
            bs.acitiveGun = ActiveGun;
        }
    }
}
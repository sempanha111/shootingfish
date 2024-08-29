using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Shoot : MonoBehaviour
{
    private List<GameObject> ListBullet = new List<GameObject>();
    public Transform BullepositonToClone;
    private GameManager GM;
    private BulletScript bs;

    void Start()
    {
         GM = GameManager.Instance;
    }
     public void ShootOnce(Transform gunTransform, int ActiveGun, int Id)
    {


        GameObject bulletclone = ListBullet.FirstOrDefault(o => !o.activeSelf && o.name == "BulletLevel" + ActiveGun+"(Clone)");
        if (bulletclone == null)
        {
            GameObject bullet = Instantiate(GM.prefab_Bullet[ActiveGun - 1], BullepositonToClone);
            ListBullet.Add(bullet);
            bulletclone = bullet;
        }
        bulletclone.transform.position = gunTransform.position;
        bulletclone.transform.rotation = gunTransform.rotation;
        bulletclone.SetActive(true);

        bulletclone.GetComponent<Rigidbody2D>().velocity = gunTransform.up * 20f;
        bs = bulletclone.GetComponent<BulletScript>();
        bs.BulletId = Id;
        bs.acitiveGun = ActiveGun;
    }
}

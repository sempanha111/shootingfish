using UnityEngine;

public class ShootingScript : MonoBehaviour
{

    private BulletScript bs;
    private GameManager GM;
    public float Speed = 16f;

    private void Awake()
    {



    }

    void Start()
    {
        GM = GameManager.Instance;
    }



    public void ShootOnce(Transform gunTransform,int ActiveGun, int Id )
    {

        GameObject bullet = Instantiate(GM.prefab_Bullet[0], gunTransform.position, gunTransform.rotation);
        bullet.GetComponent<Rigidbody2D>().velocity = gunTransform.up * Speed;
        bs = bullet.GetComponent<BulletScript>();
        bs.BulletId = Id;
        bs.acitiveGun = ActiveGun;

    }
// 
  


}

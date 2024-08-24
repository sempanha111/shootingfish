using UnityEngine;

public class ShootingScript : MonoBehaviour
{
    public static ShootingScript Instance { get; private set; }

    private WeaponsScripts ws;
    private BulletScript bs;
    public float Rate = 0f; 
    public float Speed = 16f;
    public float Next_Shot = 0f;
    public float bulletLifetime = 2f;

    private void Awake()
    {

            Instance = this;

    }

    void Start()
    {
        ws = GameManager.Instance.weaponsScripts;
    }

    public void ShootOnce(GameObject prefab_Bullet, Transform gunTransform, int Id)
    {

        GameObject bullet = Instantiate(prefab_Bullet, gunTransform.position, gunTransform.rotation);
        bullet.GetComponent<Rigidbody2D>().velocity = gunTransform.up * Speed;
        bs = bullet.GetComponent<BulletScript>();
        bs.BulletId = Id;

        Debug.Log("Shooting script id :" + Id);

        // Destroy the bullet after a set lifetime
        Destroy(bullet, bulletLifetime);
    }


}

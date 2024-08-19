
using UnityEngine;

public class ShootingScript : MonoBehaviour
{

    public static ShootingScript Instance { get; private set; }

    public GameObject prefab_shoot; // The bullet prefab
    public Transform turret; // The shooting point (like the muzzle)
    public float Rate = 0f; // Fire rate
    public float Speed = 16f; // Bullet speed

    public float Next_Shot = 0f;

    public float bulletLifetime = 2f; // Time after which the bullet is destroyed

    public Animator Net;

    public Animator netAnimator;
    private WeaponsScripts weaponsScripts;
    private GameManager GM;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        weaponsScripts = GameManager.Instance.weaponsScripts;
        GM = GameManager.Instance;
    }


    private void LateUpdate()
    {


        if (Input.GetMouseButtonDown(0))
        {
            if(GM.Amount >= weaponsScripts.Totalbet){
                ShootOnce();
                GM.CalculateTotalCoinWithBet();
            }

            if (Instance != null)
            {
                Instance.UpdateNetReference(netAnimator);
            }

        }


    }
    public void ShootOnce()
    {
        Next_Shot = Time.time + Rate;

        GameObject bullet = Instantiate(prefab_shoot, turret.position, turret.rotation);

        bullet.GetComponent<Rigidbody2D>().velocity = turret.up * Speed;

        Destroy(bullet, bulletLifetime);


        Transform BullepositonToClone = weaponsScripts.BullepositonToClone;

        bullet.transform.SetParent(BullepositonToClone);

    }

    public void UpdateNetReference(Animator newNet)
    {
        Net = newNet;
    }



}

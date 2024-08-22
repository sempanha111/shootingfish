
using UnityEngine;

public class ShootingScript : MonoBehaviour
{

    public static ShootingScript Instance { get; private set; }

    public GameObject prefab_shoot; 
    public Transform turret; 
    public float Rate = 0f; 
    public float Speed = 16f;

    public float Next_Shot = 0f;

    public float bulletLifetime = 2f;

    public Animator Net;

    public Animator netAnimator;


    private void Awake()
    {
        Instance = this;
    }



    private void LateUpdate()
    {

        if (Input.GetMouseButtonDown(0))
        {

            if (Instance != null)
            {
                Instance.UpdateNetReference(netAnimator);
            }

        }

    }
    public void ShootOnce(Transform gunTransform)
    {

        GameObject bullet = Instantiate(prefab_shoot, gunTransform.position, gunTransform.rotation);

        bullet.GetComponent<Rigidbody2D>().velocity = gunTransform.up * Speed;

        Destroy(bullet, bulletLifetime);
        

    }

    public void UpdateNetReference(Animator newNet)
    {
        Net = newNet;
    }



}

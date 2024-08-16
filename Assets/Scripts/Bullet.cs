using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{

    public GameObject prefab_shoot; // The bullet prefab
    public Transform turret; // The shooting point (like the muzzle)
    public float Rate = 0f; // Fire rate
    public float Speed = 16f; // Bullet speed

    private float Next_Shot = 1f;

    void Update()
    {
        // Check for left mouse click (index 0) and if enough time has passed for the next shot
        if (Input.GetMouseButtonDown(0) && Time.time > Next_Shot)
        {
            ShootOnce();
        }
    }

    void ShootOnce()
    {
        Next_Shot = Time.time + Rate; // Set the time for the next allowed shot

        // Instantiate the bullet prefab
        GameObject bullet = Instantiate(prefab_shoot, turret.position, turret.rotation);

        // Set bullet velocity
        bullet.GetComponent<Rigidbody2D>().velocity = turret.up * Speed;
    }
}

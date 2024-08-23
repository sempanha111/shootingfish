using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Unity.Mathematics;
using UnityEngine;

public class BulletScript : MonoBehaviour
{
    public static BulletScript Instance;
    // public GameObject netPrefab;
    [SerializeField] float netTime = 0.3f;

    [SerializeField] float damage = 1f;

    private ShootingScript shootingScript;

    [HideInInspector]public int BulletId = -99;
    void Awake()
    {
        Instance = this;
    }

    private void Start() {
        shootingScript = ShootingScript.Instance;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Fish"))
        {
             SpriteRenderer fishSpriteRenderer = other.GetComponent<SpriteRenderer>();
             FishScript getfishScript = other.GetComponent<FishScript>();

            if (fishSpriteRenderer != null)
            {
                Color fishColor = fishSpriteRenderer.color;
                Color colorFishBackup = fishColor;
                fishColor.a = 0.70f;
                fishSpriteRenderer.color = fishColor;

                getfishScript.ResetFishColor(fishSpriteRenderer,colorFishBackup);
                getfishScript.TakeDamage(fishSpriteRenderer, damage);
                
            }
            
            float fishCenterY = other.bounds.center.y;


            Vector3 netPosition = new Vector3(transform.position.x, fishCenterY, transform.position.z);
            Animator netInstance = Instantiate(shootingScript.Net, netPosition, Quaternion.identity);
   
            Destroy(netInstance.gameObject, netTime);

            Destroy(gameObject);
        }
    }


    // void OnBecameInvisible(GameObject gameObject)//Out of screen phone
    // {
    //     Destroy(gameObject);
    // }



}

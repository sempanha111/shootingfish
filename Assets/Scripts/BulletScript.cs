using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Unity.Mathematics;
using UnityEngine;

public class BulletScript : MonoBehaviour
{

    public int BulletId;
    public static BulletScript Instance;
    // public GameObject netPrefab;
    [SerializeField] float netTime = 0.3f;

    [SerializeField] float damage = 1f;

    private WeaponsScripts ws;

    void Awake()
    {
        Instance = this;
    }

    private void Start() {
        ws = GameManager.Instance.weaponsScripts;
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
                fishColor.a = 0.60f;
                fishSpriteRenderer.color = fishColor;

                getfishScript.ResetFishColor(fishSpriteRenderer,colorFishBackup);
                getfishScript.TakeDamage(fishSpriteRenderer, damage, BulletId);
                
            }
            
            float fishCenterY = other.bounds.center.y;


            Vector3 netPosition = new Vector3(transform.position.x, fishCenterY, transform.position.z);
            Animator netInstance = Instantiate(ws.Net[ws.activeGunLevel - 1], netPosition, Quaternion.identity);

            // Debug.Log("BUlLet ID :" + BulletId);
   
            Destroy(netInstance.gameObject, netTime);

            Destroy(gameObject);
        }
    }


}


using UnityEngine;

public class BulletScript : MonoBehaviour
{

    public int BulletId;
    public int acitiveGun;
    // public GameObject netPrefab;
    [SerializeField] float netTime = 0.3f;

    [SerializeField] float damage = 1f;

    private GameManager GM;

    private void Start() {
        GM = GameManager.Instance;
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

            var netInstance = Instantiate(GM.Net[acitiveGun - 1], netPosition, Quaternion.identity);


   
            Destroy(netInstance.gameObject, netTime);

            Destroy(gameObject);
        }
    }

    void OnBecameInvisible()
    {
        Destroy(gameObject);
    }


}


using UnityEngine;

public class BulletScript : MonoBehaviour
{

    public int BulletId;
    public int acitiveGun;
    [SerializeField] float netTime = 0.3f;

    [SerializeField] float damage = 1f;
    Color redcolor = new Color(1.0f, 0.58f, 0.58f, 9.88f);
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
                fishSpriteRenderer.color = redcolor;

                getfishScript.ResetFishColor(fishSpriteRenderer);
                getfishScript.TakeDamage(fishSpriteRenderer, damage, BulletId, acitiveGun);
                
            }
            
            float fishCenterY = other.bounds.center.y;
            Vector3 netPosition = new Vector3(transform.position.x, fishCenterY, transform.position.z);

            var netInstance = Instantiate(GM.Net[acitiveGun - 1], netPosition, Quaternion.identity);


   
            Destroy(netInstance.gameObject, netTime);

            gameObject.SetActive(false);
        }
    }

    void OnBecameInvisible()
    {
        gameObject.SetActive(false);
    }


}

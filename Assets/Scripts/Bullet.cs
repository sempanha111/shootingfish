using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    // public GameObject netPrefab;
    [SerializeField] float netTime = 0.3f;

    private ShootingScript shootingScript;

    private void Start() {
        shootingScript = ShootingScript.Instance;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Fish"))
        {
            // Change the fish's opacity to 0.90
            // SpriteRenderer fishSpriteRenderer = other.GetComponent<SpriteRenderer>();
            // if (fishSpriteRenderer != null)
            // {
            //     Color fishColor = fishSpriteRenderer.color;
            //     fishColor.a = 0.30f;
            //     fishSpriteRenderer.color = fishColor;

            
              
            //     StartCoroutine(ResetFishOpacity(fishSpriteRenderer, resetOpacityTime));
            // }

            float fishCenterY = other.bounds.center.y;

            Vector3 netPosition = new Vector3(transform.position.x, fishCenterY, transform.position.z);


            Animator netInstance = Instantiate(shootingScript.Net, netPosition, Quaternion.identity);

            Destroy(netInstance.gameObject, netTime);

            // Destroy the bullet
            Destroy(gameObject);
        }
    }

}

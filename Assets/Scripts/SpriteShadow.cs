using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpriteShadow : MonoBehaviour
{
    public Vector3 offset = new Vector3(-0.2f, -0.2f, 0f);
    
    private GameObject shadowClone;
    private SpriteRenderer shadowSpriteRenderer;
    private SpriteRenderer parentSpriteRenderer;

    void Start()
    {
        parentSpriteRenderer = GetComponent<SpriteRenderer>();

        // Create the shadow clone object
        shadowClone = Instantiate(GameManager.Instance.Shadow, transform);
        shadowClone.name = gameObject.name + "_Shadow";

        // Add and configure the shadow's SpriteRenderer
        shadowSpriteRenderer = shadowClone.AddComponent<SpriteRenderer>();
        shadowSpriteRenderer.color = GameManager.Instance.shadowColor;
        
        // Ensure shadow renders directly behind the fish properly
        shadowSpriteRenderer.sortingLayerName = "Fish";
        shadowSpriteRenderer.sortingOrder = parentSpriteRenderer.sortingOrder - 1;

        // Apply scale down
        float scale = shadowClone.transform.localScale.x;
        scale -= scale * 0.2f;
        shadowClone.transform.localScale = Vector3.one * scale;
        
        // Notice we do NOT copy the Animator. Copying the sprite frame in LateUpdate is much better!
    }

    // LateUpdate runs after the Animator has changed the Fish's sprite for this frame
    void LateUpdate()
    {
        // Continuously sync the shadow to match the parent perfectly
        if (shadowClone != null && parentSpriteRenderer != null)
        {
            // Frame-perfect animation copying without needing a second Animator
            shadowSpriteRenderer.sprite = parentSpriteRenderer.sprite;
            
            // Match flips in case the fish turns around
            shadowSpriteRenderer.flipX = parentSpriteRenderer.flipX;
            shadowSpriteRenderer.flipY = parentSpriteRenderer.flipY;

            // Follow position and rotation dynamically
            shadowClone.transform.position = transform.position + offset;
            shadowClone.transform.rotation = transform.rotation;
        }
    }
}
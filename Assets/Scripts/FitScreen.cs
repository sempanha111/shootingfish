
using UnityEngine;

public class FitScreen : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;
    private void LateUpdate()
    {
        FitImage();
    }

    public void FitImage(){
        // Get the screen dimensions
        float screenHeight = Camera.main.orthographicSize * 2.0f;
        float screenWidth = screenHeight * Screen.width / Screen.height;

        // Get the sprite dimensions
        float spriteWidth = spriteRenderer.sprite.bounds.size.x;
        float spriteHeight = spriteRenderer.sprite.bounds.size.y;

        // Calculate the scale factor
        float scaleX = screenWidth / spriteWidth;
        float scaleY = screenHeight / spriteHeight;


        // Set the scale of the sprite
        transform.localScale = new Vector3(scaleX, scaleY, 1f);
        // Destroy(this);
    }
}

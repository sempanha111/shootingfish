using UnityEngine;

public class ResponsiveObjectPlacement  : MonoBehaviour
{
    public Transform Object;
    
    private void LateUpdate()
    {
        var uiElement = GetComponent<RectTransform>();
        Vector3 worldPosition =   new Vector3(uiElement.position.x, uiElement.position.y, 0);
        Object.position = worldPosition;
        Destroy(this);
    }
}

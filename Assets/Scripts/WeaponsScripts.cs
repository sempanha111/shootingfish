using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class WeaponsScripts : MonoBehaviour
{

    public GameObject gunlevel1;
    public GameObject gunlevel2;

    public GameObject Gun1;
    public GameObject Gun2;

    public Animator GunLevel1Animator;
    public Animator GunLevel2Animator;

    private bool isGunLevel1Active = true;

    [SerializeField] private float AnimaShootWait = 0.11f;
    [SerializeField] private int posZ = 10;
    void Start()
    {
        gunlevel1.SetActive(true);
        gunlevel2.SetActive(false);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {

        Vector3 mousePosition = Input.mousePosition;
        mousePosition.z = Mathf.Abs(Camera.main.transform.position.z);  // Set z to the camera's distance from the gun

        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mousePosition);
        worldPosition.z = transform.position.z;  // Set world position Z to match the gun's Z-plane

        // Calculate the direction from the gun to the target position
        Vector3 direction = worldPosition - Gun1.transform.position;

        // Calculate the rotation angle
        float originalAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        float transformedAngle = originalAngle - 90f;

        // Debugging info
        Debug.DrawLine(Gun1.transform.position, worldPosition, Color.red, 2f);
        Debug.Log($"World Position: {worldPosition}, Direction: {direction}, Angle: {transformedAngle}");


            if (isGunLevel1Active)
            {
                Gun1.transform.rotation = Quaternion.Euler(0, 0, transformedAngle);
                StartCoroutine(SwitchAndAnimateGun(gunlevel1, GunLevel1Animator, "GunSpriteLevel1", "IdleGun1Animation"));
            }
            else
            {
                 Gun2.transform.rotation = Quaternion.Euler(0, 0, transformedAngle);
                StartCoroutine(SwitchAndAnimateGun(gunlevel2, GunLevel2Animator, "GunSpriteLevel2", "IdleGun2Animation"));
            }
        }

    }

    private IEnumerator SwitchAndAnimateGun(GameObject gun, Animator animator, string animationName, string idleanimation)
    {
        // Play the specified animation
        animator.Play(animationName);


        // Wait for the animation to complete
        yield return new WaitForSeconds(AnimaShootWait);

        // Play the idle animation after the action animation
        animator.Play(idleanimation);
        Debug.Log("Stop after 0.");
    }


}

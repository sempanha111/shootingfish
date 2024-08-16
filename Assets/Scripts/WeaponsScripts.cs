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

    [SerializeReference] int activeGunLevel = 2;

    [SerializeField] private float AnimaShootWait = 0.11f;


    



    void Start()
    {
        ActivateGun(activeGunLevel);
    }

    void Update()
    {
        ActivateGun(activeGunLevel);
        if (Input.GetMouseButtonDown(0))
        {
            float angle = GetAngle(GetActiveGun().transform);
            GetActiveGun().transform.rotation = Quaternion.Euler(0, 0, angle);

            string animationName = $"GunSpriteLevel{activeGunLevel}";
            string idleAnimation = $"IdleGun{activeGunLevel}Animation";
            StartCoroutine(SwitchAndAnimateGun(GetActiveGunAnimator(), animationName, idleAnimation));
        }
    }

    private void ActivateGun(int level)
    {
        activeGunLevel = level;

        gunlevel1.SetActive(level == 1);
        gunlevel2.SetActive(level == 2);
    }

    private GameObject GetActiveGun()
    {
        switch (activeGunLevel)
        {
            case 1: return Gun1;
            case 2: return Gun2;
            default: return Gun1;
        }
    }

    private Animator GetActiveGunAnimator()
    {
        switch (activeGunLevel)
        {
            case 1: return GunLevel1Animator;
            case 2: return GunLevel2Animator;
            default: return GunLevel1Animator;
        }
    }

    private IEnumerator SwitchAndAnimateGun(Animator animator, string animationName, string idleAnimation)
    {
        // Play the specified animation
        animator.Play(animationName);

        // Wait for the animation to complete
        yield return new WaitForSeconds(AnimaShootWait);

        // Play the idle animation after the action animation
        animator.Play(idleAnimation);
        Debug.Log("Animation complete.");
    }

    public float GetAngle(Transform gunTransform)
    {
        Vector3 mousePosition = Input.mousePosition;
        mousePosition.z = Mathf.Abs(Camera.main.transform.position.z);

        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mousePosition);
        worldPosition.z = gunTransform.position.z;

        Vector3 direction = worldPosition - gunTransform.position;

        float angle = (Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg) - 90f;


        return angle;
    }
}

using System.Collections;
using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;

public class WeaponsScripts : MonoBehaviour
{
    public static WeaponsScripts Instance;
    public Transform BullepositonToClone;
    public GameObject[] Gunlevel;
    public GameObject[] Gun;
    public Animator[] Anima_Gun;

    public GameObject poinClick;

    [SerializeField] int activeGunLevel = 1; // Default to level 2
    [SerializeField] private float AnimaShootWait = 0.11f;


    private ShootingScript shootingScript;


    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        shootingScript = ShootingScript.Instance;

        ActivateGun(activeGunLevel);
    }

    void Update()
    {
        ActivateGun(activeGunLevel);
        if (Input.GetMouseButtonDown(0))
        {
            PoinMouseClick();
            RotateActiveGun();
            StartCoroutine(SwitchAndAnimateGun(Anima_Gun[activeGunLevel - 1], "Shoot" + (activeGunLevel), "Idle" + (activeGunLevel)));
        }
    }

    private void ActivateGun(int gunLevel)
    {
        for (int i = 0; i < Gunlevel.Length; i++)
        {
            Gunlevel[i].SetActive(i == activeGunLevel - 1); 
        }

    
    }

    private void RotateActiveGun()
    {

        GameObject activeGun = Gun[activeGunLevel - 1];

        float angle = GetAngle(activeGun.transform);

        activeGun.transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private IEnumerator SwitchAndAnimateGun(Animator animator, string animationName, string idleAnimation)
    {
        animator.Play(animationName);

        yield return new WaitForSeconds(AnimaShootWait);

        animator.Play(idleAnimation);
    }

    public float GetAngle(Transform gunTransform)
    {
        // Get mouse position in world space
        Vector3 mousePosition = Input.mousePosition;
        mousePosition.z = Mathf.Abs(Camera.main.transform.position.z);

        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mousePosition);
        worldPosition.z = gunTransform.position.z;

        // Calculate the direction vector from the gun to the mouse position
        Vector3 direction = worldPosition - gunTransform.position;

        // Calculate the angle in degrees and adjust by 90 degrees to match initial rotation
        float angle = (Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg) - 90f;

        return angle;
    }


    private void PoinMouseClick(){
        Vector3 mouseposition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseposition.z = 0 ;

        GameObject poinClickMouse =  Instantiate(poinClick, mouseposition, Quaternion.identity);

        Destroy(poinClickMouse, 0.2f);
    }


}

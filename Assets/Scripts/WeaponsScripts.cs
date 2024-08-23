using System.Collections;
using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;

public class WeaponsScripts : MonoBehaviour
{

    public Transform BullepositonToClone;
    public GameObject[] Gunlevel;
    public GameObject[] Gun;
    public Animator[] Anima_Gun;
    [SerializeField] public float[] Bet;
    public int bulletId = -99;
    public GameObject poinClick;

    [SerializeField] public int activeGunLevel = 1;
    [SerializeField] private float AnimaShootWait = 0.11f;

    public float Totalbet;

    private GameManager GM;
    private ShootingScript shootScript;


    public Animator newNetupdate;
    public GameObject  activeGun;
    void Start()
    {
        GM = GameManager.Instance;
        shootScript = ShootingScript.Instance;
        ActivateGun(activeGunLevel);
    }

    public void ActivateGun(int activeGunLevel)
    {
        for (int i = 0; i < Gunlevel.Length; i++)
        {
            Gunlevel[i].SetActive(i == activeGunLevel - 1);
        }
        AlwayGetBet(activeGunLevel);
        activeGun = Gun[activeGunLevel - 1];
    }

    private void AlwayGetBet(int gunLevel)
    {
        Totalbet = Bet[gunLevel - 1];
    }

    private void RotateActiveGun()
    {
        float angle = GetAngle(activeGun.transform);
        activeGun.transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private IEnumerator SwitchAndAnimateGun(Animator animator, string animationName, string idleAnimation)
    {
        animator.Play(animationName);

        yield return new WaitForSeconds(AnimaShootWait);

        animator.Play(idleAnimation);
    }

    private float GetAngle(Transform gunTransform)
    {

        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        worldPosition.z = 0;

        Vector3 direction = worldPosition - gunTransform.position;

        float angle = (Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg) - 90f;

        return angle;
    }


    private void PoinMouseClick()
    {
        Vector3 mouseposition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseposition.z = 0;

        GameObject poinClickMouse = Instantiate(poinClick, mouseposition, Quaternion.identity);

        Destroy(poinClickMouse, 0.2f);
    }
    private bool holding = false;
    private float timeToShoot= 0;
    private float fireRate = 5;

    void Update()
    {
        if(holding)
        {
            if(timeToShoot <= Time.time){
            ShootingClick();
            timeToShoot = Time.time + 1/fireRate;//0+1/10 = 
            }
        }
    }

    public void ShootingClick()
    {

        RotateActiveGun();
        if (GM.Amount >= Totalbet)
        {
            PoinMouseClick();
            StartCoroutine(SwitchAndAnimateGun(Anima_Gun[activeGunLevel - 1], "Shoot" + (activeGunLevel), "Idle" + (activeGunLevel)));
            shootScript.ShootOnce(activeGun.transform,bulletId);
            GM.CalculateTotalCoinWithBet();
        }
    }

    public void OnClickDown()
    {
        holding = true;
    }

     public void OnClickUp()
    {
        holding = false;
    }

}

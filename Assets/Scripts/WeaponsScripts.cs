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


    public GameObject poinClick;

    [SerializeField] public int activeGunLevel = 1;
    [SerializeField] private float AnimaShootWait = 0.11f;

    public float Totalbet;

    private GameManager GM;
    private BulletScript bs;


    public GameObject  activeGun;
    void Start()
    {
        GM = GameManager.Instance;
        ActivateGun(activeGunLevel);
    }

    public void ActivateGun(int gununLevelSet)
    {
        for (int i = 0; i < Gunlevel.Length; i++)
        {
            Gunlevel[i].SetActive(i == gununLevelSet - 1);
        }
        AlwayGetBet(gununLevelSet);
        activeGun = Gun[gununLevelSet - 1];
        activeGunLevel = gununLevelSet;
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
            PoinMouseClick();
            timeToShoot = Time.time + 1/fireRate;//0+1/10 = 
            }
        }
    }

    public void ShootingClick()
    {

        RotateActiveGun();
        if (GM.Amount >= Totalbet)
        {
            
            StartCoroutine(SwitchAndAnimateGun(Anima_Gun[activeGunLevel - 1], "Shoot" + (activeGunLevel), "Idle" + (activeGunLevel)));
            ShootOnce(activeGun.transform, activeGunLevel, 0);
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

    public void ShootOnce(Transform gunTransform,int ActiveGun, int Id )
    {

        GameObject bullet = Instantiate(GM.prefab_Bullet[ActiveGun - 1], gunTransform.position, gunTransform.rotation);
        bullet.GetComponent<Rigidbody2D>().velocity = gunTransform.up * 16f;
        bs = bullet.GetComponent<BulletScript>();
        bs.BulletId = Id;
        bs.acitiveGun = ActiveGun;


    }

}

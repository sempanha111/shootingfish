using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class WeaponsScripts : MonoBehaviour
{


    public GameObject[] Gunlevel;
    public GameObject[] Gun;
    public Animator[] Anima_Gun;
    [SerializeField] public float[] Bet;


    private List<GameObject> Listpoinclick = new List<GameObject>();


    public GameObject poinClick;

    [SerializeField] public int activeGunLevel = 1;
    [SerializeField] private float AnimaShootWait = 0.11f;

    public float Totalbet;

    private GameManager GM;
    private BulletScript bs;


    public GameObject activeGun;
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
        if (animator == null || !animator.gameObject.activeInHierarchy)
        {
            yield break;
        }

        animator.Play(animationName);

        yield return new WaitForSeconds(AnimaShootWait);

        if (animator == null || !animator.gameObject.activeInHierarchy)
        {
            yield break;
        }

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

        GameObject poinclickClone = Listpoinclick.FirstOrDefault(o => !o.activeSelf);
        if(poinclickClone == null){
            GameObject poininstan = Instantiate(poinClick);
            Listpoinclick.Add(poininstan);
            poinclickClone = poininstan;
        }
        poinclickClone.SetActive(true);
        poinclickClone.transform.position = mouseposition;
        poinclickClone.transform.rotation = Quaternion.identity;

        StartCoroutine(IEnumResetpoin(poinclickClone));
    }

    private IEnumerator IEnumResetpoin(GameObject poinclickClone){
        yield return new WaitForSeconds(0.2f);
        poinclickClone.SetActive(false);
    }

    private bool holding = false;
    private float timeToShoot = 0;
    private float fireRate = 5;

    void Update()
    {
        if (holding)
        {

            if (timeToShoot <= Time.time)
            {
                ShootingClick();
                PoinMouseClick();
                timeToShoot = Time.time + 1 / fireRate;//0+1/10 = 
            }
        }
    }

    public void ShootingClick()
    {

        RotateActiveGun();
        if (GM.Amount >= Totalbet)
        {

            StartCoroutine(SwitchAndAnimateGun(Anima_Gun[activeGunLevel - 1], "Shoot" + (activeGunLevel), "Idle" + (activeGunLevel)));
            GM.shoot.ShootOnce(activeGun.transform, activeGunLevel, 0);
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

using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class WeaponNPC : MonoBehaviour
{

    [SerializeField] int id;
    private GameManager GM;
    private Gun1 gun1;
    private Gun2 gun2;
    private Gun3 gun3;


    private GameObject[] Gunlevel;
    private GameObject[] Gun;
    private Animator[] Anima_Gun;
    private float[] Bet;
    public int activeGunLevel = 1;
    public float Totalbet;
    [SerializeField] private float AnimaShootWait = 0.11f;
    private GameObject activeGun;
    private BulletScript bs;

    void Start()
    {
        GM = GameManager.Instance;
        gun1 = GM.gun1;
        gun2 = GM.gun2;
        gun3 = GM.gun3;


        ChooseWeapon(id);

        ActivateGun(activeGunLevel);
    }




    private void ChooseWeapon(int id)
    {

        switch (id)
        {
            case 1:
                Gunlevel = gun1.Gunlevel;
                Gun = gun1.Gun;
                Anima_Gun = gun1.Anima_Gun;
                Bet = gun1.Bet;




                break;
            case 2:
                Gunlevel = gun2.Gunlevel;
                Gun = gun2.Gun;
                Anima_Gun = gun2.Anima_Gun;
                Bet = gun2.Bet;



                break;
            default:
                Gunlevel = gun3.Gunlevel;
                Gun = gun3.Gun;
                Anima_Gun = gun3.Anima_Gun;
                Bet = gun3.Bet;



                break;

        }
    }


    public void ActivateGun(int gunLevelSet)
    {
        for (int i = 0; i < Gunlevel.Length; i++)
        {
            Gunlevel[i].SetActive(i == gunLevelSet - 1);

        }
        GetEachBetGun(gunLevelSet);
        activeGun = Gun[gunLevelSet - 1];

        activeGunLevel = gunLevelSet;


    }

    void GetEachBetGun(int activeGunLevel)
    {
        Totalbet = Bet[activeGunLevel - 1];
    }

    private float timeToShoot = 0;
    private float fireRate = 5;
    FishScript targetFish;
    void Update()
    {


        GM.UIManager.SetTextBetNPC(id, Totalbet.ToString());

        if (id == 1)
        {
            GM.UIManager.SetTextTotalNPC(id, gun1.AmountCoin.ToString());
        }
        else if (id == 2)
        {
            GM.UIManager.SetTextTotalNPC(id, gun2.AmountCoin.ToString());
        }
        else
        {
            GM.UIManager.SetTextTotalNPC(id, gun3.AmountCoin.ToString());
        }


        if (targetFish == null || !targetFish.gameObject.activeSelf)
        {
          
            targetFish = FindFish();

            if (targetFish == null)
            {
                return; 
            }

        }
        else
        {
            if (timeToShoot <= Time.time)
            {
                RotateActiveGun();
                ShootingAIClick();
                timeToShoot = Time.time + 1 / fireRate;
            }
        }
    }


    public void ShootingAIClick()
    {
        StartCoroutine(SwitchAndAnimateGun(Anima_Gun[activeGunLevel - 1], "Shoot" + (activeGunLevel), "Idle" + (activeGunLevel)));
        GM.shoot.ShootOnce(activeGun.transform, activeGunLevel, id);
        switch(id){
            case 1:
                gun1.AmountCoin -= Totalbet;
                break;
            case 2:
                gun2.AmountCoin -= Totalbet;
                break;
            default:
                gun3.AmountCoin -= Totalbet;
                break;
        }
    }
    // Function 
    private void RotateActiveGun()
    {
        float angle = GetAngle(activeGun.transform, targetFish.transform.position);
        activeGun.transform.rotation = Quaternion.Euler(0, 0, angle);
    }
    private float GetAngle(Transform gunTransform, Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - gunTransform.position;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

        return angle;
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


    private FishScript FindFish()
    {
        FishScript[] Fishes = GM.fishInScreenList.ToArray();
        FishScript closestFish = null;
        float closestDistance = Mathf.Infinity;

        foreach (FishScript fish in Fishes)
        {
            if (!fish.gameObject.activeSelf)
            {
                continue;
            }
            float distanceToFish = Vector3.Distance(transform.position, fish.transform.position);
            if (distanceToFish < closestDistance)
            {
                closestDistance = distanceToFish;
                closestFish = fish;
            }
        }

        ChangeGun(closestFish);
        return closestFish;
    }

    void ChangeGun(FishScript fishscript)
    {
        if (fishscript == null)
        {
            return;
        }

        if (fishscript.Hp <= 40)
        {
            ActivateGun(17);
        }
        else if (fishscript.Hp <= 50)
        {
            ActivateGun(17);
        }
        else if (fishscript.Hp <= 60)
        {
            ActivateGun(17);
        }
        else
        {
            ActivateGun(17);
        }
    }








}

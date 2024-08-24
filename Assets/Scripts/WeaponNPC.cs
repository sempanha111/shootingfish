using System.Collections;
using System.Collections.Generic;


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
    public GameObject[] prefab_Bullet;
    public Animator[] Net;
    public int activeGunLevel = 1;
    public float Totalbet;
    [SerializeField] private float AnimaShootWait = 0.11f;
    private GameObject activeGun;
    private ShootingScript shootingScript;

    void Start()
    {
        GM = GameManager.Instance;
        shootingScript = ShootingScript.Instance;
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


                Debug.Log("Debug from GunNPC 1");

                break;
            case 2:
                Gunlevel = gun2.Gunlevel;
                Gun = gun2.Gun;
                Anima_Gun = gun2.Anima_Gun;
                Bet = gun2.Bet;

                Debug.Log("Debug from GunNPC 2");

                break;
            default:
                Gunlevel = gun3.Gunlevel;
                Gun = gun3.Gun;
                Anima_Gun = gun3.Anima_Gun;
                Bet = gun3.Bet;

                Debug.Log("Debug from GunNPC 3");

                break;

        }
    }


    public void ActivateGun(int activeGunLevel)
    {
        for (int i = 0; i < Gunlevel.Length; i++)
        {
            Gunlevel[i].SetActive(i == activeGunLevel - 1);

        }
        GetEachBetGun(activeGunLevel);
        activeGun = Gun[activeGunLevel - 1];

    }

    void GetEachBetGun(int activeGunLevel)
    {
        Totalbet = Bet[activeGunLevel - 1];
    }

    FishScript targetFish = null;
    private float timeToShoot= 0;
    private float fireRate = 12;
    void Update()
    {
        GM.UIManager.SetTextTotalNPC(id, gun1.AmountCoin.ToString());
        GM.UIManager.SetTextBetNPC(id, Totalbet.ToString());


        if (targetFish == null)
        {
            targetFish = FindFish();
        }
        else
        {
            if(timeToShoot <= Time.time){
                RotateActiveGun();
                ShootingAIClick();
                timeToShoot = Time.time + 5 / fireRate;
            }
        }


    }


    public void ShootingAIClick()
    {
        
        StartCoroutine(SwitchAndAnimateGun(Anima_Gun[activeGunLevel - 1], "Shoot" + (activeGunLevel), "Idle" + (activeGunLevel)));
        shootingScript.ShootOnce(prefab_Bullet[activeGunLevel - 1], activeGun.transform, id);
        gun1.AmountCoin -= Bet[activeGunLevel - 1];
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
        animator.Play(animationName);

        yield return new WaitForSeconds(AnimaShootWait);

        animator.Play(idleAnimation);

        // Debug.Log("play Animatin banh in Animator Gun[" + (activeGunLevel - 1) +"] Shoot" + (activeGunLevel));
    }

    private FishScript FindFish()
    {
        FishScript[] Fishes = GM.fishInScreenList.ToArray();
        FishScript closestFish = null;

        float closestDistance = Mathf.Infinity;

        foreach (FishScript fish in Fishes)
        {
            float distanceToFish = Vector3.Distance(transform.position, fish.transform.position);
            if (distanceToFish < closestDistance)
            {
                closestDistance = distanceToFish;
                closestFish = fish;
            }
        }

        return closestFish;
    }





}

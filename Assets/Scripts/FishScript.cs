using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FishScript : MonoBehaviour
{
    [SerializeField] public float Hp;
    [SerializeField] public float CoinFish;
    [SerializeField] public float MoveSpeed;
    public int id;

    private float HpBackup;

    private Rigidbody2D rb2d;

    SpriteRenderer fishsprite;
    Animator animator;



    private GameManager GM;

    private void Start()
    {
        HpBackup = Hp;
        GM = GameManager.Instance;
        MoveHandle();
        fishsprite = this.GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        if (id == 13)
        {
            StartCoroutine(ChangeSpeedRoutine());
        }
        else
        {
            StartCoroutine(ChangeSpeedNormalFish());
        }
   
    }

    private bool IsDead()
    {
        return Hp <= 0;
    }



    void Resetcolor(SpriteRenderer fish)
    {
        fish.color = new Color(1f, 1f, 1f, 1f);
    }
    public void ResetFishColor(SpriteRenderer Fish)
    {
        if (Fish == null || !Fish.gameObject.activeSelf) return;
        StartCoroutine(IEnumResetFishColor(Fish));
    }

    private IEnumerator IEnumResetFishColor(SpriteRenderer Fish)
    {
        yield return new WaitForSeconds(0.1f);
        Fish.color = new Color(1f, 1f, 1f, 1f);
    }

    public void TakeDamage(SpriteRenderer Fish, float Damage, int BulletId, int acitiveGunlevel)
    {
        Hp -= Damage;
        FishSystem(Fish, BulletId, acitiveGunlevel);
    }

    private void FishSystem(SpriteRenderer Fish, int BulletId, int acitiveGunlevel)
    {
        if (IsDead())
        {
            float Bet = GM.weaponsScripts.Bet[acitiveGunlevel - 1];
            float coinAmount = CoinFish * Bet;
            coinAmount = Mathf.Round(coinAmount * 100f) / 100f;

            if (id == 13)
            {
                GM.animatiorManager.playParticle(transform.position);
                GM.animatiorManager.PlayCoin(transform.position, 1, BulletId);

                if (BulletId == 0)
                {
                    GM.SoundManager.PlaySoundBigwinCoin1();
                }

            }
            else
            {
                GM.DisplayTextManagerScript.Display(("+" + coinAmount).ToString(), transform.position);
                GM.coinManager.coinAnima(this, BulletId);
            }


            if (BulletId == 0)
            {
                GM.CalulateTotalCoinWithCoinFish(coinAmount * Bet);
                if (coinAmount < 100)
                {

                    GM.SoundManager.PlaySoundCoin1();
                }
                else
                {
                    GM.SoundManager.PlaySoundCoin2();
                }
                Debug.Log(coinAmount);
            }
            else
            {
                GM.CalulateNPCCoinFish(coinAmount, BulletId);
            }


            GM.fishInScreenList.Remove(this);




            fishsprite.flipY = false;
            this.Hp = HpBackup;
            Resetcolor(fishsprite);
            gameObject.SetActive(false);
        }
    }

    private IEnumerator ChangeSpeedRoutine()
    {
        // First speed = Normal

        rb2d.velocity = transform.right * MoveSpeed;


        Debug.Log($"First Normal Speed: {MoveSpeed}");

        // Keep first speed for a while
        yield return new WaitForSeconds(Random.Range(5f, 7f));

        while (gameObject.activeInHierarchy)
        {
            int state = Random.Range(0, 3);

            switch (state)
            {
                case 0: // Slow
                    MoveSpeed = Random.Range(1f, 1.5f);
                    break;

                case 1: // Normal
                    MoveSpeed = Random.Range(2.1f, 2.5f);
                    break;

                case 2: // Fast
                    MoveSpeed = Random.Range(2f, 2.9f);
                    break;
            }

            rb2d.velocity = transform.right * MoveSpeed;
            UpdateAnimationSpeed();

            float keepTime = Random.Range(3f, 5f);

            Debug.Log($"State: {state}, Speed: {MoveSpeed}, Time: {keepTime}");

            yield return new WaitForSeconds(keepTime);
        }
    }
    private IEnumerator ChangeSpeedNormalFish()
    {
        rb2d.velocity = transform.right * MoveSpeed;
        yield return new WaitForSeconds(Random.Range(2f, 5f));

        // while (gameObject.activeInHierarchy)
        // {
        //     float change = Random.Range(-0.5f, 1.5f);

        //     MoveSpeed += change;

        //     MoveSpeed = Mathf.Clamp(MoveSpeed, -1.5f, 1.5f);

        //     rb2d.velocity = transform.right * MoveSpeed;
        //     UpdateAnimationSpeed();

        //     yield return new WaitForSeconds(Random.Range(6f, 10f));
        // }
    }



    private void UpdateAnimationSpeed()
    {
        if (animator == null) return;

        // Example: speed 3 = animation 1x
        animator.speed = MoveSpeed / 3f;

        // Prevent too slow or too fast
        animator.speed = Mathf.Clamp(animator.speed, 0.5f, 3f);
    }

    public void MoveHandle()
    {
        if (rb2d == null)
        {
            rb2d = GetComponent<Rigidbody2D>();
        }


        rb2d.velocity = transform.right * MoveSpeed;
    }

    private void OnBecameInvisible()
    {
        GM.fishInScreenList.Remove(this);
        Resetcolor(fishsprite);
        gameObject.SetActive(false);
    }

    private void OnBecameVisible()
    {
        GM.fishInScreenList.Add(this);
    }
}

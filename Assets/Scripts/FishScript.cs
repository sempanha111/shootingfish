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



    private GameManager GM;

    private void Start()
    {
        HpBackup = Hp;
        GM = GameManager.Instance;
        MoveHandle();
        fishsprite = this.GetComponent<SpriteRenderer>();
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
                GM.animatiorManager.PlayAnima(transform.position, 1, BulletId);
            }
            else
            {
                GM.DisplayTextManagerScript.Display(("+" + coinAmount).ToString(), transform.position);
                GM.coinManager.coinAnima(this, BulletId);
            }


            if (BulletId == 0)
            {
                GM.CalulateTotalCoinWithCoinFish(coinAmount * Bet);
            }
            else
            {
                GM.CalulateNPCCoinFish(coinAmount, BulletId);
            }


            GM.fishInScreenList.Remove(this);


            this.Hp = HpBackup;
            Resetcolor(fishsprite);
            gameObject.SetActive(false);
        }
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

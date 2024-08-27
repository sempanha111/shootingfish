using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FishScript : MonoBehaviour
{
    [SerializeField] public float Hp = 5f;
    [SerializeField] public float CoinFish;
    [SerializeField] public float MoveSpeed;

    private Rigidbody2D rb2d;



    private GameManager GM;

    private void Start()
    {
        GM = GameManager.Instance;
        MoveHandle();
    }

    private bool IsDead()
    {
        return Hp <= 0;
    }


    public void ResetFishColor(SpriteRenderer Fish, Color backUpColor)
    {
        StartCoroutine(IEnumResetFishColor(Fish, backUpColor));
    }

    private IEnumerator IEnumResetFishColor(SpriteRenderer Fish, Color backUpColor)
    {
        yield return new WaitForSeconds(0.1f);
        Fish.color = backUpColor;
    }

    public void TakeDamage(SpriteRenderer Fish, float Damage, int BulletId)
    {
        Hp -= Damage;
        FishSystem(Fish, BulletId);
    }

    private void FishSystem(SpriteRenderer Fish, int BulletId)
    {
        if (IsDead())
        {
            GM.DisplayTextManagerScript.Display(("+" + CoinFish).ToString(), transform.position);
            GM.coinManager.coinAnima(this, BulletId);

            if(BulletId == 0){
                GM.CalulateTotalCoinWithCoinFish(CoinFish);
            }
            else{
                GM.CalulateNPCCoinFish(CoinFish, BulletId);
            }

            Destroy(gameObject);
        }
    }



    void MoveHandle()
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
        Destroy(gameObject);
    }

    private void OnBecameVisible()
    {
        GM.fishInScreenList.Add(this);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FishScript : MonoBehaviour
{
    [SerializeField] public float Hp = 5f;
    [SerializeField] public int CoinFish;
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



    public void TakeDamage(SpriteRenderer Fish, float Damage)
    {
        Hp -= Damage;
        FishSystem(Fish);
    }

    private void FishSystem(SpriteRenderer Fish)
    {
        if (IsDead())
        {
            Fish.gameObject.SetActive(false);
            GM.DisplayTextManagerScript.Display(("+" + CoinFish).ToString(), transform);
            GM.coinManager.coinAnima(transform);
            GM.CalulateTotalCoinWithCoinFish(CoinFish);

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


}

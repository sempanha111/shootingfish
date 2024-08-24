using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class CoinManager : MonoBehaviour
{
    public GameObject coinprefab;
    public Transform coinparent;
    public Transform[] coinEnd;
    public float moveDuration;
    public Ease moveEase;

    public int CoinAmount;

    private GameManager GM;

    private FishScript fishScript;



    void Start()
    {
        GM = GameManager.Instance;
    }

    public void coinAnima(Transform Fishpos, int BulletId){



        fishScript = Fishpos.GetComponent<FishScript>();

        float fishcoin =fishScript.CoinFish / 2;

        float timeToLoop = CalculateTimeToLoop(fishcoin);

        for(int i = 0; i <= timeToLoop; i++){
            GameObject coinobject = Instantiate(coinprefab, coinparent);
            coinobject.SetActive(true);

            var offset =  new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f);
            coinobject.transform.position = offset + Fishpos.transform.position;

            coinobject.transform.localScale = Vector3.one * 0.01f;

            coinobject.transform.DOScale(Vector3.one * 0.15f, 0.25f).SetDelay(0.2f).OnComplete(()=>{
                coinobject.transform.DOScale(Vector3.one * 0.01f,3.5f);
            });

            coinobject.transform.DOMove(coinEnd[BulletId].position,moveDuration).SetEase(moveEase).SetDelay(Random.Range(0.8f, 1f)).OnComplete(() => {
                Destroy(coinobject);
            });   


        }   
    }


    private float CalculateTimeToLoop(float fishcoin)
    {
        float clampedFishcoin = Mathf.Clamp(fishcoin, 0.1f, 500f);

        return Mathf.Lerp(3, 30, (clampedFishcoin - 0.1f) / (500f - 0.1f));
    }


 
}

using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class CoinManager : MonoBehaviour
{
    public GameObject coinprefab;
    public Transform coinparent;
    public Transform coinEnd;
    public float moveDuration;
    public Ease moveEase;

    public int CoinAmount;

    private GameManager GM;

    void Start()
    {
        GM = GameManager.Instance;
    }

    public void coinAnima(Transform Fishpos){

        for(int i = 0; i <= CoinAmount; i++){
            GameObject coinobject = Instantiate(coinprefab, coinparent);
            coinobject.SetActive(true);

            var offset =  new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f);
            coinobject.transform.position = offset + Fishpos.transform.position;

            coinobject.transform.localScale = Vector3.one * 0.01f;

            coinobject.transform.DOScale(Vector3.one * 0.15f, 0.25f).SetDelay(0.2f).OnComplete(()=>{
                coinobject.transform.DOScale(Vector3.one * 0.01f,3.5f);
            });

            coinobject.transform.DOMove(coinEnd.position,moveDuration).SetEase(moveEase).SetDelay(Random.Range(0.8f, 1f)).OnComplete(() => {
                Destroy(coinobject);
            });   


        }   
    }


 
}

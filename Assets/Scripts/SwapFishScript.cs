using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwapFishScript : MonoBehaviour
{
    public GameObject[] fish;
    public Transform FishSpawnPosition;


    public Transform StartPos;


    void Start()
    {
        StartCoroutine(IEnumStartFish());
    }
    private IEnumerator IEnumStartFish()
    {
        yield return new WaitForSeconds(0.7f);

        while(true){
            
                Vector3 randomOffset = new Vector3(Random.Range(-2f, 2f),  Random.Range(-2f, 2f), 0f);

                GameObject spawnedFish = Instantiate(fish[Random.Range(0, 3)], FishSpawnPosition);
                spawnedFish.transform.position = StartPos.position;

                yield return new WaitForSeconds(3f);
            
        }
    }

}

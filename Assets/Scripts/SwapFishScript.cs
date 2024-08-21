using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwapFishScript : MonoBehaviour
{
    public GameObject[] fish;
    public Transform FishSpawnPosition;


    public Transform TopLeftPos;
    public Transform BottomLeftPos;
    public Transform TopRightPos;
    public Transform BottomRightPos;


    void Start()
    {
        StartCoroutine(IEnumStartFish_TOP_LEFT());
        StartCoroutine(IEnumStartFish_TOP_RIGHT());
        
    }

    int orderLayer;
    private IEnumerator IEnumStartFish_TOP_LEFT()
    {
        yield return new WaitForSeconds(0.7f);

        while (true)
        {

            Vector3 randomOffset = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), 0f);

            GameObject spawnedFish = Instantiate(fish[Random.Range(0, 3)], FishSpawnPosition);
            spawnedFish.transform.position = TopLeftPos.position + randomOffset;
            spawnedFish.SetActive(true);
            spawnedFish.GetComponent<SpriteRenderer>().sortingOrder = orderLayer;
            orderLayer++;
            
            RotateForFish(spawnedFish.transform, BottomRightPos);

            yield return new WaitForSeconds(Random.Range(2f, 4f));

        }
    }
    private IEnumerator IEnumStartFish_TOP_RIGHT()
    {
        yield return new WaitForSeconds(0.7f);

        while (true)
        {

            Vector3 randomOffset = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), 0f);

            GameObject spawnedFish = Instantiate(fish[Random.Range(0, 3)], FishSpawnPosition);
            spawnedFish.transform.position = TopRightPos.position + randomOffset;
            spawnedFish.SetActive(true);
            spawnedFish.GetComponent<SpriteRenderer>().sortingOrder = orderLayer;
            orderLayer++;
            
            RotateForFish(spawnedFish.transform, BottomLeftPos);

            yield return new WaitForSeconds(Random.Range(2f, 4f));

        }
    }



    private void RotateForFish(Transform spawnedFish, Transform EndPosition)
    {
        float angle = GetAngle(spawnedFish, EndPosition) + Random.Range(-5f,10f);
        spawnedFish.transform.rotation = Quaternion.Euler(0, 0, angle);
    }



    private float GetAngle(Transform spawnedFish, Transform EndPosition)
    {
        
        Vector3 worldPosition = EndPosition.position;
        worldPosition.z = 0;

        Vector3 direction = worldPosition - spawnedFish.position;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        return angle;
    }

}

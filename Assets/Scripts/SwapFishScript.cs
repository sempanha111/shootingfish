using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwapFishScript : MonoBehaviour
{
    public GameObject[] Fish;
    // private GameObject fish;
    public Transform FishSpawnPosition;

    public Transform TopLeftPos;
    public Transform BottomLeftPos;
    public Transform TopRightPos;
    public Transform BottomRightPos;



    void Start()
    {
        StartCoroutine(IEnumStartFish_TOP_LEFT());
        StartCoroutine(IEnumStartFish_TOP_RIGHT());
        StartCoroutine(IEnumStartFish_Bottom_RIGHT());
        StartCoroutine(IEnumStartFish_Bottom_LEFT());
        

        IEnumSmallFish();

    }

    int orderLayer;
    private IEnumerator IEnumStartFish_TOP_LEFT()
    {
        yield return new WaitForSeconds(0.7f);

        while (true)
        {

            Vector3 randomOffset = new Vector3(0f, Random.Range(-5f, 5f), 0f);

            GameObject spawnedFish = Instantiate(Fish[GetFish()], FishSpawnPosition);
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
        while (true)
        {
            Vector3 randomOffset = new Vector3(0f, Random.Range(-5f, 5f), 0f);
            int fishIndex = GetFish();
            GameObject spawnedFish = Instantiate(Fish[fishIndex], TopRightPos.position + randomOffset, Quaternion.identity, FishSpawnPosition);
            RotateForFish(spawnedFish.transform, BottomLeftPos);

            spawnedFish.SetActive(true);
            SpriteRenderer fishSprite = spawnedFish.GetComponent<SpriteRenderer>();
            fishSprite.sortingOrder = orderLayer;

            if(fishIndex == 7 || fishIndex == 14){
                fishSprite.flipY = true;
            }
            
            orderLayer++;



            yield return new WaitForSeconds(Random.Range(1f, 4f));

        }
    }
    private IEnumerator IEnumStartFish_Bottom_RIGHT()
    {
        while (true)
        {
            int fishIndex = GetFish();
            Vector3 randomOffset = new Vector3(0f, Random.Range(-5f, 5f), 0f);
            
            GameObject spawnedFish = Instantiate(Fish[fishIndex], BottomRightPos.position + randomOffset, Quaternion.identity, FishSpawnPosition);
            RotateForFish(spawnedFish.transform, TopLeftPos);

            spawnedFish.SetActive(true);

            SpriteRenderer fishSprite = spawnedFish.GetComponent<SpriteRenderer>();

            fishSprite.sortingOrder = orderLayer;

            if(fishIndex == 7 || fishIndex == 14){
                fishSprite.flipY = true;
            }

            orderLayer++;

            yield return new WaitForSeconds(Random.Range(2f, 5f));

        }
    }
    private IEnumerator IEnumStartFish_Bottom_LEFT()
    {
        while (true)
        {
            int fishIndex = GetFish();
            Vector3 randomOffset = new Vector3(0f, Random.Range(-5f, 5f), 0f);
            
            GameObject spawnedFish = Instantiate(Fish[fishIndex], BottomLeftPos.position + randomOffset, Quaternion.identity, FishSpawnPosition);
            RotateForFish(spawnedFish.transform, TopRightPos);

            spawnedFish.SetActive(true);

            SpriteRenderer fishSprite = spawnedFish.GetComponent<SpriteRenderer>();

            fishSprite.sortingOrder = orderLayer;

            orderLayer++;

            yield return new WaitForSeconds(Random.Range(1f, 5f));

        }
    }
    private IEnumerator IEnumSmallFish()
    {
        
        while (true)
        {
            int fishIndex = GetSmallFish();
            var (StartPos, EndPos) = GeneratePos();
            
            GameObject spawnedFish = Instantiate(Fish[fishIndex], StartPos.position, Quaternion.identity, FishSpawnPosition);
            RotateForFish(spawnedFish.transform, EndPos);

            spawnedFish.SetActive(true);

            SpriteRenderer fishSprite = spawnedFish.GetComponent<SpriteRenderer>();

            fishSprite.sortingOrder = orderLayer;

            orderLayer++;

            yield return new WaitForSeconds(Random.Range(1f, 2f));

        }
    }


    private int GetFish()
    {

        int percent = Random.Range(1, 100);
        int FishLength = Fish.Length;
        int FishIndex;

        if (percent >= 90) //10%
        {
            FishIndex = Random.Range(8,10);
        }
        else if (percent >= 70) //20%
        {
            FishIndex = Random.Range(6,FishLength);
        }
        else //70%
        {
            FishIndex = Random.Range(1,5);
        }

        return FishIndex;
    }
    private int GetSmallFish()
    {

        int percent = Random.Range(1, 100);
        int FishLength = Fish.Length;
        int FishIndex;

        if (percent >= 90) //10%
        {
            FishIndex = 1;
        }
        else if (percent >= 70) //20%
        {
            FishIndex = Random.Range(1,2);
        }
        else //70%
        {
            FishIndex = Random.Range(1,4);
        }

        return FishIndex;
    }

    float SpawnNextTime = 70f;
    void LateUpdate()
    {           
        if(Time.time >= SpawnNextTime){
            var (startPos, endPos) = GeneratePos();
            Boos(startPos,endPos);
            SpawnNextTime = Time.time +70f;
        }
    }


    private (Transform StartPos, Transform EndPos) GeneratePos(){
        int Pos = Random.Range(1, 4);
        Transform StartPos = null;
        Transform EndPos = null;

        if(Pos == 1){
            StartPos = TopLeftPos;
            EndPos = BottomRightPos;
        }
        else if(Pos == 2){
            StartPos = TopRightPos;
            EndPos = BottomLeftPos;
        }
        else if(Pos == 3){
            StartPos = BottomLeftPos;
            EndPos = TopRightPos;
        }
        else{
            StartPos = BottomRightPos;
            EndPos = TopLeftPos;
        }

        return (StartPos, EndPos);
    }


    private void Boos(Transform StartPos, Transform EndPos){
        Vector3 randomOffset = new Vector3(0f, Random.Range(-5f, 5f), 0f);
        
        GameObject spawnedFish = Instantiate(Fish[0], StartPos.position + randomOffset, Quaternion.identity, FishSpawnPosition);     

        RotateForFish(spawnedFish.transform, EndPos);
        spawnedFish.SetActive(true);
        SpriteRenderer fishSprite = spawnedFish.GetComponent<SpriteRenderer>();

        fishSprite.sortingOrder = orderLayer + 100;
        
    }


    private void RotateForFish(Transform spawnedFish, Transform EndPosition)
    {
        float angle = GetAngle(spawnedFish, EndPosition) + Random.Range(-1f, 10f);
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


using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwapFishScript : MonoBehaviour
{
    public GameObject[] Fish;
    public Transform FishSpawnPosition;

    public Transform LeftPos;
    public Transform RightPos;

    int orderLayer;
    float SpawnNextTime = 70f;
    void Start()
    {

        StartCoroutine(IEnumFish_LEFT_Generate());
        StartCoroutine(IEnumFish_RIGHT_Generate());


    }








    void LateUpdate()
    {
        if (Time.time >= SpawnNextTime)  //Time for Boos Spawn and Delay Time 70f
        {
            var (startPos, endPos) = GeneratePos();
            Boos(startPos, endPos);
            SpawnNextTime = Time.time + 70f;
        }
    }














    //Generate Random Function All//
    private (Transform StartPos, Transform EndPos) GeneratePos() //Function to Random position Left-->Right or Right-->Left
    {
        int Pos = Random.Range(1, 2);
        Transform StartPos = null;
        Transform EndPos = null;

        if (Pos == 1)
        {
            StartPos = LeftPos;
            EndPos = RightPos;
        }
        else
        {
            StartPos = RightPos;
            EndPos = LeftPos;
        }

        return (StartPos, EndPos);
    }
    private float GetAngle(Transform spawnedFish, Transform EndPosition)
    {
        Vector3 worldPosition = EndPosition.position;

        worldPosition.z = 0;

        Vector3 direction = worldPosition - spawnedFish.position;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        return angle;
    }
    private void RotateForFish(Transform spawnedFish, Transform EndPosition, int AngleRandom)
    {
        float angle = GetAngle(spawnedFish, EndPosition);
        spawnedFish.transform.rotation = Quaternion.Euler(0, 0, angle + AngleRandom);
    }
    private int GetFishGenerate() //Function to Random General Fish by % of Fish
    {

        int percent = Random.Range(1, 100);
        int FishLength = Fish.Length;
        int FishIndex;

        if (percent >= 90) //10%
        {
            FishIndex = Random.Range(9, 12);
        }
        else if (percent >= 70) //20%
        {
            FishIndex = Random.Range(6, FishLength);
        }
        else //70%
        {
            FishIndex = Random.Range(1, 5);
        }

        return FishIndex;
    }
    private void Boos(Transform StartPos, Transform EndPos)//Generate Start Position Boos​ and Generate End Position
    {
        Vector3 randomOffset = new Vector3(0f, Random.Range(-5f, 5f), 0f);
        GameObject spawnedFish = Instantiate(Fish[0], StartPos.position + randomOffset, Quaternion.identity, FishSpawnPosition);

        RotateForFish(spawnedFish.transform, EndPos, Random.Range(-5, 5));

        spawnedFish.SetActive(true);

        SpriteRenderer fishSprite = spawnedFish.GetComponent<SpriteRenderer>();

        fishSprite.sortingOrder = orderLayer + 100;

    }

    // private IEnumerator IEnumFish_LEFT_Generate() //Function SWap General Fish  From Left Only
    // {
    //     yield return new WaitForSeconds(0.7f);

    //     while (true)
    //     {
    //         Vector3 randomOffset = new Vector3(0f, Random.Range(-5f, 5f), 0f);

    //         GameObject spawnedFish = Instantiate(Fish[GetFishGenerate()], LeftPos.position + randomOffset, Quaternion.identity, FishSpawnPosition);
    //         RotateForFish(spawnedFish.transform, RightPos, Random.Range(-5, 5));
    //         spawnedFish.SetActive(true);

    //         SpriteRenderer SpriteFish = spawnedFish.GetComponent<SpriteRenderer>();
    //         SpriteFish.sortingOrder = orderLayer;
    //         orderLayer++;

    //         yield return new WaitForSeconds(Random.Range(1f, 4f));

    //     }
    // }
    private IEnumerator IEnumFish_LEFT_Generate() //Function SWap General Fish  From Right Only
    {
        yield return new WaitForSeconds(0.7f);

        while (true)
        {
            int FishId = GetFishGenerate();
            Vector3 randomOffset = new Vector3(0f, Random.Range(-8f, 8f), 0f);

            GameObject spawnedFish = Instantiate(Fish[FishId], LeftPos.position + randomOffset, Quaternion.identity, FishSpawnPosition);
            RotateForFish(spawnedFish.transform, RightPos, Random.Range(-8, 8));
            spawnedFish.SetActive(true);
            SpriteRenderer SpriteFish = spawnedFish.GetComponent<SpriteRenderer>();
            SpriteFish.sortingOrder = orderLayer;
            orderLayer++;

            if (FishId == 1 || FishId == Random.Range(2,4))
            {
                int randomValue = Random.Range(1, 4);
                if (randomValue == 2 || randomValue == 3 || randomValue == 4)
                {
                    for (int i = 0; i <= Random.Range(2, 4); i++)
                    {
                        spawnedFish = Instantiate(Fish[FishId], LeftPos.position + new Vector3(Random.Range(-1f, 2f), Random.Range(-1f, i), 0f), Quaternion.identity, FishSpawnPosition);
                        RotateForFish(spawnedFish.transform, RightPos, Random.Range(-2, 2));
                        SpriteFish = spawnedFish.GetComponent<SpriteRenderer>();
                        SpriteFish.sortingOrder = orderLayer;
                        orderLayer++;
                    }
                }
            }

            

            yield return new WaitForSeconds(Random.Range(1f, 4f));

        }
    }
    private IEnumerator IEnumFish_RIGHT_Generate() //Function SWap General Fish  From Right Only
    {
        yield return new WaitForSeconds(0.7f);

        while (true)
        {
            int FishId = GetFishGenerate();
            Vector3 randomOffset = new Vector3(0f, Random.Range(-8f, 8f), 0f);

            GameObject spawnedFish = Instantiate(Fish[FishId], RightPos.position + randomOffset, Quaternion.identity, FishSpawnPosition);
            RotateForFish(spawnedFish.transform, LeftPos, Random.Range(-8, 8));
            spawnedFish.SetActive(true);
            SpriteRenderer SpriteFish = spawnedFish.GetComponent<SpriteRenderer>();
            SpriteFish.sortingOrder = orderLayer;
            orderLayer++;   

            if (FishId == 2 || FishId == 3)
            {
                int randomValue = Random.Range(1, 4);
                if (randomValue == 2 || randomValue == 3 || randomValue == 4)
                {
                    for (int i = 0; i <= Random.Range(2, 5); i++)
                    {
                        spawnedFish = Instantiate(Fish[FishId], RightPos.position + new Vector3(Random.Range(-1f, 2f), Random.Range(-1f, i), 0f), Quaternion.identity, FishSpawnPosition);
                        RotateForFish(spawnedFish.transform, LeftPos, Random.Range(-2, 2));
                        SpriteFish = spawnedFish.GetComponent<SpriteRenderer>();
                        SpriteFish.sortingOrder = orderLayer;
                        orderLayer++;   
                    }
                }
            }

        
            if (FishId == 9 || FishId == 14)
            {
                SpriteFish.flipY = true;
            }

            yield return new WaitForSeconds(Random.Range(1f, 4f));

        }
    }

}


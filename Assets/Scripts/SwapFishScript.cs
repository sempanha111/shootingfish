using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SwapFishScript : MonoBehaviour
{
    public GameObject[] Fish;
    public Transform FishSpawnPosition;

    public Transform LeftPos;
    public Transform RightPos;

    int orderLayer;
    float SpawnNextTime = 3f;


    private List<GameObject> fishpolling = new List<GameObject>();


    void Start()
    {

        StartCoroutine(IEnumFish_RIGHT_Generate_Pulling());
        StartCoroutine(IEnumFish_LEFT_Generate_Pulling());


    }








    void LateUpdate()
    {
        if (Time.time >= SpawnNextTime)  //Time for Boos Spawn and Delay Time 70f
        {
            var (startPos, endPos) = GeneratePos();
            Boos(startPos, endPos);
            SpawnNextTime = Time.time + 5f;
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
    private IEnumerator IEnumFish_RIGHT_Generate_Pulling() //Function SWap General Fish  From Right Only
    {
        // yield return new WaitForSeconds(0.7f);

        while (true)
        {
            int FishId = GetFishGenerate();

            Vector3 randomOffset = new Vector3(0f, Random.Range(-8f, 8f), 0f);



            GameObject FishObject = fishpolling.FirstOrDefault(o => !o.activeSelf && o.name == Fish[FishId].name + "(Clone)");

            if (FishObject == null)
            {
                GameObject spawnedFish = Instantiate(Fish[FishId], FishSpawnPosition);
                fishpolling.Add(spawnedFish);
                FishObject = spawnedFish;
            }


            FishScript fishScript = FishObject.GetComponent<FishScript>();

            FishObject.transform.position = LeftPos.position + randomOffset;

            RotateForFish(FishObject.transform, RightPos, Random.Range(-8, 8));

            FishObject.SetActive(true);
            fishScript.MoveHandle();


            SpriteRenderer SpriteFish = FishObject.GetComponent<SpriteRenderer>();
            SpriteFish.sortingOrder = orderLayer;
            orderLayer += 2;

            if (FishId == 2 || FishId == 3)
            {
                int randomValue = Random.Range(1, 4);
                if (randomValue == 2 || randomValue == 3 || randomValue == 4)
                {
                    for (int i = 0; i <= Random.Range(2, 5); i++)
                    {

                        FishObject = fishpolling.FirstOrDefault(o => !o.activeSelf && o.name == Fish[FishId].name + "(Clone)");
                        if (FishObject == null)
                        {
                            GameObject spawnedFish = Instantiate(Fish[FishId], FishSpawnPosition);
                            fishpolling.Add(spawnedFish);
                            FishObject = spawnedFish;
                        }
                        FishObject.transform.position = LeftPos.position + new Vector3(Random.Range(-1f, 2f), Random.Range(-1f, i), 0f);

                        RotateForFish(FishObject.transform, RightPos, Random.Range(-2, 2));
                        SpriteFish = FishObject.GetComponent<SpriteRenderer>();
                        SpriteFish.sortingOrder = orderLayer;
                        orderLayer += 2;
                    }
                }
            }


            if (FishId == 9 || FishId == 14)
            {
                SpriteFish.flipY = true;
            }

            yield return new WaitForSeconds(Random.Range(1f, 2.1f));

        }
    }
    private IEnumerator IEnumFish_LEFT_Generate_Pulling() //Function SWap General Fish  From Left Only
    {
        // yield return new WaitForSeconds(0.7f);

        while (true)
        {
            int FishId = GetFishGenerate();

            Vector3 randomOffset = new Vector3(0f, Random.Range(-8f, 8f), 0f);



            GameObject FishObject = fishpolling.FirstOrDefault(o => !o.activeSelf && o.name == Fish[FishId].name + "(Clone)");

            if (FishObject == null)
            {
                GameObject spawnedFish = Instantiate(Fish[FishId], FishSpawnPosition);
                fishpolling.Add(spawnedFish);
                FishObject = spawnedFish;
            }


            FishScript fishScript = FishObject.GetComponent<FishScript>();

            FishObject.transform.position = RightPos.position + randomOffset;

            RotateForFish(FishObject.transform, LeftPos, Random.Range(-8, 8));

            FishObject.SetActive(true);
            fishScript.MoveHandle();


            SpriteRenderer SpriteFish = FishObject.GetComponent<SpriteRenderer>();
            SpriteFish.sortingOrder = orderLayer;
            orderLayer += 2;

            if (FishId == 1 || FishId == Random.Range(2,4))
            {
                int randomValue = Random.Range(1, 4);
                if (randomValue == 2 || randomValue == 3 || randomValue == 4)
                {
                    for (int i = 0; i <= Random.Range(2, 4); i++)
                    {

                        FishObject = fishpolling.FirstOrDefault(o => !o.activeSelf && o.name == Fish[FishId].name + "(Clone)");
                        if (FishObject == null)
                        {
                            GameObject spawnedFish = Instantiate(Fish[FishId], FishSpawnPosition);
                            fishpolling.Add(spawnedFish);
                            FishObject = spawnedFish;
                        }
                        FishObject.transform.position = RightPos.position + new Vector3(Random.Range(-1f, 2f), Random.Range(-1f, i), 0f);

                        RotateForFish(FishObject.transform, LeftPos, Random.Range(-2, 2));
                        SpriteFish = FishObject.GetComponent<SpriteRenderer>();
                        SpriteFish.sortingOrder = orderLayer;
                        orderLayer += 2;
                    }
                }
            }


            if (FishId == 9 || FishId == 14)
            {
                SpriteFish.flipY = true;
            }

            yield return new WaitForSeconds(Random.Range(1f, 2.1f));

        }
    }



}


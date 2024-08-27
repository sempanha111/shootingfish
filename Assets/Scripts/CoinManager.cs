using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEditor.Rendering;
using System.Diagnostics;

public class CoinManager : MonoBehaviour
{
    public GameObject coinprefab;
    public Transform coinparent;
    public Transform[] coinEnd;
    public float moveDuration;
    public AnimationCurve moveEase;

    private GameManager GM;


    private List<GameObject> coinPooling = new List<GameObject>();

    void Start()
    {
        GM = GameManager.Instance;
    }

    public void coinAnima(FishScript fish, int BulletId)
    {
        StartCoroutine(IEnumCoinAnima(fish, BulletId));
    }

    private IEnumerator IEnumCoinAnima(FishScript fish, int BulletId)
    {
        var fishScript = fish;
        float fishcoin = fishScript.CoinFish;
        Vector3 fishPos = fish.transform.position;
        float timeToLoop = CalculateTimeToLoop(fishcoin);
        // float timeToLoop = 5;

        Vector3[] squareOffsets = new Vector3[] { };

        switch ((int)timeToLoop)
        {
            case 1:
                squareOffsets = new Vector3[]
                {
            new Vector3(0f, 0f, 0f) // Center
                };
                break;
            case 2:
                squareOffsets = new Vector3[]
                {
            new Vector3(-0.25f, -0.25f, 0f), // Bottom-left
            new Vector3(0.25f, -0.25f, 0f)   // Bottom-right
                };
                break;
            case 3:
                squareOffsets = new Vector3[]
                {
            new Vector3(-0.25f, -0.25f, 0f), // Bottom-left
            new Vector3(0.25f, -0.25f, 0f),  // Bottom-right
            new Vector3(0f, 0.25f, 0f)       // Top-center
                };
                break;
            case 4:
                squareOffsets = new Vector3[]
                {
            new Vector3(-0.25f, -0.25f, 0f), // Bottom-left
            new Vector3(0.25f, -0.25f, 0f),  // Bottom-right
            new Vector3(-0.25f, 0.25f, 0f),  // Top-left
            new Vector3(0.25f, 0.25f, 0f)    // Top-right
                };
                break;
            case 5:
                squareOffsets = new Vector3[]
                {
            new Vector3(-0.25f, -0.25f, 0f), // Bottom-left
            new Vector3(0.25f, -0.25f, 0f),  // Bottom-right
            new Vector3(-0.25f, 0.25f, 0f),  // Top-left
            new Vector3(0.25f, 0.25f, 0f),   // Top-right
            new Vector3(0f, 0f, 0f)          // Center
                };
                break;
            case 6:
                squareOffsets = new Vector3[]
                {
            new Vector3(-0.25f, -0.25f, 0f), // Bottom-left
            new Vector3(0.25f, -0.25f, 0f),  // Bottom-right
            new Vector3(-0.25f, 0.25f, 0f),  // Top-left
            new Vector3(0.25f, 0.25f, 0f),   // Top-right
            new Vector3(0f, 0f, 0f),         // Center
            new Vector3(0f, -0.5f, 0f)       // Bottom-center
                };
                break;
            case 7:
                squareOffsets = new Vector3[]
                {
            new Vector3(-0.25f, -0.25f, 0f), // Bottom-left
            new Vector3(0.25f, -0.25f, 0f),  // Bottom-right
            new Vector3(-0.25f, 0.25f, 0f),  // Top-left
            new Vector3(0.25f, 0.25f, 0f),   // Top-right
            new Vector3(0f, 0f, 0f),         // Center
            new Vector3(0f, -0.5f, 0f),      // Bottom-center
            new Vector3(0f, 0.5f, 0f)        // Top-center
                };
                break;
            case 8:
                squareOffsets = new Vector3[]
                {
            new Vector3(-0.25f, -0.25f, 0f), // Bottom-left
            new Vector3(0.25f, -0.25f, 0f),  // Bottom-right
            new Vector3(-0.25f, 0.25f, 0f),  // Top-left
            new Vector3(0.25f, 0.25f, 0f),   // Top-right
            new Vector3(0f, 0f, 0f),         // Center
            new Vector3(0f, -0.5f, 0f),      // Bottom-center
            new Vector3(0f, 0.5f, 0f),       // Top-center
            new Vector3(-0.5f, 0f, 0f)       // Left-center
                };
                break;
            case 9:
                squareOffsets = new Vector3[]
                {
            new Vector3(-0.25f, -0.25f, 0f), // Bottom-left
            new Vector3(0.25f, -0.25f, 0f),  // Bottom-right
            new Vector3(-0.25f, 0.25f, 0f),  // Top-left
            new Vector3(0.25f, 0.25f, 0f),   // Top-right
            new Vector3(0f, 0f, 0f),         // Center
            new Vector3(0f, -0.5f, 0f),      // Bottom-center
            new Vector3(0f, 0.5f, 0f),       // Top-center
            new Vector3(-0.5f, 0f, 0f),      // Left-center
            new Vector3(0.5f, 0f, 0f)        // Right-center
                };
                break;
            case 10:
                squareOffsets = new Vector3[]
                {
            new Vector3(-0.25f, -0.25f, 0f), // Bottom-left
            new Vector3(0.25f, -0.25f, 0f),  // Bottom-right
            new Vector3(-0.25f, 0.25f, 0f),  // Top-left
            new Vector3(0.25f, 0.25f, 0f),   // Top-right
            new Vector3(0f, 0f, 0f),         // Center
            new Vector3(0f, -0.5f, 0f),      // Bottom-center
            new Vector3(0f, 0.5f, 0f),       // Top-center
            new Vector3(-0.5f, 0f, 0f),      // Left-center
            new Vector3(0.5f, 0f, 0f),       // Right-center
            new Vector3(-0.5f, -0.5f, 0f)    // Bottom-left corner
                };
                break;
            default:
                // Default case for more than 10, reusing 4-point square layout
                squareOffsets = new Vector3[]
                {
            new Vector3(-0.25f, -0.25f, 0f), // Bottom-left
            new Vector3(0.25f, -0.25f, 0f),  // Bottom-right
            new Vector3(-0.25f, 0.25f, 0f),  // Top-left
            new Vector3(0.25f, 0.25f, 0f)    // Top-right
                };
                break;
        }



        for (int i = 0; i <= timeToLoop; i++)
        {

            GameObject coinobject = coinPooling.FirstOrDefault(o => !o.activeSelf);//

            if (coinobject == null)
            {
                coinobject = Instantiate(coinprefab, coinparent);
                coinPooling.Add(coinobject);
            }

            if (BulletId >= 1)
            {
                SpriteRenderer coinsprite = coinobject.GetComponent<SpriteRenderer>();
                Color col = coinsprite.color;
                col.a = 0.7f;
                coinsprite.color = col;
            }

            int index = (i + 1) % squareOffsets.Length;

            Vector3 offset = squareOffsets[index];
            coinobject.transform.position = offset + fishPos;


            StartCoroutine(Scale(coinobject.transform, coinEnd[BulletId]));

            MoveObject(coinobject, coinEnd[BulletId], 1f);
            coinobject.SetActive(true);
        }



        yield return new WaitForSeconds(0.01f);
    }

    private float CalculateTimeToLoop(float fishcoin)
    {
        // Clamp the fishcoin value to ensure it's within the expected range
        float clampedFishcoin = Mathf.Clamp(fishcoin, 0.1f, 500f);

        // Define the input range and the corresponding output range
        float inputMin = 0.1f;
        float inputMax = 500f;
        float outputMin = 2f;  // Desired minimum value for timeToLoop
        float outputMax = 40f; // Desired maximum value for timeToLoop

        // Calculate the normalized value of fishcoin in the input range
        float normalizedFishcoin = (clampedFishcoin - inputMin) / (inputMax - inputMin);

        // Interpolate between outputMin and outputMax based on the normalized value
        float timeToLoop = Mathf.Lerp(outputMin, outputMax, normalizedFishcoin);

        return timeToLoop;
    }


    public void MoveObject(GameObject obj, Transform targetEnd, float delay)
    {
        StartCoroutine(MoveCoroutine(obj, targetEnd, delay));
    }

    private IEnumerator MoveCoroutine(GameObject obj, Transform targetEnd, float delay)
    {
        yield return new WaitForSeconds(delay);

        float speed = 15f; 
        Vector3 startScale = new Vector3(0.1f, 0.1f, 0);
        Vector3 endScale = Vector3.zero; 
        float initialDistance = Vector2.Distance(obj.transform.position, targetEnd.position); // Initial distance to the target
        float dist = initialDistance;

        while (dist > 0.01f)
        {
            obj.transform.position = Vector2.MoveTowards(obj.transform.position, targetEnd.position, Time.deltaTime * speed);

            dist = Vector2.Distance(obj.transform.position, targetEnd.position);

            float scaleFactor = dist / initialDistance; 
            obj.transform.localScale = Vector3.Lerp(endScale, startScale, scaleFactor);

            yield return null; 
        }

        if (obj != null)
        {
            obj.SetActive(false);
            obj.transform.position = targetEnd.position;
            SpriteRenderer coinsprite = obj.GetComponent<SpriteRenderer>();
            Color col = coinsprite.color;
            col.a = 1;
            coinsprite.color = col;
            obj.transform.localScale = new Vector3(0.1f, 0.1f, 0);
        }
    }


    //pop up function
    private IEnumerator Scale(Transform transform, Transform End)
    {
        float duration = 0.1f;
        float time = 0;



        Vector3 startScale = transform.localScale;
        Vector3 targetScale = Vector3.one * 0.12f;
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + new Vector3(0, 0.3f, 0);

        while (time < duration)
        {
            transform.localScale = Vector3.Lerp(startScale, targetScale, time / duration);
            transform.position = Vector3.Lerp(startPosition, targetPosition, time / duration);

            time += Time.deltaTime;
            yield return null;
        }

        time = 0;

        while (time < duration)
        {
            transform.localScale = Vector3.Lerp(targetScale, startScale, time / duration);
            transform.position = Vector3.Lerp(targetPosition, startPosition, time / duration);

            time += Time.deltaTime;
            yield return null;
        }
    }






}

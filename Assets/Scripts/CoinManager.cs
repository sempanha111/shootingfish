using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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

        Vector3[] diamondOffsets = new Vector3[] { };

        float basePadding = 0.18f; // Base padding for small numbers
        float paddingIncrement = 0.1f; // Smaller increment to space out coins more subtly

        // Calculate the actual padding dynamically based on the number of coins
        float actualPadding = basePadding + ((int)timeToLoop - 1) * paddingIncrement;

        switch ((int)timeToLoop)
        {
            case 1:
                diamondOffsets = new Vector3[]
                {
            new Vector3(0f, 0f, 0f) // Center
                };
                break;
            case 2:
                diamondOffsets = new Vector3[]
                {
            new Vector3(-actualPadding, 0f, 0f),  // Left
            new Vector3(actualPadding, 0f, 0f)    // Right
                };
                break;
            case 3:
                diamondOffsets = new Vector3[]
                {
            new Vector3(0f, actualPadding, 0f),   // Top
            new Vector3(-actualPadding, -actualPadding, 0f), // Bottom-left
            new Vector3(actualPadding, -actualPadding, 0f)   // Bottom-right
                };
                break;
            case 4:
                diamondOffsets = new Vector3[]
                {
            new Vector3(-actualPadding, 0f, 0f),  // Left
            new Vector3(actualPadding, 0f, 0f),   // Right
            new Vector3(0f, actualPadding, 0f),   // Top
            new Vector3(0f, -actualPadding, 0f)   // Bottom
                };
                break;
            case 5:
                diamondOffsets = new Vector3[]
                {
            new Vector3(-actualPadding, 0f, 0f),  // Left
            new Vector3(actualPadding, 0f, 0f),   // Right
            new Vector3(0f, actualPadding, 0f),   // Top
            new Vector3(0f, -actualPadding, 0f),  // Bottom
            new Vector3(0f, 0f, 0f)               // Center
                };
                break;
            case 6:
                diamondOffsets = new Vector3[]
                {
            new Vector3(-actualPadding, 0f, 0f),     // Left
            new Vector3(actualPadding, 0f, 0f),      // Right
            new Vector3(0f, actualPadding, 0f),      // Top
            new Vector3(0f, -actualPadding, 0f),     // Bottom
            new Vector3(-actualPadding * 0.5f, actualPadding * 0.5f, 0f), // Top-left
            new Vector3(actualPadding * 0.5f, -actualPadding * 0.5f, 0f)  // Bottom-right
                };
                break;
            default:
                // For 7 or more coins, distribute them in a circular pattern
                List<Vector3> offsets = new List<Vector3>();

                // Calculate an angle step based on the number of coins
                float angleStep = 360f / timeToLoop;
                for (int i = 0; i < timeToLoop; i++)
                {
                    float angle = i * angleStep * Mathf.Deg2Rad; // Convert degrees to radians
                    float x = Mathf.Cos(angle) * actualPadding;
                    float y = Mathf.Sin(angle) * actualPadding;
                    offsets.Add(new Vector3(x, y, 0f));
                }
                diamondOffsets = offsets.ToArray();
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

            int index = (i + 1) % diamondOffsets.Length;

            Vector3 offset = diamondOffsets[index];
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

        // Define key points for fishcoin and their corresponding timeToLoop values
        float[] fishcoinPoints = { 0.1f, 0.3f, 1f, 2f, 5f, 10f, 50f, 100f, 500f };
        float[] timeToLoopPoints = { 1f, 1f, 2f, 3f, 5f, 7f, 10f, 15f, 20f };

        // Find the correct interval and interpolate
        for (int i = 0; i < fishcoinPoints.Length - 1; i++)
        {
            if (clampedFishcoin >= fishcoinPoints[i] && clampedFishcoin <= fishcoinPoints[i + 1])
            {
                // Linear interpolation between the two key points
                float t = (clampedFishcoin - fishcoinPoints[i]) / (fishcoinPoints[i + 1] - fishcoinPoints[i]);
                return Mathf.Lerp(timeToLoopPoints[i], timeToLoopPoints[i + 1], t);
            }
        }

        // Fallback in case of unexpected input, though clamping should prevent this
        return timeToLoopPoints[timeToLoopPoints.Length - 1];
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

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DisplayTextManagerScript : MonoBehaviour
{
    public Camera mainCamera;
    public TextMeshProUGUI textPrefab;
    public Transform TextHolder;

    public void Display(string st, Transform pos)
    {
        StartCoroutine(IEnumDisplay(st, pos));
    }

    private IEnumerator IEnumDisplay(string st, Transform pos)
    {
        yield return new WaitForSeconds(0.5f);

        var Text = Instantiate(textPrefab, TextHolder);
        Text.text = st;
        Text.transform.position = pos.position;//set position spawn text to position (pos==pos fish)
        Text.gameObject.SetActive(true);

        StartCoroutine(ScaleText(Text.transform));
        yield return new WaitForSeconds(1f);
        StartCoroutine(IEnumResetDisplayText(Text));
    }

    private IEnumerator IEnumResetDisplayText(TextMeshProUGUI text)
    {

        float duration = 0.2f;
        float time = 0;

        Color originalColor = text.color;
        Color targetColor = new Color(originalColor.r, originalColor.g, originalColor.b, 0); 

        while (time < duration)
        {
            text.color = Color.Lerp(originalColor, targetColor, time / duration);

            time += Time.deltaTime;
            yield return null;
        }

        text.color = targetColor;

        yield return new WaitForSeconds(0.5f);

        Destroy(text.gameObject);
    }

    private IEnumerator ScaleText(Transform textTransform)
    {
        float duration = 0.25f, time = 0;
        Vector3 startScale = Vector3.zero;
        Vector3 targetScale = Vector3.one;

        Vector3 startPosition = textTransform.localPosition;
        Vector3 targetPosition = startPosition + new Vector3(0, 90, 0); // Target position, moved up by 10 units on the y-axis



        while (time < duration)
        {
            textTransform.localScale = Vector3.Lerp(startScale, targetScale, time / duration);

            textTransform.localPosition = Vector3.Lerp(startPosition, targetPosition, time / duration);

            time += Time.deltaTime;
            yield return null;
        }
    }


}
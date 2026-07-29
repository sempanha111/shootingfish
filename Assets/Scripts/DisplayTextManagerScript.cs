using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DisplayTextManagerScript : MonoBehaviour
{
    public Camera mainCamera;
    public TextMeshProUGUI textPrefab;
    public Transform TextHolder;
    
    // NEW: Pool for floating text
    private List<TextMeshProUGUI> textPool = new List<TextMeshProUGUI>();

    public void Display(string st, Vector3 pos)
    {
        StartCoroutine(IEnumDisplay(st, pos));
    }

    private IEnumerator IEnumDisplay(string st, Vector3 pos)
    {
        yield return new WaitForSeconds(0.5f);

        // Recycle old text instead of Instantiating new ones!
        TextMeshProUGUI activeText = textPool.FirstOrDefault(t => !t.gameObject.activeSelf);
        if (activeText == null)
        {
            activeText = Instantiate(textPrefab, pos, Quaternion.identity, TextHolder);
            textPool.Add(activeText);
        }

        // Reset text properties
        activeText.transform.position = pos;
        activeText.text = st;
        activeText.color = new Color(activeText.color.r, activeText.color.g, activeText.color.b, 1f); 
        activeText.gameObject.SetActive(true);

        StartCoroutine(ScaleText(activeText.transform));

        yield return new WaitForSeconds(1f);
        StartCoroutine(IEnumResetDisplayText(activeText));
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

        // Put it to sleep instead of Destroying it!
        text.gameObject.SetActive(false);
    }

    private IEnumerator ScaleText(Transform textTransform)
    {
        float duration = 0.25f, time = 0;
        Vector3 startScale = Vector3.zero;
        Vector3 targetScale = Vector3.one;

        Vector3 startPosition = textTransform.localPosition;
        Vector3 targetPosition = startPosition + new Vector3(0, 90, 0);

        while (time < duration)
        {
            textTransform.localScale = Vector3.Lerp(startScale, targetScale, time / duration);
            textTransform.localPosition = Vector3.Lerp(startPosition, targetPosition, time / duration);
            time += Time.deltaTime;
            yield return null;
        }
    }
}
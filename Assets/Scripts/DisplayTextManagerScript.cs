using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DisplayTextManagerScript : MonoBehaviour
{
    public Camera mainCamera;
    public Text textPrefab;
    public Transform TextHolder;

    public void Display(string st,Transform pos)
    {
        var Text = Instantiate(textPrefab,TextHolder);
        Text.text = st;
        Text.transform.position = GetWorldPosition(pos);
        
        Text.gameObject.SetActive(true);
        StartCoroutine(IEnumResetDisplayText(Text));
    }

    //Convert Text Position To Fish Position
    public Vector3 GetWorldPosition(Transform target)
    {
        return mainCamera.WorldToScreenPoint(target.position);
    }


    private IEnumerator IEnumResetDisplayText(Text text){
        yield return new WaitForSeconds(0.5f);
        text.gameObject.SetActive(false);
    }
}
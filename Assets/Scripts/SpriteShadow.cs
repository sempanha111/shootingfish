using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class SpriteShadow : MonoBehaviour
{
    public Vector3 offset;
    GameObject shadownClone;
    void Start()
    {
        shadownClone = Instantiate(GameManager.Instance.Shadow, transform);

        shadownClone.AddComponent<SpriteRenderer>();
        SpriteRenderer sprRnd = shadownClone.GetComponent<SpriteRenderer>();
        SpriteRenderer parentSprRnd = GetComponent<SpriteRenderer>();
        sprRnd.sprite = parentSprRnd.sprite;
        sprRnd.color = GameManager.Instance.shadowColor;
        sprRnd.sortingLayerName = "Fish";
        sprRnd.sortingOrder = parentSprRnd.sortingOrder - 1;

        Animator ShadowAnimator = shadownClone.AddComponent<Animator>();
        ShadowAnimator.runtimeAnimatorController = GetComponent<Animator>().runtimeAnimatorController;

        float scale = shadownClone.transform.localScale.x;
        scale -= scale * 0.2f;
        shadownClone.transform.localScale = Vector3.one * scale;

        shadownClone.transform.position = transform.position + offset;
        shadownClone.transform.rotation = transform.rotation;
    }


    void OnBecameVisible()
    {
        shadownClone.transform.position = transform.position + offset;
        shadownClone.transform.rotation = transform.rotation;
    }
}

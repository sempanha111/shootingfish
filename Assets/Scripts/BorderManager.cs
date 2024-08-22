using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class BoderManager : MonoBehaviour
{
   private void OnTriggerEnter2D(Collider2D other) {
    if(other.gameObject.CompareTag("Fish")){
        SpriteRenderer Fish = other.GetComponent<SpriteRenderer>();
        Destroy(Fish.gameObject);    
    }
   }
}

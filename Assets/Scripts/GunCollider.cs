using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunCollider : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.gameObject.CompareTag("Fish")){
            Collider2D Fish = other.GetComponent<Collider2D>();
            Fish.enabled = false;
            StartCoroutine(IEnumReable(Fish));
        }
    }

    private  IEnumerator IEnumReable(Collider2D Fish){
        yield return new WaitForSeconds(2f);
        Fish.enabled = true;
    }

}

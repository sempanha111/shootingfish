using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GunCollider : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Fish"))
        {
            Collider2D fishCollider = other.GetComponent<Collider2D>();
            if (fishCollider != null)
            {
                fishCollider.enabled = false;
                StartCoroutine(IEnumReable(fishCollider));
            }
        }
    }



    private IEnumerator IEnumReable(Collider2D Fish)
    {
        yield return new WaitForSeconds(2f);

        // Check if the fishCollider still exists before enabling it
        if (Fish != null)
        {
            Fish.enabled = true;
        }
    }

}

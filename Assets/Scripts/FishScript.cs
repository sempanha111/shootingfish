using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FishScript : MonoBehaviour
{   
    [SerializeField] public float Hp = 5f;

    
    private BulletScript bulletScript;

    private GameManager GM;

    private void Start()
    {
        bulletScript = BulletScript.Instance;
       GM = GameManager.Instance;
        
    }

    private bool IsDead(){
        return Hp <= 0;
    }


    public void ResetFishColor(SpriteRenderer Fish, Color backUpColor)
    {
        StartCoroutine(IEnumResetFishColor(Fish, backUpColor));
    }

    private IEnumerator IEnumResetFishColor(SpriteRenderer Fish, Color backUpColor)
    {
        yield return new WaitForSeconds(0.1f);
        Fish.color = backUpColor;
    }



    public void TakeDamage(SpriteRenderer Fish, float Damage){
        Hp -= Damage;      
        FishSystem(Fish);
    }

    private void FishSystem(SpriteRenderer Fish){
        if(IsDead()){
            Fish.gameObject.SetActive(false);
            
        }
    }   


}

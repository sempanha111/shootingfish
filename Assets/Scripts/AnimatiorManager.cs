using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AnimatiorManager : MonoBehaviour
{
    public Transform animatorParent;
    public GameObject[] Anima_object;
    public GameObject partical;
    public Transform particleParent;

    private List<GameObject> ListAnimator = new List<GameObject>();
    private List<ParticleSystem> Listparticle = new List<ParticleSystem>();

    Vector3 offet;
    private GameManager GM;
    private void Start()
    {
        GM = GameManager.Instance;
        

    }
    public void PlayAnima(Vector3 pos, int index, int BulletId){
        GameObject anima_clone = ListAnimator.FirstOrDefault(o => !o.activeSelf && o.name == "AnimationFish"+index+"(Clone)");
        if(anima_clone == null){
            var animatorInstan = Instantiate(Anima_object[index - 1], animatorParent);
            ListAnimator.Add(animatorInstan);
            anima_clone = animatorInstan;
        }
        anima_clone.transform.position = pos;

        anima_clone.SetActive(true);

        StartCoroutine(IEnumMove(anima_clone, BulletId));
    }
    private IEnumerator IEnumMove(GameObject obj , int BulletId){

        yield return new WaitForSeconds(1f);


        
        if(BulletId == 0 || BulletId == 1){
            offet = new Vector3(0, 1f,0);
        }
        else{
            offet = new Vector3(0, -1f,0);
        }

        Vector3 end_pos = GM.coinManager.coinEnd[BulletId].transform.position + offet;

        float speed = 50f;
        float initDistance = Vector2.Distance(obj.transform.position, end_pos);
        float distance = initDistance;
        while(distance > 1){
            float Factor = distance / initDistance;
            obj.transform.position = Vector2.MoveTowards(obj.transform.position, end_pos, Time.deltaTime * speed * Factor);
            distance = Vector2.Distance(obj.transform.position, end_pos);
            yield return null;
        }

        yield return new WaitForSeconds(4f);
        // obj.transform.position = Vector3.zero;
        obj.SetActive(false);

    }

    private ParticleSystem Getparticle(){
        ParticleSystem particleClone = Listparticle.FirstOrDefault(o => !o.isPlaying);
        if(particleClone == null){
            GameObject particleInstance = Instantiate(partical, particleParent);
            particleClone = particleInstance.GetComponent<ParticleSystem>(); 
            Listparticle.Add(particleClone);
        }
        

        return particleClone;

    }
    public void playParticle(Vector3 pos){
        ParticleSystem particle = Getparticle();
        particle.gameObject.SetActive(true);
        particle.transform.position = pos;
        particle.Play();
        StartCoroutine(ReturnParticleToPoll(particle));
    }

    private IEnumerator ReturnParticleToPoll(ParticleSystem particle){
        yield return new WaitForSeconds(3f);
        if(partical != null){
            particle.Stop();
            particle.Clear();
            particle.gameObject.SetActive(false);
        }
    }

}
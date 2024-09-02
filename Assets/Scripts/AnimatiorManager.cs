using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AnimatiorManager : MonoBehaviour
{
    public Transform animatorParent;
    public GameObject[] Anima_object;
    public GameObject partical;
    public Transform particleParent;
    public Transform[] GunPos;
    public GameObject Coin;

    private List<GameObject> ListAnimator = new List<GameObject>();
    private List<GameObject> ListCoinAnimator = new List<GameObject>();
    private List<ParticleSystem> Listparticle = new List<ParticleSystem>();


    public TextMeshProUGUI CointextUI;

    Vector3 offet;
    private GameManager GM;
    private void Start()
    {
        GM = GameManager.Instance;
    }


    private ParticleSystem Getparticle()
    {
        ParticleSystem particleClone = Listparticle.FirstOrDefault(o => !o.isPlaying);
        if (particleClone == null)
        {
            GameObject particleInstance = Instantiate(partical, particleParent);
            particleClone = particleInstance.GetComponent<ParticleSystem>();
            Listparticle.Add(particleClone);
        }
        return particleClone;
    }
    public void playParticle(Vector3 pos)
    {
        ParticleSystem particle = Getparticle();
        particle.gameObject.SetActive(true);
        particle.transform.position = pos;
        particle.Play();
        StartCoroutine(ReturnParticleToPoll(particle));
    }

    private IEnumerator ReturnParticleToPoll(ParticleSystem particle)
    {
        yield return new WaitForSeconds(3f);
        if (partical != null)
        {
            particle.Stop();
            particle.Clear();
            particle.gameObject.SetActive(false);
        }
    }

    public void PlayCoin(Vector3 pos, int index, int BulletId)
    {
        GameObject coin_clone = ListCoinAnimator.FirstOrDefault(o => !o.activeSelf);
        if (coin_clone == null)
        {
            var animatorInstan = Instantiate(Coin, animatorParent);
            ListCoinAnimator.Add(animatorInstan);
            coin_clone = animatorInstan;
        }

        coin_clone.transform.position = pos;
        coin_clone.SetActive(true);


        StartCoroutine(IEnumMove(coin_clone, BulletId));
    }
    private IEnumerator IEnumMove(GameObject obj, int BulletId)
    {

        yield return new WaitForSeconds(0.8f);


        if (BulletId == 0 || BulletId == 1)
        {
            offet = new Vector3(0, 2f, 0);
        }
        else
        {
            offet = new Vector3(0, -2f, 0);
        }

        Vector3 end_pos = GunPos[BulletId].position + offet;

        float speed = 50f;
        float initDistance = Vector2.Distance(obj.transform.position, end_pos);
        float distance = initDistance;
        while (distance > 1)
        {
            float Factor = distance / initDistance;
            obj.transform.position = Vector2.MoveTowards(obj.transform.position, end_pos, Time.deltaTime * speed * Factor);
            distance = Vector2.Distance(obj.transform.position, end_pos);
            yield return null;
        }

        StartCoroutine(PlayAnima(obj.transform.position, 1));
        obj.SetActive(false);

    }
    public IEnumerator PlayAnima(Vector3 pos, int index)
    {
        GameObject anima_clone = ListAnimator.FirstOrDefault(o => !o.activeSelf && o.name == "AnimationFish" + index + "(Clone)");
        if (anima_clone == null)
        {
            var animatorInstan = Instantiate(Anima_object[index - 1], animatorParent);
            ListAnimator.Add(animatorInstan);
            anima_clone = animatorInstan;
        }
        anima_clone.transform.position = pos;
        anima_clone.SetActive(true);

        TextMeshProUGUI Textclone = Instantiate(CointextUI,pos + new Vector3(0, -0.7f,0), Quaternion.identity, GM.DisplayTextManagerScript.TextHolder);
        Textclone.text = "Win "+ 190.ToString();
        Textclone.gameObject.SetActive(true);


        yield return new WaitForSeconds(3f);
        Destroy(Textclone.gameObject);
        anima_clone.SetActive(false);
    }

}
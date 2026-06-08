using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    private GameManager GM;


    public AudioSource bgMusic;
    public AudioSource Click;
    public AudioSource SoundCoin1;
    public AudioSource SoundCoin2;
    public AudioSource BigwinCoin1;
    void Start()
    {
        GM = GameManager.Instance;
        // LoadSoundSetting();
    }

    // Update is called once per frame

    public void PlayClickSound()
    {
        Click.Play();
    }
    public void PlaySoundCoin1()
    {
        SoundCoin1.Play();
    }
    public void PlaySoundCoin2()
    {
        SoundCoin2.Play();
    }
    public void PlaySoundBigwinCoin1()
    {
        BigwinCoin1.Play();
    }
}

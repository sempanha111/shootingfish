using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    private GameManager GM;


    public AudioSource bgMusic;
    public AudioSource Click;
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
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
public class UIManager : MonoBehaviour
{

    [SerializeField] private Text textTotal;
    [SerializeField] private Text textBet;

    int activeLevel;

    //NPC
    [SerializeField] private Text[] textTotalNPC;
    [SerializeField] private Text[] textBetNPC;

    public GameObject pausePanel;
    public GameObject resumePanel;

    public GameObject soundOn;
    public GameObject soundOff;
    public Image sumbtn;
    public Image dokbtn;


    private GameManager GM;
    void Start()
    {
        GM = GameManager.Instance;
        LoadSoundSetting();
        Color col = dokbtn.color;
        col.a = 0.4f;
        dokbtn.color = col;
    }

    public void SetTextTotal(string st)
    {
        textTotal.text = st;
    }

    public void SetTextBet(string st)
    {
        textBet.text = st;
    }


    //NPC
    public void SetTextTotalNPC(int id, string st)
    {
        textTotalNPC[id - 1].text = st;
    }

    public void SetTextBetNPC(int id, string st)
    {
        textBetNPC[id - 1].text = st;
    }

    public void BtnOpacity(Image Btn, float opa)
    {
        Color col = Btn.color;
        col.a = opa;
        Btn.color = col;
    }



    public void dokGun()
    {
        activeLevel = GM.weaponsScripts.activeGunLevel;
        if (activeLevel > 1)
        {
            if (activeLevel == 2)
            {
                BtnOpacity(dokbtn, 0.4f);
            }

            if (activeLevel == 17)
            {
                BtnOpacity(sumbtn, 1f);
            }

            activeLevel--;

            GM.weaponsScripts.ActivateGun(activeLevel);
            GM.weaponsScripts.activeGunLevel = activeLevel;
        }
        // else
        // {
        //     Color col = dokbtn.color;
        //     col.a = 0.4f;
        //     dokbtn.color = col;
        // }
    }
    public void SumGun()
    {
        activeLevel = GM.weaponsScripts.activeGunLevel;
        if (activeLevel < GM.weaponsScripts.Gunlevel.Length)
        {
            if ((activeLevel + 1) == GM.weaponsScripts.Gunlevel.Length)
            {
                BtnOpacity(sumbtn, 0.4f);
            }
            if (activeLevel == 1)
            {
                BtnOpacity(dokbtn, 1f);
            }


            activeLevel++;

            GM.weaponsScripts.ActivateGun(activeLevel);
            GM.weaponsScripts.activeGunLevel = activeLevel;
        }
        // else
        // {
        //     Color col = sumbtn.color;
        //     col.a = 0.4f;
        //     sumbtn.color = col;
        // }
    }
    public void GoHome()
    {
        Time.timeScale = 1f;

        if (GM != null)
        {
            GM.SavePlayerCoinNow();
        }

        SceneManager.LoadScene(0);
    }
    public void PauseGame()
    {
        Time.timeScale = 0f;
        SoundOff();
        pausePanel.SetActive(true);
        resumePanel.SetActive(false);
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
        SoundOn();
        pausePanel.SetActive(false);
        resumePanel.SetActive(true);
    }




    public void SoundOff()
    {
        AudioListener.volume = 0f;

        soundOff.SetActive(true);
        soundOn.SetActive(false);

        PlayerPrefs.SetInt("Sound", 0);
        PlayerPrefs.Save();
    }

    public void SoundOn()
    {
        AudioListener.volume = 1f;

        soundOff.SetActive(false);
        soundOn.SetActive(true);

        PlayerPrefs.SetInt("Sound", 1);
        PlayerPrefs.Save();
    }

    private void LoadSoundSetting()
    {
        int soundState = PlayerPrefs.GetInt("Sound", 1); // Default = ON

        if (soundState == 0)
        {
            AudioListener.volume = 0f;
            soundOff.SetActive(true);
            soundOn.SetActive(false);
        }
        else
        {
            AudioListener.volume = 1f;
            soundOff.SetActive(false);
            soundOn.SetActive(true);
        }
    }


}

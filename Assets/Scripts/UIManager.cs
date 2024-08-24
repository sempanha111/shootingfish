using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{

    [SerializeField] private Text textTotal;
    [SerializeField] private Text textBet;

    int activeLevel;

    //NPC
    [SerializeField] private Text[] textTotalNPC;
    [SerializeField] private Text[] textBetNPC;



    private GameManager GM;
    void Start()
    {
        GM = GameManager.Instance;
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



    

    public void dokGun()
    {
         activeLevel = GM.weaponsScripts.activeGunLevel;
        if(activeLevel > 1){
            activeLevel--;

            GM.weaponsScripts.ActivateGun(activeLevel);
            GM.weaponsScripts.activeGunLevel = activeLevel;
        }
    }
    public void SumGun()
    {
         activeLevel = GM.weaponsScripts.activeGunLevel;
        if(activeLevel < GM.weaponsScripts.Gunlevel.Length){
            activeLevel++;

            GM.weaponsScripts.ActivateGun(activeLevel);
            GM.weaponsScripts.activeGunLevel = activeLevel;
        }
    }


}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{

    [SerializeField] private Text textTotal;
    [SerializeField] private Text textBet;


    public void SetTextTotal(string st)
    {
        textTotal.text = st;
    }

    public void SetTextBet(string st)
    {
        textBet.text = st;
    }
}

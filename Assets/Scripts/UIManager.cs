using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{

    [SerializeField] private Text textTotal;
    [SerializeField] private Text textBet;

    private GameManager GM;

    private void Start() {
        GM = GameManager.Instance;
    }
    private void Update() {
        textTotal.text = (GM.Amount).ToString();
        textBet.text = (GM.weaponsScripts.Totalbet).ToString();
         
    }
}

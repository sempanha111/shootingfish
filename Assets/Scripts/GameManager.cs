using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public WeaponsScripts weaponsScripts {get; private set;}

    

    public float Amount = 2000;

    void Awake() {
        Instance = this;
    }

    private void Start(){
        weaponsScripts = GetComponent<WeaponsScripts>();
    }

    public void CalculateTotalCoinWithBet(){
        Amount -= weaponsScripts.Totalbet;
    }


}

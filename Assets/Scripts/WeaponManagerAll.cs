using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

public class WeaponManagerAll : MonoBehaviour
{
    [SerializeField] public GameObject[] Weapon;

    private int Weapon_ti;

    private int setToNpc1;

    void Start()
    {
        
    }

    private void ChooseWeapon(int Weapon_ti){

        switch(Weapon_ti){
            case 1: 
                // Debug.Log("Weapon ti 1");
                
                break;
            case 2:
                // Debug.Log("Weapon ti 1");
                break;
            default:
                // Debug.Log("weapon ti 1");
                break;

        }
    }

}

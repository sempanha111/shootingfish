using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    void Update()
    {

    }
    public void StartGame()
    {
        SceneManager.LoadScene(1);
    }

    public void GoHome()
    {
        SceneManager.LoadScene(0);
    }
}

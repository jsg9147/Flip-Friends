using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MainUIManager : MonoBehaviour
{
    public MainUI mainUI;

    public List<GameObject> uiList;
    void Start()
    {
        StartInit();
    }

    void StartInit()
    {
        foreach (GameObject go in uiList)
        {
            go.SetActive(false);
        }

        mainUI.gameObject.SetActive(true);
    }
}

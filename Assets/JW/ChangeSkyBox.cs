using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeSkyBox : MonoBehaviour
{
    public Material[] skyboxes;
    private int index = 0;

    private void Start()
    {
        RenderSettings.skybox = skyboxes[index];
    }

    void Update()
    {
        if (Input.GetMouseButtonUp(0))
        {
            index++;
            index %= skyboxes.Length;
            RenderSettings.skybox = skyboxes[index];
        }
    }
}

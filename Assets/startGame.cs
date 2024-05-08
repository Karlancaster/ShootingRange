using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ViveSR.anipal.Eye;


public class startGame : MonoBehaviour
{

    void Start(){
        SRanipal_Eye_v2.LaunchEyeCalibration();
    }
    // Update is called once per frame
    void Update()
    {
        // Check if the left mouse button (button index 0) is clicked
        if (Input.GetMouseButtonDown(0))
        {
            // Load the scene named "Calibration"
            SceneManager.LoadScene("Calibration");
        }
    }
}

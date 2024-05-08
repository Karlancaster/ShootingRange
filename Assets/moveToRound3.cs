using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class moveToRound3 : MonoBehaviour
{
    // Start is called before the first frame update
    IEnumerator Start()
    {
        // Wait for 5 seconds
        yield return new WaitForSeconds(5);
        // Load the scene named "Round1"
        SceneManager.LoadScene("Round2");
    }
}

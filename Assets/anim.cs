using UnityEngine;
using System.Collections;

public class anim : MonoBehaviour {

	// Use this for initialization
    private bool notPlaying = false;
	// Update is called once per frame
    void Update()
    {
        if (!GameObject.Find("Blades").GetComponent<Animation>().isPlaying && !notPlaying)
            GameObject.Find("Blades").GetComponent<Animation>().Play("Idle");
    }
    void OnTriggerEnter(Collider other)
    {
        GetComponent<Animation>().CrossFade("ToClose", 0.1f);
        notPlaying = true;
	}

    void OnTriggerExit(Collider other)
    {
        GetComponent<Animation>().CrossFade("ToCloseF", 0.1f);
        notPlaying = false;
    }
}

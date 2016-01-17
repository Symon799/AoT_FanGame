using UnityEngine;
using System.Collections;

public class anim : MonoBehaviour {

	// Use this for initialization
    //private bool notPlaying = false;
    //private bool notPlaying2 = false;
    private GameObject velo;
    //private GameObject blades;
	// Update is called once per frame

    void Start()
    {
        velo = GameObject.Find("RigidBodyFPSController");
        //blades = GameObject.Find("Blades");
    }
    void Update()
    {
        //if (!notPlaying)
        {
            if (velo.GetComponent<Rigidbody>().velocity.magnitude > 0.7)
            {
                GetComponent<Animator>().SetBool("speed", true);
                //blades.GetComponent<Animation>().CrossFade("Walk2", 0.2f);
            }
            else
            {
                GetComponent<Animator>().SetBool("speed", false);
                //blades.GetComponent<Animation>().CrossFade("Idle", 0.2f);
            }
        }
    }
    void OnTriggerEnter(Collider other)
    {
        GetComponent<Animator>().SetBool("close", true);
        //GetComponent<Animation>().CrossFade("ToClose", 0.2f);
        //notPlaying = true;
	}

    void OnTriggerExit(Collider other)
    {
        GetComponent<Animator>().SetBool("close", false);
        //GetComponent<Animation>().CrossFade("ToCloseF", 0.2f);
        //notPlaying = false;
    }
}

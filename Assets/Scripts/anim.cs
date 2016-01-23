using UnityEngine;
using System.Collections;

public class anim : MonoBehaviour {

    private GameObject velo;
    //private ParticleSystem smoke;


    void Start()
    {
        //smoke = GameObject.Find("WhiteSmoke").GetComponent<ParticleSystem>();
        velo = GameObject.Find("RigidBodyFPSController");
    }
    void Update()
    {
        if (velo.GetComponent<Rigidbody>().velocity.magnitude > 0.7)
        {
            GetComponent<Animator>().SetBool("speed", true);
        }
        else
        {
            GetComponent<Animator>().SetBool("speed", false);
        }
    }

    void OnTriggerEnter()
    {
        GetComponent<Animator>().SetBool("close", true);
    }

    void OnTriggerExit()
    {
        GetComponent<Animator>().SetBool("close", false);
    }
}

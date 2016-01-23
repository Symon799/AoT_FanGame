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
        if (Input.GetButtonDown("Attack"))
            GetComponent<Animator>().SetInteger("state", 1);
        else if (Input.GetButtonUp("Attack"))
        {
            GetComponent<Animator>().SetInteger("state", 2);
            StartCoroutine("attack");
        }

        if (velo.GetComponent<Rigidbody>().velocity.magnitude > 0.7)
        {
            GetComponent<Animator>().SetBool("speed", true);
            if (velo.GetComponent<Rigidbody>().velocity.magnitude > 10)
                GetComponent<Animator>().SetBool("air", true);
            else
                GetComponent<Animator>().SetBool("air", false);
                
        }
        else
        {
            GetComponent<Animator>().SetBool("speed", false);
        }
    }

    IEnumerator attack()
    {
        yield return new WaitForSeconds(0.15f);
        GetComponent<Animator>().SetInteger("state", 0);
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

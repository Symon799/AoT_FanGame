using UnityEngine;
using System.Collections;

 [RequireComponent(typeof(AudioSource))]
public class anim : MonoBehaviour {

    private GameObject velo;
    public ParticleSystem speedParticles;
    private ParticleSystem smoke;
    private ParticleSystem.EmissionModule smokeEm;


    void Start()
    {
        smoke = GameObject.Find("WhiteSmoke").GetComponent<ParticleSystem>();
        velo = GameObject.Find("RigidBodyFPSController");
        smokeEm = smoke.emission;
    }
    void Update()
    {
        float vit = velo.GetComponent<Rigidbody>().velocity.magnitude;
        if (Input.GetButtonDown("Attack"))
            GetComponent<Animator>().SetInteger("state", 1);
        else if (Input.GetButtonUp("Attack"))
        {
            GetComponent<Animator>().SetInteger("state", 2);
            GetComponent<AudioSource>().Play();
            StartCoroutine("attack");
        }
        if (vit > 0.7)
        {
            smokeEm.enabled = true;
            if (vit > 25)
                speedParticles.Play();
            else
                speedParticles.Stop();
                
            GetComponent<Animator>().SetBool("speed", true);
            if (vit > 10)
                GetComponent<Animator>().SetBool("air", true);
            else
                GetComponent<Animator>().SetBool("air", false);

        }
        else
        {
            smokeEm.enabled = false;
            GetComponent<Animator>().SetBool("speed", false);
        }
    }
        IEnumerator attack()
    {
        yield return new WaitForSeconds(0.15f);
        GetComponent<Animator>().SetInteger("state", 0);
    }

    void OnTriggerEnter(Collider collider)
    {
        if (collider.tag != "Vision")
            GetComponent<Animator>().SetBool("close", true);
    }

    void OnTriggerExit(Collider collider)
    {
        if (collider.tag != "Vision")
            GetComponent<Animator>().SetBool("close", false);
    }
}

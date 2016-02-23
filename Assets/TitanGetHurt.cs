using UnityEngine;
using System.Collections;

 [RequireComponent(typeof(AudioSource))]
public class TitanGetHurt : MonoBehaviour {

    public GameObject titan;
    public GameObject blood;
    public GameObject bloodloc;
	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}

    void OnTriggerStay(Collider collider)
    {
        if (collider.tag == "Blades" && Input.GetButtonUp("Attack") && titan.GetComponent<Animator>().GetInteger("state") == 0)
        {
            titan.GetComponent<Animator>().SetBool("dead", true);
            GetComponent<AudioSource>().Play();
            Instantiate(blood, bloodloc.transform.position, bloodloc.transform.rotation);
        }
    }
}

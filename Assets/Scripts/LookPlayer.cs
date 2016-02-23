using UnityEngine;
using System.Collections;

public class LookPlayer : MonoBehaviour {

    private GameObject player;
    private bool dead = false;
    private bool oneTime = false;
    private bool deadLookTime = false;
    private Vector3 deadO;

    public GameObject head;
    public GameObject deadOrient;
    public float fieldOfViewAngle = 110f;
    public float speed;
    public float speedrotdeath;

    private bool seen;
    private float x, y, z;

	void Start ()
    {
        player = GameObject.Find("RigidBodyFPSController");
	}
	
	// Update is called once per frame
	void Update ()
    {
        if (GetComponent<Animator>().GetBool("dead"))
            dead = true;

        Vector3 direction = player.transform.position - head.transform.position;
        float angle = Vector3.Angle(direction, transform.forward);
        if (angle < fieldOfViewAngle * 0.5f)
            seen = true;
        else
            seen = false;
        if (dead)
        {
            if (!oneTime)
            {
                StartCoroutine("DeadLook");
                oneTime = true;
                deadO = deadOrient.transform.forward;
            }
            if (!deadLookTime)
            {
                SmoothLook(deadO, speedrotdeath);
            }
        }
	}

    void OnTriggerStay(Collider collider)
    {
        if (!dead)
        {
            if (collider.tag == "Player" && seen)
                SmoothLook(player.transform.position - head.transform.position, speed);
        }
    }

    void SmoothLook(Vector3 newDirection, float speedy)
    {
        head.transform.rotation = Quaternion.Lerp(head.transform.rotation, Quaternion.LookRotation(newDirection), Time.deltaTime * speedy);
    }

    IEnumerator DeadLook()
    {
        yield return new WaitForSeconds(0.9f);
        deadLookTime = true;
    }
}

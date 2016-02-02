using UnityEngine;
using System.Collections;

public class LookPlayer : MonoBehaviour {

    private GameObject player;
    public GameObject head;
    public float fieldOfViewAngle = 110f;
    public float speed;
    private bool seen;
	// Use this for initialization
	void Start ()
    {
        player = GameObject.Find("RigidBodyFPSController");
	}
	
	// Update is called once per frame
	void Update ()
    {
        Vector3 direction = player.transform.position - head.transform.position;
        float angle = Vector3.Angle(direction, transform.forward);
        if (angle < fieldOfViewAngle * 0.5f)
            seen = true;
        else
            seen = false;
        //head.transform.LookAt(player.transform.position);
	}

    void OnTriggerStay(Collider collider)
    {
        if (collider.tag == "Player" && seen)
            SmoothLook(player.transform.position - head.transform.position);
    }

    void SmoothLook(Vector3 newDirection)
    {
        head.transform.rotation = Quaternion.Lerp(head.transform.rotation, Quaternion.LookRotation(newDirection), Time.deltaTime * speed);
    }
}

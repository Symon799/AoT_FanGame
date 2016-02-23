using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class ShootGrapple : MonoBehaviour {

    public Rigidbody grap;
    public float speed;
	
	void Update ()
    {
        if (Time.timeScale != 0)
        {
            if (this.gameObject.name == "ShootRight")
            {
                if (Input.GetMouseButtonDown(0))
                {
                    GetComponent<AudioSource>().Play();
                    Rigidbody cloneLeft = Instantiate(grap, transform.position, transform.rotation) as Rigidbody;
                    cloneLeft.name = "cloneLeft";
                    cloneLeft.velocity = transform.TransformDirection(Vector3.forward * speed);
                }
            }
            else
            {
                if (Input.GetMouseButtonDown(1))
                {
                    GetComponent<AudioSource>().Play();
                    Rigidbody cloneRight = Instantiate(grap, transform.position, transform.rotation) as Rigidbody;
                    cloneRight.name = "cloneRight";
                    cloneRight.velocity = transform.TransformDirection(Vector3.forward * speed);
                }
            }
        }
	}
}

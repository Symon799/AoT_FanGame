using UnityEngine;
using System.Collections;

public class Hook : MonoBehaviour {

    //public GameObject smoke;
    private GameObject player;
    private GameObject player2;
    private Rigidbody Rplay;

    private bool oneTime = false;
    private float curDistance;
    private LineRenderer line;
    private bool oneTime2 = true;
    private float distance;
    private Vector3 aimPosition;

    float startWidth = 0.03f;
    float endWidth = 0.006f;

    public float speedHook = 10;
    bool hooked = false;


    void Start()
    {
        player = GameObject.Find("RigidBodyFPSController");
        Rplay = GameObject.Find("RigidBodyFPSController").GetComponent<Rigidbody>();
        line = GetComponentInChildren<LineRenderer>();
    }

    void DestroyGrap()
    {
        startWidth -= 0.001f;
        endWidth -= 0.001f;
        line.SetWidth(startWidth, endWidth);
        Destroy(this.gameObject, 0.1f);
    }



    void FixedUpdate()
    {
        if (Rplay.velocity.magnitude > 22)
            Rplay.velocity = Rplay.velocity.normalized * 22;

        //DESTROY
        float destroyDistance = Vector3.Distance(this.transform.position, player.transform.position);
        if (destroyDistance > 80)
            DestroyGrap();

        if (this.gameObject.name == "cloneLeft")
	    {
            if (!Input.GetButton("Fire1"))
		    {
                DestroyGrap();
		    }
	    }
	    else
	    {
		    if (!Input.GetButton("Fire2"))
		    {
                DestroyGrap();
		    }
	    }

	    if (hooked)
	    {                
            if (Input.GetButton("Jump"))
            {
                try
                {
                    if (oneTime2)
                    {
                        GameObject.Find("MainCamera").GetComponent<Animation>().Play();
                        oneTime2 = false;
                    }

  
                    //GameObject.Find("MainCamera").GetComponent<Animation>().Play();
                    if (Input.GetButton("Fire1") && Input.GetButton("Fire2") && GameObject.Find("cloneRight").GetComponent<Rigidbody>().isKinematic && GameObject.Find("cloneLeft").GetComponent<Rigidbody>().isKinematic)
                    {
                        GameObject cloneL = GameObject.Find("cloneLeft");
                        GameObject cloneR = GameObject.Find("cloneRight");
                        Vector3 moveDir1 = (cloneR.transform.position - player.transform.position).normalized;
                        Vector3 moveDir2 = (cloneL.transform.position - player.transform.position).normalized;
                        Vector3 moveDirF = moveDir1 + moveDir2;
                        Rplay.velocity = (moveDirF * (speedHook - 1));
                        oneTime = true;
                    }
                    else
                    {
                        if (!oneTime)
                        {
                            Vector3 moveDir = (this.transform.position - player.transform.position).normalized;
                            Rplay.velocity = (moveDir * speedHook);
                            Rplay.velocity += player.transform.forward * speedHook;
                        }
                        else
                            StartCoroutine("MyCoroutine");
                    }
                }
                catch { }
            }
            
            /*curDistance = Vector3.Distance(this.transform.position, player.transform.position);

	 	    if (curDistance > distance)
	 	    {
                player.transform.position = distance * (player.transform.position - this.transform.position).normalized + transform.position;
                //aimPosition = distance * (player.transform.position - this.transform.position).normalized + transform.position;

                //Vector3 moveDir = (this.transform.position - player.transform.position).normalized;
                //Rplay.velocity = (Rplay.velocity * -Rplay.velocity.magnitude/10);
	   	    }*/
	    }
        else
            oneTime2 = true;
	}


    IEnumerator MyCoroutine()
    {
        yield return new WaitForSeconds(0.1f);
        oneTime = false;
    }

    void OnCollisionEnter(Collision collision)
    {
	    GetComponent<Rigidbody>().isKinematic = true;
        distance = Vector3.Distance(this.transform.position, player.transform.position);
 	    hooked = true;
    }
}

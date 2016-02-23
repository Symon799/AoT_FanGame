using UnityEngine;
using System.Collections;

public class Hook : MonoBehaviour {

    //public GameObject smoke;
    private GameObject player;
    private GameObject player2;
    private Rigidbody Rplay;

    private bool oneTime;
    private float curDistance;
    private LineRenderer line;
    private bool oneTime2 = true;
    //private float distance;
    private Vector3 aimPosition;
    private GameObject cloneL;
    private GameObject cloneR;
    private GameObject mainCamera;


    float startWidth = 0.03f;
    float endWidth = 0.006f;

    public float speedHook = 10;
    bool hooked = false;


    void Start()
    {
        cloneL = GameObject.Find("cloneLeft");
        cloneR = GameObject.Find("cloneRight");
        player = GameObject.Find("RigidBodyFPSController");
        Rplay = GameObject.Find("RigidBodyFPSController").GetComponent<Rigidbody>();
        mainCamera = GameObject.Find("MainCamera");
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
        if (destroyDistance > 120)
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
                if (Input.GetButton("Fire1") && Input.GetButton("Fire2"))
                    oneTime = true;

                try
                {
                    if (oneTime2)
                    {
                        mainCamera.GetComponent<Animation>().Play();
                        oneTime2 = false;
                    }

                    if (Input.GetButton("Fire1") && Input.GetButton("Fire2") && cloneR.GetComponent<Rigidbody>().isKinematic && cloneL.GetComponent<Rigidbody>().isKinematic)
                    {
                        Vector3 moveDir1 = (cloneR.transform.position - player.transform.position).normalized;
                        Vector3 moveDir2 = (cloneL.transform.position - player.transform.position).normalized;
                        Vector3 moveDirF = moveDir1 + moveDir2;
                        Rplay.velocity = (moveDirF * (speedHook - 1));
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
        yield return new WaitForSeconds(0.2f);
        oneTime = false;
    }

    void OnCollisionEnter(Collision collision)
    {
	    GetComponent<Rigidbody>().isKinematic = true;
        //distance = Vector3.Distance(this.transform.position, player.transform.position);
 	    hooked = true;
    }
}

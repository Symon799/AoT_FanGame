using UnityEngine;
using System.Collections;

public class DrawGrapple : MonoBehaviour {

    private LineRenderer line;
    GameObject play;
    private bool shake = false;
    private bool timed = true;
    
    float startWidth = 0.04f;
    float endWidth = 0.01f;
	void Start ()
    {
        line = this.gameObject.GetComponent<LineRenderer>();
	    line.SetWidth(startWidth, endWidth);
	    line.SetVertexCount(2);
	    line.GetComponent<Renderer>().enabled = true;
	}
	
	// Update is called once per frame
	void Update ()
    {
        if (this.gameObject.transform.parent.gameObject.name == "cloneRight")
            play = GameObject.Find("ShootLeft");
        else
            play = GameObject.Find("ShootRight");

        StartCoroutine("MyCoroutine");
        if (timed)
        {
            if (!shake)
            {
                line.SetPosition(0, transform.position);
                line.SetPosition(1, play.transform.position);
                shake = true;
            }
            else
            {
                line.SetPosition(0, transform.position + new Vector3(0.005f, 0.005f, 0.005f));
                line.SetPosition(1, play.transform.position + new Vector3(0.005f, 0.005f, 0.005f));
                shake = false;
            }
        }
        else
        {
            line.SetPosition(0, transform.position);
            line.SetPosition(1, play.transform.position);
        }
	}

    IEnumerator MyCoroutine()
    {
        yield return new WaitForSeconds(0.3f);
        timed = false;
    }
}

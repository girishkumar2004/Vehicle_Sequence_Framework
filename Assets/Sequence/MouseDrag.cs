using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MouseDrag : MonoBehaviour
{

    Rigidbody rb;
    float ditansce = 3;
    void Start()
    {

       // rb = GetComponent<Rigidbody>();
       
    }

    private void OnMouseDrag()
    {
        Vector3 mousePosition = new Vector3(Input.mousePosition.x, Input.mousePosition.y, ditansce);//-Camera.main.transform.position.z + transform.position.z)

        Vector3 objPosition = Camera.main.ScreenToWorldPoint(mousePosition);

        transform.position = objPosition;

       // rb.isKinematic = true;

    }

   /* private void OnMouseUp()
    {
        rb.isKinematic = false;
    }*/


}
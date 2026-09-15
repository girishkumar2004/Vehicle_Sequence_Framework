using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerColission : MonoBehaviour
{
    public string _objectToBeCollided;
    public SequenceHandler seqHandler;

    private void Start()
    {
        seqHandler = GameObject.Find("Sequence Handler").GetComponent<SequenceHandler>();
    }
    private void OnTriggerEnter(Collider obj)
    {
        Debug.Log(obj.gameObject.name);
        if (obj.gameObject.name == _objectToBeCollided)
        {
            seqHandler.TaskCompleted();
        }
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.LogError(_objectToBeCollided);
        }
    }
}

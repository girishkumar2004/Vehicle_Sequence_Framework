using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TaskComplete : MonoBehaviour
{
    SequenceHandler handler;
    // Start is called before the first frame update
    void Start()
    {
        handler = GameObject.Find("Sequence Handler").GetComponent<SequenceHandler>();
    }

    public void EndTask()
    {
        handler.TaskCompleted();
    }
}

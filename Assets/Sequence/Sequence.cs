using System.Collections;
using System.Collections.Generic;
using UnityEngine;



[System.Serializable]
public class Sequence : MonoBehaviour
{
    public string SequenceName;
    [Tooltip("Create Tasks for sequence")]
    public List<Task> TaskList = new List<Task>();
    
}

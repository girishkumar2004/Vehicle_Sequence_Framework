using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PersistentSequenceHandler : MonoBehaviour
{
   
    [Tooltip("The sequence list is referred from the scriptable objects here.")]
    public List<Sequence> sequenceList = new List<Sequence>();
    [Tooltip("The current task index in each persistent sequence can be viewed here")]
    public List<int> currentTask;
    public List<bool> WaitForTrigger;

    void Start()
    {
        for (int i = 0; i < sequenceList.Count; i++)
        {
            currentTask.Add(0);
            WaitForTrigger.Add(false);
            NextTask(i);
        }
    }
    public void TaskCompleted(int index)
    {
        sequenceList[index].TaskList[currentTask[index]].TaskCompleted = true;
        currentTask[index]++;
        NextTask(index);
    }
        
    public void InitialiseTaskOnRepeat(int index)
    {
        foreach(Task a in sequenceList[index].TaskList)
        {
            a.TriggerCompleted = false;
            a.TaskCompleted = false;
        }
    }
    
    public void NextTask(int index)
    {
        if (currentTask[index] >= sequenceList[index].TaskList.Count)
        {
            currentTask[index]=0;
            InitialiseTaskOnRepeat(index);
            NextTask(index);
            return;
        }
        switch (sequenceList[index].TaskList[currentTask[index]].typeOfInteraction)
        {
           /* case Task.TypeOfInteraction.Draggable:
                WaitForTrigger[index] = true;
                break;
            //case Task.TypeOfInteraction.Raycast_Select:
            //    WaitForTrigger[index] = true;
            //    break;
            case Task.TypeOfInteraction.Animatable:
                WaitForTrigger[index] = true;
                break;*/
            case Task.TypeOfInteraction.None:
                WaitForTrigger[index] = false;
                break;
            default:
                break;
        }
        if (!WaitForTrigger[index])
        {
            sequenceList[index].TaskList[currentTask[index]].EventsToFollow.Invoke();
        }
    }
    public void TriggerForTaskDone(int index)
    {
        if (WaitForTrigger[index])
        {
            WaitForTrigger[index] = false;
            sequenceList[index].TaskList[currentTask[index]].TriggerCompleted = true;
            sequenceList[index].TaskList[currentTask[index]].EventsToFollow.Invoke();
        }
    }
}


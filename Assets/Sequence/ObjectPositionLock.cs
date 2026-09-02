using UnityEngine;

/// <summary>
/// Locks a GameObject at a specified Transform position every LateUpdate.
/// Prevents multiple components fighting over an object's world position.
/// Only the ACTIVE lock owns the object's position.
/// USAGE:
///   - Add to a manager or scene controller.
///   - Assign objectToLock and lockTarget.
///   - Call ActivateLock() when a task starts that requires the object at that position.
///   - Call DeactivateLock() when the task ends.
/// </summary>
[AddComponentMenu("Vedanta/Utilities/Object Position Lock")]
public class ObjectPositionLock : MonoBehaviour
{
    [Header("LOCK CONFIGURATION")]
    [Tooltip("The GameObject to be locked in place.")]
    public GameObject objectToLock;
    [Tooltip("The Transform this object should be locked to.")]
    public Transform lockTarget;
    [Tooltip("If true, rotation is also locked.")]
    public bool lockRotation = true;
    [Tooltip("If true, lock activates automatically on Enable.")]
    public bool lockOnEnable = false;

    [Header("STATE")]
    public bool isLocked = false;

    private void Awake() { enabled = false; }

    private void OnEnable()
    {
        if (lockOnEnable) ActivateLock();
    }

    private void LateUpdate()
    {
        if (!isLocked || objectToLock == null || lockTarget == null) return;

        if (lockRotation)
            objectToLock.transform.SetPositionAndRotation(lockTarget.position, lockTarget.rotation);
        else
            objectToLock.transform.position = lockTarget.position;
    }

    /// <summary>Begin locking the object every LateUpdate.</summary>
    public void ActivateLock()
    {
        isLocked = true;
        enabled = true;
        Debug.Log($"[ObjectPositionLock] Locked '{(objectToLock ? objectToLock.name : "null")}' to '{(lockTarget ? lockTarget.name : "null")}'");
    }

    /// <summary>Stop locking. Object may now move freely.</summary>
    public void DeactivateLock()
    {
        isLocked = false;
        enabled = false;
        Debug.Log($"[ObjectPositionLock] Released '{(objectToLock ? objectToLock.name : "null")}'");
    }

    public void SetLockTarget(Transform target)
    {
        lockTarget = target;
        if (isLocked && objectToLock != null && lockTarget != null)
            objectToLock.transform.SetPositionAndRotation(lockTarget.position, lockTarget.rotation);
    }

    public void MoveToTarget()
    {
        if (objectToLock != null && lockTarget != null)
            objectToLock.transform.SetPositionAndRotation(lockTarget.position, lockTarget.rotation);
    }
}

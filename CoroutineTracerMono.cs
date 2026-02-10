#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

public class CoroutineTraceMono : MonoBehaviour
{
    /// <summary>
    /// Start a managed coroutine with automatic tracking
    /// </summary>
    public new Coroutine StartCoroutine(IEnumerator routine)
    {
        return CoroutineManager.Instance.Run(this, routine, CoroutineManager.CoroutineStartPolicy.AllowMultiple);
    }
    
    /// <summary>
    /// Stop a coroutine by method name
    /// </summary>
    public new void StopCoroutine(string methodName)
    {
        CoroutineManager.Instance.Stop(this, methodName);
    }
    
    /// <summary>
    /// Stop a coroutine by Coroutine reference
    /// </summary>
    public new void StopCoroutine(Coroutine routine)
    {
        if (routine == null) return;
        
        // Find the handle that matches this coroutine
        var handle = CoroutineManager.Instance.GetHandleByCoroutine(this, routine);
        if (handle != null)
        {
            CoroutineManager.Instance.Stop(handle);
        }
        else
        {
            // Fallback to Unity's native stop if not tracked
            base.StopCoroutine(routine);
        }
    }
    
    /// <summary>
    /// Stop all coroutines on this MonoBehaviour
    /// </summary>
    public new void StopAllCoroutines()
    {
        CoroutineManager.Instance.StopAllByOwner(this);
    }
}
#endif

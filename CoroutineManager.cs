using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

// CoroutineManager: A centralized manager for tracking and controlling coroutines across the project.
// 與原生Coroutine方法對應：
//  - StartCoroutine: 原生作法 : manager作法
//      - StartCoroutine(IEnumerator) : Run(this, IEnumerator, policy)
//  - StopCoroutine: 原生作法 : manager作法
//      - StopCoroutine(Coroutine) : Stop(handle)
//      - StopCoroutine(string) : Stop(name, this)
//      - StopAllCoroutines() : StopAllByOwner(this)

public class CoroutineManager : MonoBehaviour
{
    private static CoroutineManager _instance;
    public static CoroutineManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("CoroutineManager");
                _instance = go.AddComponent<CoroutineManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    
    public enum CoroutineStartPolicy
    {
        StopExisting,    // Stop existing and start new (default)
        UseExisting,    // Don't start if already running
        AllowMultiple    // Allow multiple instances with same name
    }

    [Serializable]
    public class CoroutineHandle
    {
        [Tooltip("註冊事件方法名")]
#if ODIN_INSPECTOR && (UNITY_EDITOR || DLL_MODE)
        [Sirenix.OdinInspector.ShowInInspector]
        [Sirenix.OdinInspector.ReadOnly]
#endif
        public readonly string MethodName;
        [NonSerialized] public Coroutine Coroutine;
        [Tooltip("註冊事件實體")]
#if ODIN_INSPECTOR && (UNITY_EDITOR || DLL_MODE)
        [Sirenix.OdinInspector.ShowInInspector]
        [Sirenix.OdinInspector.ReadOnly]
#endif
        public readonly MonoBehaviour Owner;

        [Tooltip("執行時間")]
#if ODIN_INSPECTOR && (UNITY_EDITOR || DLL_MODE)
        [Sirenix.OdinInspector.ShowInInspector]
        [Sirenix.OdinInspector.DisplayAsString(Format = "HH:mm:ss")]
#endif
        public readonly DateTime StartTime;

        [Tooltip("結束時間")]
#if ODIN_INSPECTOR && (UNITY_EDITOR || DLL_MODE)
        [Sirenix.OdinInspector.ShowInInspector]
        [Sirenix.OdinInspector.DisplayAsString(Format = "HH:mm:ss")]
#endif
        public DateTime? EndTime {get; private set;}

        [Tooltip("執行狀態")]
#if ODIN_INSPECTOR && (UNITY_EDITOR || DLL_MODE)
        [Sirenix.OdinInspector.ShowInInspector]
        [Sirenix.OdinInspector.ReadOnly]
#endif
        public bool IsRunning {get; private set;}
        [Tooltip("事件註冊位址")]
#if ODIN_INSPECTOR && (UNITY_EDITOR || DLL_MODE)
        [Sirenix.OdinInspector.HideLabel]
        [Sirenix.OdinInspector.ShowInInspector]
        [Sirenix.OdinInspector.InlineButton(nameof(OpenInEditor), "", Icon = Sirenix.OdinInspector.SdfIconType.At)]
#endif
        public string CallSite {get; private set;}
        
        [NonSerialized] private string file;
        [NonSerialized] private int line;

        public CoroutineHandle(string name, MonoBehaviour owner)
        {
            MethodName = name;
            Owner = owner;
            StartTime = DateTime.Now;
            IsRunning = true;
#if DEVELOP_INFO
            CallSite = CaptureCallSite();
#else
            CallSite = "N/A";
#endif
        }

        public void SetRunningStatus(bool isRunning)
        {
            IsRunning = isRunning;
            if (!isRunning)
            {
                EndTime = DateTime.Now;
            }
        }
#if DEVELOP_INFO
        private string CaptureCallSite()
        {
            var stack = new System.Diagnostics.StackTrace(true);
            
            for (int i = 0; i < stack.FrameCount; i++)
            {
                var frame = stack.GetFrame(i);
                var method = frame.GetMethod();
                string fileName = frame.GetFileName();
                
                // Skip if no file info
                if (string.IsNullOrEmpty(fileName)) 
                    continue;
                
                // Skip internal Unity/Package files
                if (fileName.Contains("PackageCache") || 
                    fileName.Contains("Library") ||
                    fileName.Contains("CoroutineManager")) 
                    continue;
                
                // Skip the Run method and extension methods
                if (method.Name == "Run" || 
                    method.Name == "RunManaged" ||
                    method.DeclaringType == typeof(CoroutineManager))
                    continue;
                
                // Found the actual caller
                line = frame.GetFileLineNumber();
                
                // Make path relative to project
                string relativePath = fileName.Replace('\\', '/');
                string projectPath = System.IO.Path.GetDirectoryName(Application.dataPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(projectPath))
                {
                    relativePath = relativePath.Replace(projectPath + "/", "");
                }
                file = relativePath;
                return $"{relativePath}:{line}";
            }
            
            return "Unknown";
        }
#endif
        public void OpenInEditor()
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(file)) { return; }
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(file);
            if (asset != null)
                UnityEditor.AssetDatabase.OpenAsset(asset, line);
            else
                Debug.Log($"[<color=#FFCC00>HandlerInfo</color>] not found {file}");
#endif
        }
    }

#if ODIN_INSPECTOR && (UNITY_EDITOR || DLL_MODE)
    [Sirenix.OdinInspector.ListDrawerSettings(
                HideAddButton = true, HideRemoveButton = true,
                IsReadOnly = true, ShowItemCount = true)]
    [Sirenix.OdinInspector.Title("場景事件註冊表")]
    [Sirenix.OdinInspector.ShowInInspector]
    [Sirenix.OdinInspector.Searchable]
    [Sirenix.OdinInspector.HideLabel]
    [Sirenix.OdinInspector.TableList(ShowPaging = true, NumberOfItemsPerPage = 20, IsReadOnly = true)]
    [Sirenix.OdinInspector.DictionaryDrawerSettings(
        DisplayMode = Sirenix.OdinInspector.DictionaryDisplayOptions.CollapsedFoldout,
        KeyLabel = "", IsReadOnly = true)]
#endif
    // Running coroutines (always tracked)
    private Dictionary<string, List<CoroutineHandle>> runningCoroutines = new Dictionary<string, List<CoroutineHandle>>();

#if DEVELOP_INFO
#if ODIN_INSPECTOR && (UNITY_EDITOR || DLL_MODE)
    [Sirenix.OdinInspector.ListDrawerSettings(
                HideAddButton = true, HideRemoveButton = true,
                IsReadOnly = true, ShowItemCount = true)]
    [Sirenix.OdinInspector.Title("完成事件註冊表")]
    [Sirenix.OdinInspector.ShowInInspector]
    [Sirenix.OdinInspector.Searchable]
    [Sirenix.OdinInspector.HideLabel]
    [Sirenix.OdinInspector.TableList(ShowPaging = true, NumberOfItemsPerPage = 20, IsReadOnly = true)]
    [Sirenix.OdinInspector.DictionaryDrawerSettings(
        DisplayMode = Sirenix.OdinInspector.DictionaryDisplayOptions.CollapsedFoldout,
        KeyLabel = "", IsReadOnly = true)]
#endif
    // Completed coroutines (only tracked in development)
    private Dictionary<string, List<CoroutineHandle>> completedCoroutines = new Dictionary<string, List<CoroutineHandle>>();
    [SerializeField] private int maxCompletedHistorySize = 100;
#endif

    /// <summary>
    /// Register and run a coroutine on the owner MonoBehaviour
    /// </summary>
    public CoroutineHandle Run(MonoBehaviour owner, IEnumerator routine, CoroutineStartPolicy policy = CoroutineStartPolicy.AllowMultiple)
    {
        if (owner == null)
        {
            Debug.LogError("CoroutineManager: Owner cannot be null!");
            return null;
        }

        string coroutineName = ExtractMethodName(routine);
        
        // Check existing based on policy
        bool hasExisting = IsRunning(coroutineName, owner);
        
        switch (policy)
        {
            case CoroutineStartPolicy.StopExisting:
                if (hasExisting)
                {
                    Stop(coroutineName, owner);
                }
                break;
                
            case CoroutineStartPolicy.UseExisting:
                if (hasExisting)
                {
                    // Return existing handle instead of creating new one
                    return GetHandle(coroutineName, owner);
                }
                break;
                
            case CoroutineStartPolicy.AllowMultiple:
                // Do nothing, allow multiple instances
                break;
        }
        
        var handle = new CoroutineHandle(coroutineName, owner);

        handle.Coroutine = owner.StartCoroutine(WrapCoroutine(routine, handle));

        if (!runningCoroutines.ContainsKey(coroutineName))
        {
            runningCoroutines[coroutineName] = new List<CoroutineHandle>();
        }
        runningCoroutines[coroutineName].Add(handle);

        return handle;
    }


    private string ExtractMethodName(IEnumerator routine)
    {
        string fullName = routine.ToString();
        
        // Compiler generates: "<MethodName>d__XX"
        // We want to extract: "MethodName"
        if (fullName.Contains("<") && fullName.Contains(">"))
        {
            int start = fullName.IndexOf('<') + 1;
            int end = fullName.IndexOf('>');
            if (end > start)
            {
                return fullName.Substring(start, end - start);
            }
        }
        
        // Fallback to full name if parsing fails
        return fullName;
    }

    // 等待routine結束後的包裝器
    private IEnumerator WrapCoroutine(IEnumerator routine, CoroutineHandle handle)
    {
        yield return routine;
        
        handle.SetRunningStatus(false);

        // Handle completion based on build mode
        OnCoroutineCompleted(handle);
    }

    // Coroutine 結束後處理
    private void OnCoroutineCompleted(CoroutineHandle handle)
    {
        // Remove from running list
        if (runningCoroutines.TryGetValue(handle.MethodName, out var runningList))
        {
            runningList.Remove(handle);
            if (runningList.Count == 0)
            {
                runningCoroutines.Remove(handle.MethodName);
            }
        }

#if DEVELOP_INFO
        // DEVELOP_INFO: Move to completed table
        if (!completedCoroutines.ContainsKey(handle.MethodName))
        {
            completedCoroutines[handle.MethodName] = new List<CoroutineHandle>();
        }
        completedCoroutines[handle.MethodName].Add(handle);

        // Check if we need to remove oldest completed
        int totalCompleted = GetCompletedCount();
        if (totalCompleted > maxCompletedHistorySize)
        {
            RemoveOldestCompleted();
        }
#endif
        // Release: Just remove from running table (already done above)
    }

#if DEVELOP_INFO
    private void RemoveOldestCompleted()
    {
        // Find the oldest completed coroutine across all names
        CoroutineHandle oldest = null;
        string oldestName = null;

        foreach (var kvp in completedCoroutines)
        {
            foreach (var handle in kvp.Value)
            {
                if (oldest == null || handle.EndTime < oldest.EndTime)
                {
                    oldest = handle;
                    oldestName = kvp.Key;
                }
            }
        }

        if (oldest != null && oldestName != null)
        {
            completedCoroutines[oldestName].Remove(oldest);
            if (completedCoroutines[oldestName].Count == 0)
            {
                completedCoroutines.Remove(oldestName);
            }
        }
    }
#endif

    /// <summary>
    /// Stop coroutines with the given name and owner
    /// </summary>
    public void Stop(string name, MonoBehaviour owner)
    {
        if (runningCoroutines.TryGetValue(name, out var list))
        {
            foreach (var handle in list.Where(h => h.Owner == owner).ToList())
            {
                StopCoroutineHandle(handle);
            }
        }
    }

    /// <summary>
    /// Stop a specific coroutine handle
    /// </summary>
    public void Stop(CoroutineHandle handle)
    {
        if (handle != null && handle.IsRunning)
        {
            StopCoroutineHandle(handle);
        }
    }

    /// <summary>
    /// Stop a specific coroutine handle implementation
    /// </summary>
    private void StopCoroutineHandle(CoroutineHandle handle)
    {
        if (handle.Owner != null)
        {
            handle.Owner.StopCoroutine(handle.Coroutine);
        }
        handle.SetRunningStatus(false);
        
        // Handle completion
        OnCoroutineCompleted(handle);
    }

    public void StopAll()
    {
        foreach (var handle in GetRunning().ToList())
        {
            StopCoroutineHandle(handle);
        }
    }

    public void StopAllByOwner(MonoBehaviour owner)
    {
        foreach (var handle in GetRunning().Where(h => h.Owner == owner).ToList())
        {
            StopCoroutineHandle(handle);
        }
    }

    /// <summary>
    /// Check if any coroutine with the given name is running
    /// </summary>
    public bool IsRunning(string name)
    {
        return runningCoroutines.ContainsKey(name) && runningCoroutines[name].Count > 0;
    }

    /// <summary>
    /// Check if a coroutine with the given name and owner is running
    /// </summary>
    public bool IsRunning(string name, MonoBehaviour owner)
    {
        if (runningCoroutines.TryGetValue(name, out var list))
        {
            return list.Any(h => h.Owner == owner);
        }
        return false;
    }

    /// <summary>
    /// Get the first running coroutine handle with the given name and owner
    /// </summary>
    public CoroutineHandle GetHandle(string name, MonoBehaviour owner)
    {
        if (runningCoroutines.TryGetValue(name, out var list))
        {
            return list.FirstOrDefault(h => h.Owner == owner);
        }
        return null;
    }

    /// <summary>
    /// Get all running coroutine handles with the given name
    /// </summary>
    public List<CoroutineHandle> GetHandles(string name)
    {
        if (runningCoroutines.TryGetValue(name, out var list))
        {
            return new List<CoroutineHandle>(list);
        }
        return new List<CoroutineHandle>();
    }

    /// <summary>
    /// Get all running coroutine handles with the given name and owner
    /// </summary>
    public List<CoroutineHandle> GetHandles(string name, MonoBehaviour owner)
    {
        if (runningCoroutines.TryGetValue(name, out var list))
        {
            return list.Where(h => h.Owner == owner).ToList();
        }
        return new List<CoroutineHandle>();
    }

    public List<CoroutineHandle> GetAll()
    {
        var all = GetRunning();
#if DEVELOP_INFO
        all.AddRange(GetCompleted());
#endif
        return all;
    }

    /// <summary>
    /// 取得所有正在進行中的coroutine列表
    /// </summary>
    public List<CoroutineHandle> GetRunning()
    {
        return runningCoroutines.Values.SelectMany(list => list).ToList();
    }

    /// <summary>
    /// 取得特定owner正在進行中的coroutine列表
    /// </summary>
    public List<CoroutineHandle> GetRunningByOwner(MonoBehaviour owner)
    {
        return GetRunning().Where(c => c.Owner == owner).ToList();
    }

#if DEVELOP_INFO
    public List<CoroutineHandle> GetCompleted()
    {
        return completedCoroutines.Values.SelectMany(list => list).ToList();
    }

    public List<CoroutineHandle> GetCompletedByOwner(MonoBehaviour owner)
    {
        return GetCompleted().Where(c => c.Owner == owner).ToList();
    }

    public void ClearCompleted()
    {
        completedCoroutines.Clear();
    }

    public void ClearCompletedByOwner(MonoBehaviour owner)
    {
        foreach (var name in completedCoroutines.Keys.ToList())
        {
            var list = completedCoroutines[name];
            list.RemoveAll(h => h.Owner == owner);
            if (list.Count == 0)
            {
                completedCoroutines.Remove(name);
            }
        }
    }
#endif

    public int GetRunningCount()
    {
        return GetRunning().Count;
    }

#if DEVELOP_INFO
    public int GetCompletedCount()
    {
        return GetCompleted().Count;
    }

    public int GetTotalCount()
    {
        return GetRunningCount() + GetCompletedCount();
    }
#endif
}

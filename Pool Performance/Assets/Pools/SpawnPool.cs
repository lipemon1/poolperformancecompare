using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpawnPool : MonoBehaviour
{
    public string m_poolName;                   //The pool name (important to find them later)
    public int hash;
    public GameObject m_objectPrefab;           //The pool object
    string prefabPath;

    public bool m_createOnStart;                //Will create some instances on start?
    public bool m_adoptChildren;                //If there is any GO, adopt it as a Pool Object.

    public int m_startInstances = 1;                //Starting instances.
    public int m_extraInstances = 1;            //If we need more objects than we have created, create more X instances.
    public int m_maxSpawnedInstances = 0;                //Starting instances.

    private Queue<GameObject> m_poolObjects;            //Objects in the pool
    private List<GameObject> m_spawnedObjects;

    Vector3 originalScale = Vector3.zero;

    public Queue<GameObject> PoolObjects
    {
        get
        {
            if (m_poolObjects == null)
                m_poolObjects = new Queue<GameObject>();

            return m_poolObjects;
        }
    }

    public List<GameObject> SpawnedObjects
    {
        get
        {
            if (m_spawnedObjects == null)
                m_spawnedObjects = new List<GameObject>();

            return m_spawnedObjects;
        }
    }

    public string PrefabPath
    {
        get { return prefabPath; }
    }

    void Awake()
    {
        if (m_poolName == "")
            m_poolName = gameObject.name;

        transform.hierarchyCapacity = 50;
        transform.position = Vector3Utils.Pool;

        if (m_adoptChildren)
        {
            foreach (Transform child in transform)
            {
                if (child.gameObject.activeSelf)
                {
                    SpawnedObjects.Add(child.gameObject);

                    child.GetComponent<IPoolObject>()?.OnSpawn(this);

                    IPoolObject[] a = child.GetComponents<IPoolObject>();
                    if (a.Length > 0)
                        DebugMantis.LogError("NAO DEVERIA TER 2 IPOOLOBJECT COMPONENTS NO MESMO OBJETO ");
                    else
                        DebugMantis.Log("Ta certo..tem q remover essa parte da arrray de UPoolObject inteira");
                }
                else
                {
                    DebugMantis.LogError($"Passa por aqui? A condição é true? {!PoolObjects.Contains(child.gameObject)}");
                    if (!PoolObjects.Contains(child.gameObject))
                        PoolObjects.Enqueue(child.gameObject);
                }
            }
        }

        if (m_createOnStart)
            CreateInstances(m_startInstances);
    }

    public void Initialize(GameObject pPrefab, string pPoolName, int pStartInstances, int poolHashCode, string pPrefabPath, int instancesPerFrame = 1, Action callback = null)
    {
        hash = poolHashCode;
        prefabPath = pPrefabPath;
        Initialize(pPrefab, pPoolName, pStartInstances, instancesPerFrame, callback);
    }

    public void Initialize(GameObject pPrefab, string pPoolName, int pStartInstances, int instancesPerFrame = 1, Action callback = null)
    {
        m_objectPrefab = pPrefab;
        m_poolName = pPoolName;
        m_startInstances = pStartInstances;

        //DebugMantis.Log($"Creating pool: [{m_poolName}] - [{m_startInstances}] instances".ToColor(ColorUtils.Yellow));

        if (PoolManager.Current) // Fix temporario pra utilização da spawnPool em diferentes SkydomeSession
            PoolManager.Current.RegisterPool(m_poolName, this);

        CreateInstances(m_startInstances, instancesPerFrame, callback);
    }

    /// <summary>
    /// Instantiate the objects 
    /// </summary>
    /// <param name='fCount'>
    /// The number of new instances.
    /// </param>
    public void CreateInstances(float fCount, int instancesPerFrame = 1, Action callback = null)
    {
        if (fCount <= 0)
            fCount = 1;

        if (PoolManager.Current) // Fix temporario pra utilização da spawnPool em diferentes SkydomeSession
            PoolManager.Current.RegisterPool(m_poolName, this);

        Timing.KillCoroutines(timerRoutine);
        timerRoutine = Timing.RunCoroutine(CreateInstancesAsync((int)fCount, instancesPerFrame, callback));
    }

    public void AddInstances(int fCount, int instancesPerFrame = 1, Action callback = null)
    {
        m_startInstances += fCount;

        CreateInstances(fCount, instancesPerFrame, callback);
    }

    IEnumerator<float> CreateInstancesAsync(int amount, int instancesPerFrame, Action callback = null)
    {
        if (amount > instancesPerFrame)
            DebugMantis.LogWarning("Pool :" + m_poolName + " try to create " + amount + " instances. But is limited to " + instancesPerFrame + " to per frame. Spliting into multiple frames");

        int count = 0;
        for (int i = 0; i < amount; i++)
        {
            GameObject newInstance = Instantiate(m_objectPrefab, Vector3Utils.Pool, Quaternion.identity, transform);
            if (originalScale == Vector3.zero)
                originalScale = newInstance.transform.localScale;

            newInstance.transform.localPosition = Vector3.zero;
            newInstance.SetActive(false);
            PoolObjects.Enqueue(newInstance);

            count++;
            if (count >= instancesPerFrame)
            {
                yield return Timing.WaitForOneFrame;
                count = 0;
            }

        }

        callback?.Invoke();
    }

    public virtual GameObject GetNextToBeSpawned()
    {
        if (PoolObjects.Count == 0)
            CreateInstances(1);

        return PoolObjects.Peek();
    }



    /// <summary>
    /// Used only by NetworkPool.Current.cs - NOT ON CLIENT
    /// </summary>
    /// <param name="pPosition"></param>
    /// <param name="pRotation"></param>
    /// <param name="pNetworkInstantiated"></param>
    /// <returns></returns>
    public GameObject OnNetworkSpawn(Vector3 pPosition, Quaternion pRotation, int? viewId, GameServer server, ParamTable table)
    {
        return Spawn(pPosition, pRotation, viewId, server, table);
    }

    public GameObject Spawn(Vector3 pPosition, Quaternion pRotation = default(Quaternion))
    {
        return Spawn(pPosition, pRotation, null, null, null);
    }

    /// <summary>
    /// Search for an object in the pool and spawn it! Returns a GameObject!
    /// </summary>
    /// <param name='pPosition'>
    /// The position where the object will be created.
    /// </param>
    /// <param name='pRotation'>
    /// The rotation of the spawned object.
    /// </param>
    GameObject Spawn(Vector3 pPosition, Quaternion pRotation, int? viewId, GameServer server, ParamTable table)
    {
        GameObject go = null;
        try
        {
            if (PoolObjects.Count == 0)
                CreateInstances(m_extraInstances);

            if (PoolObjects.Count > 0)
            {
                go = PoolObjects.Dequeue();

                if (go != null)
                {
                    go.transform.position = pPosition;
                    go.transform.rotation = pRotation;
                    go.SetActive(true);

                    INetworkPoolObject[] objs = null;
                    if (viewId != null || table != null)
                    {
                        objs = go.GetComponents<INetworkPoolObject>();

                        for (int i = 0; i < objs.Length; i++)
                            objs[i].OnPreSpawn(server, viewId, table, true);
                    }

                    DespawnInMarkedObject mark = go.GetComponent<DespawnInMarkedObject>();
                    if (mark != null)
                        mark.Destroy();

                    IPoolObject[] poolObjects = go.GetComponents<IPoolObject>();
                    for (int i = 0; i < poolObjects.Length; i++)
                        poolObjects[i].OnSpawn(this);

                    if (objs != null)
                        for (int i = 0; i < objs.Length; i++)
                            objs[i].OnPostSpawn();

                    SpawnedObjects.Add(go);
                }

                if (m_maxSpawnedInstances < SpawnedObjects.Count)
                    m_maxSpawnedInstances = SpawnedObjects.Count;
                return go;
            }
        }
        catch (Exception ex)
        {
            DebugMantis.LogException(new Exception($"Failed to instantiate: {name}. Go:({go})", ex));
        }

        DebugMantis.Log("No more instances in the pool.");
        return null;
    }

    public GameObject Spawn()
    {
        return Spawn(Vector3.zero, Quaternion.identity);
    }

    /// <summary>
    /// Search for an object in the pool and spawn it! Returns a Generic!
    /// </summary>
    /// <param name='pPosition'>
    /// The position where the object will be created.
    /// </param>
    /// <param name='pRotation'>
    /// The rotation of the spawned object.
    /// </param>
    public T Spawn<T>(Vector3 pPosition, Quaternion pRotation) where T : Component
    {
        GameObject obj = Spawn(pPosition, pRotation);
        bool isIPoolObj = false;
        foreach (MonoBehaviour monoBehaviour in obj.GetComponents<MonoBehaviour>())
        {
            if (monoBehaviour.HasInterface<IPoolObject>(out _))
                isIPoolObj = true;
        }
        if (!isIPoolObj)
            obj.AddComponent<SimpleLocalPoolObject>();

        return obj.GetComponent<T>() as T;// ?? obj.AddComponent<SimpleLocalPoolObject>() as T;
    }

    public T Spawn<T>(Vector3 pPosition) where T : Component
    {
        return Spawn<T>(pPosition, Quaternion.identity);
    }

    public T Spawn<T>() where T : Component
    {
        return Spawn<T>(Vector3.zero, Quaternion.identity);
    }

    /// <summary>
    /// Disable the object and put it back in the pool.
    /// </summary>
    /// <param name='pPoolObject'>
    /// The object that will be despawned.
    /// </param>
    public void Despawn(GameObject pPoolObject)
    {
        if (SpawnedObjects.Contains(pPoolObject))
        {
            SpawnedObjects.Remove(pPoolObject);

            if (pPoolObject == null)
                return;

            foreach (IPoolObject obj in pPoolObject.GetComponents<IPoolObject>())
                obj.OnDespawn();

            DespawnInMarkedObject mark = pPoolObject.GetComponent<DespawnInMarkedObject>();
            mark?.Destroy();

            if (pPoolObject.transform != null)
            {
                if (transform != null)
                {
                    pPoolObject.transform.SetParent(transform);
                    if (pPoolObject.transform is RectTransform)
                        pPoolObject.transform.position = Vector2Utils.Pool;
                    else
                        pPoolObject.transform.position = Vector3Utils.Pool;
                    pPoolObject.transform.localScale = originalScale;
                }
            }

            if (pPoolObject != null)
            {
                pPoolObject.SetActive(false);
                Timing.CallDelayed(0.5f, () =>
                {
                    PoolObjects.Enqueue(pPoolObject);
                }, gameObject);
            }
            //pPoolObject.SetActive(false);
        }
        //else
        //    DebugMantis.LogWarning("Pool does not contain : " + pPoolObject.name);
    }

    private IEnumerator DespawnInCoroutine(GameObject pPoolObject, float pTime, Action destroyCallback = null)
    {
        //DespawnInMarkedObject obj = pPoolObject.GetOrAddComponent<DespawnInMarkedObject>();
        //obj.despawnTime = NetworkTime.Time + pTime;

        //yield return new WaitForSeconds(pTime);

        //DespawnInMarkedObject mark = pPoolObject.GetComponent<DespawnInMarkedObject>();

        ////0.3f eh o tempo limite que a pool considera pra que o objeto esteja no tempo certo de depawn dele, qt menor melhor, mas maior chance de nao despawnar
        //if (pPoolObject.activeSelf && mark != null && mark.despawnTime - NetworkTime.Time < 0.1f)
        //{
        //    Object.Destroy(mark);
        //    Despawn(pPoolObject);
        //}
        //else
        //    DebugMantis.LogWarning(obj.name + "DespawnTimeDifference: " + Mathf.Abs(mark.despawnTime - NetworkTime.Time) + ", not despawned! limit = 0.1f");     

        DespawnInMarkedObject mark = pPoolObject.GetOrAddComponentInChildren<DespawnInMarkedObject>();
        yield return new WaitForFixedSeconds(pTime);
        mark.Destroy();
        Despawn(pPoolObject);

        destroyCallback?.Invoke();
    }

    /// <summary>
    /// Despawn an object in X seconds
    /// </summary>
    /// <param name='pPoolObject'>
    /// The pool object.
    /// </param>
    /// <param name='pTime'>
    /// Time in seconds.
    /// </param>
    public Coroutine DespawnIn(GameObject pPoolObject, float pTime, Action destroyCallback = null)
    {
        if (pTime == Mathf.Infinity)
            return null;

        pPoolObject.GetOrAddComponentInChildren<DespawnInMarkedObject>().CreateCoroutine(DespawnInCoroutine(pPoolObject, pTime, destroyCallback));

        //If coroutine could be started because object is inactive. Destroy immediately
        if (pPoolObject.GetComponent<DespawnInMarkedObject>().coroutine == null)
            Despawn(pPoolObject);

        return pPoolObject.GetComponent<DespawnInMarkedObject>().coroutine;
    }

    public void CancelDespawnIn(Coroutine pCoroutine)
    {
        if (pCoroutine != null)
        {
            StopCoroutine(pCoroutine);
        }
    }

    /// <summary>
    /// Disable the first object in the pool.
    /// </summary>
    public void DespawnFirst()
    {
        if (SpawnedObjects.Count > 0)
            Despawn(SpawnedObjects[0]);
    }

    /// <summary>
    /// Disable all the object in the pool.
    /// </summary>
    public void DespawnAll()
    {
        while (SpawnedObjects.Count != 0)
            Despawn(SpawnedObjects[0]);
    }

    /// <summary>
    /// Destroys all the pool objects.
    /// </summary>
    public void DestroyPoolObjects()
    {
        foreach (GameObject go in PoolObjects)
            Destroy(go);

        PoolObjects.Clear();

        foreach (GameObject go in SpawnedObjects)
            Destroy(go);

        SpawnedObjects.Clear();
    }

    /// <summary>
    /// Return a list of spawned objects.
    /// </summary>
    /// <returns>The spawned objects.</returns>
    public List<GameObject> GetSpawnedObjects()
    {
        return SpawnedObjects;
    }

    void OnDestroy()
    {
        Timing.KillCoroutines(timerRoutine);

        foreach (GameObject spawnedObject in SpawnedObjects)
        {
            if (spawnedObject == null)
                continue;

            spawnedObject.GetComponent<DespawnInMarkedObject>()?.StopAllCoroutines();
        }

        if (PoolsUtils.GetPoolFlags())
        {
            if (m_startInstances < PoolObjects.Count)
            {
                DebugMantis.LogError("Pool size is wrong! Pool: " + m_poolName + " | Pool Init Size: " + m_startInstances + " | Pool Final Size: " + PoolObjects.Count);
            }
        }

        DestroyPoolObjects();
    }
}


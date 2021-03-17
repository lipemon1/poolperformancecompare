using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResourcesPool : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void LoadPoolFromResources()
    {
        GameObject rootsPrefab = Resources.Load<GameObject>("Rooter/Roots");
        rootsPool = PoolManager.Current.GetOrCreateLocalPool(rootsPrefab, 3);
    }
}

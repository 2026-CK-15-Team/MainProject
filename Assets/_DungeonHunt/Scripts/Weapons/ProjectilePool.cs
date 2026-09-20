using System.Collections.Generic;
using UnityEngine;

public class ProjectilePool : MonoBehaviour
{
    private static ProjectilePool instance;

    public static ProjectilePool Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("ProjectilePool (Auto)");
                instance = go.AddComponent<ProjectilePool>();
            }
            return instance;
        }
    }

    private readonly Dictionary<GameObject, Queue<GameObject>> pools = new();
    private readonly Dictionary<GameObject, GameObject> instanceToPrefab = new();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!pools.TryGetValue(prefab, out Queue<GameObject> queue))
        {
            queue = new Queue<GameObject>();
            pools[prefab] = queue;
        }

        GameObject obj;
        if (queue.Count > 0)
        {
            obj = queue.Dequeue();
        }
        else
        {
            obj = Instantiate(prefab);
            instanceToPrefab[obj] = prefab;
        }

        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);
        return obj;
    }

    public void Return(GameObject instance)
    {
        if (!instanceToPrefab.TryGetValue(instance, out GameObject prefab))
        {
            Destroy(instance); 
            return;
        }

        instance.SetActive(false);
        pools[prefab].Enqueue(instance);
    }
}
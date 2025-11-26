using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ServerMapSpawnPoint : MonoBehaviour
{
    [SerializeField] Transform SpawnParents;

    private void Awake()
    {
        RootManager.Instance.GameNetworkManager.SvMapSpawnList.Add(SpawnParents);
    }


}

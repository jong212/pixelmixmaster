using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ServerMapSpawnPoint : MonoBehaviour
{

    [SerializeField] Transform SpawnParents;
    [SerializeField] Transform EffectParents;

    private void Awake()
    {
        RootManager.Instance.GameNetworkManager.SvMapSpawnList.Add(SpawnParents);
        RootManager.Instance.GameNetworkManager.EffectPointList.Add(EffectParents);        
    }


}

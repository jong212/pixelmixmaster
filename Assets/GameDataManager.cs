using BACKND;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameDataManager : NetworkBehaviour
{
    
    public readonly SyncList<string> mapList = new SyncList<string>();

    [Server]
    public void ServerMapSet()
    {
        foreach( var map in mapList)
        {
            GameObject prefabs = RootManager.Instance.AddressableCDD.GetPrefab(map);
            if(prefabs != null)
            {
                Instantiate(prefabs, RootManager.Instance.GameNetworkManager.Mapparent);
            }
        }

    } 
    public override void OnStartClient()
    {
        base.OnStartClient();

        foreach (var map in mapList)
        {
            var a = RootManager.Instance.AddressableCDD.GetPrefab(map);
            if(a != null)
            {
                Debug.Log($"1_3아아���� ���� : Ŭ��ܿ��� {map}�ε��ؼ� ���ÿϷ�");
                Instantiate(a, RootManager.Instance.GameNetworkManager.Mapparent);
            }
        }
        // dd
    }

}

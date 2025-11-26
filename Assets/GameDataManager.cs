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
                Debug.Log($"1_3서버 실행 : 클라단에서 {map}로드해서 세팅완료");
                Instantiate(a, RootManager.Instance.GameNetworkManager.Mapparent);
            }
        }
        // 원하는 로직 실행
    }

}

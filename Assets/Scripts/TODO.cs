using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TODO : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

//1. SPUM Sample Scene 에서 캐릭  선택할때 스킬 선택화면 안 보일때 PlayerObj.cs 에서 Awake 되어있는거 Start로 바꿔줘야함
/*
 
 public Dictionary<PlayerState, int> IndexPair = new ();
    void Awake()
    {
        if(_prefabs == null )
        {
            _prefabs = transform.GetChild(0).GetComponent<SPUM_Prefabs>();
            if(!_prefabs.allListsHaveItemsExist()){
                _prefabs.PopulateAnimationLists();
            }
        }
        _prefabs.OverrideControllerInit();
        foreach (PlayerState state in Enum.GetValues(typeof(PlayerState)))
        {
            IndexPair[state] = 0;
        }
    }

*/

//2. 뒤끝 차트 인벤 에서 AnimIdx는 공격 애니메이션 인덱스임
/*
0 = 기본 소드 공격
5 = 원거리 공격
*/


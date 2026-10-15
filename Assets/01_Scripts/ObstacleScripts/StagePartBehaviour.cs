using Mirror;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// 스테이지에 중첩하는 부품 프리팹용 NetworkBehaviour.
// NetworkIdentity는 스테이지 루트에만 있으므로, 부품 프리팹을 단독으로 임포트·편집할 때
// Mirror OnValidate의 "requires a NetworkIdentity" 에러가 매번 쌓인다. 그 경우만 검사를 건너뛰고,
// 씬에 NetworkIdentity 없이 놓인 경우는 그대로 에러를 남긴다.
public abstract class StagePartBehaviour : NetworkBehaviour
{
    protected override void OnValidate()
    {
#if UNITY_EDITOR
        if (IsStandalonePartPrefab()) return;
#endif
        base.OnValidate();
    }

#if UNITY_EDITOR
    private bool IsStandalonePartPrefab()
    {
        if (GetComponentInParent<NetworkIdentity>(true) != null) return false;

        return PrefabUtility.IsPartOfPrefabAsset(this)
            || PrefabStageUtility.GetPrefabStage(gameObject) != null;
    }
#endif
}

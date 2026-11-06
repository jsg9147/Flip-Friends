using UnityEngine;

public class DetectPlatformResetBtn : LayerBasedSwitch
{
    public PlayerDetectPlatform detectPlatform;

    // 기존 일반 콜라이더 버튼은 아래에서 머리로 누른다. 트리거형은 부모의 접촉 집합을 쓴다.
    protected override bool HasAdditionalContact => boxCollider != null && !boxCollider.isTrigger && HasPlayerBelow();

    protected override void OnSwitchStateChanged(bool newState)
    {
        if (!isServer || !newState) return;
        if (detectPlatform == null)
        {
            Debug.LogError($"{transform.root.name}/{name}: 리셋할 발판이 연결되지 않았습니다.", this);
            return;
        }
        detectPlatform.ResetPlatformState();
    }
}

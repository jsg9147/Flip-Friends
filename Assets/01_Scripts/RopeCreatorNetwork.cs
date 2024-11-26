using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class RopeCreatorNetwork : NetworkBehaviour
{
    [SerializeField]
    private GameObject ropePrefab; // 로프 프리팹 설정

    [SerializeField, Min(1)]
    private int ropeLength = 5; // 로프 길이 설정 (최소값 1)

    private void Start()
    {
        if (isServer) // 서버에서만 실행
        {
            CreateRope();
        }
    }

    private void CreateRope()
    {
        if (ropePrefab == null)
        {
            Debug.LogError("Rope Prefab이 설정되지 않았습니다.");
            return;
        }

        for (int i = 0; i < ropeLength; i++)
        {
            // 로프 조각 위치 계산
            Vector3 ropePosition = new Vector3(transform.position.x, transform.position.y - i, transform.position.z);

            // 로프 조각 생성
            GameObject ropeSegment = Instantiate(ropePrefab, ropePosition, Quaternion.identity, transform);

            // 네트워크에 생성된 객체 등록
            NetworkServer.Spawn(ropeSegment);
        }
    }

    // 씬 뷰에서 로프의 위치를 시각적으로 표시
    private void OnDrawGizmos()
    {
        if (ropeLength <= 0) return;

        Gizmos.color = Color.yellow;

        // 로프의 시작과 끝 위치를 선으로 연결
        Vector3 startPosition = transform.position;
        Vector3 endPosition = startPosition + Vector3.down * (ropeLength - 1);
        Gizmos.DrawLine(startPosition, endPosition);

        // 각 로프 세그먼트의 위치에 구체를 그림
        for (int i = 0; i < ropeLength; i++)
        {
            Vector3 position = new Vector3(transform.position.x, transform.position.y - i, transform.position.z);
            Gizmos.DrawSphere(position, 0.1f);
        }
    }

    // 에디터에서 값 검증
    protected override void OnValidate()
    {
        base.OnValidate();
        if (ropeLength < 1)
        {
            Debug.LogWarning("Rope Length는 최소 1 이상이어야 합니다. 1로 초기화합니다.");
            ropeLength = 1;
        }

        if (ropePrefab == null)
        {
            Debug.LogWarning("Rope Prefab이 설정되지 않았습니다. 프리팹을 설정해주세요.");
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class PlayerDetectPlatform : PlatformController
{
    public float firstWaitTime;

    // 나중에 rigidbody 로 이동하게 변경하자. 충돌 여부 변경시키면 가능할꺼 같음, 지금은 일딴 기능만 추가
    public bool disapearObject;

    bool firstWait;

    int beforeIndex;
    bool isDisapear;

    private bool moveStart = false;

    public override void Start()
    {
        base.Start();

        firstWait = true;
        beforeIndex = 0;
        isDisapear = false;
    }

    // Update is called once per frame
    public override void FixedUpdate()
    {
        if (isServer)
        {
            if (IsMove())
            {
                base.FixedUpdate();
            }
            if (isComplite && disapearObject)
            {
                Disapear();
            }
        }
    }

    bool IsMove()
    {
        if (isComplite)
            return false;

        bool isMove = false;

        if (moveStart)
        {
            isMove = true;
        }

        if (firstWait && isMove)
        {
            nextMoveTime = Time.time + firstWaitTime;
            firstWait = false;
        }

        if (isCycled)
        {
            moveStart = false;
            isCycled = false;
            isMove = false;
        }

        if (beforeIndex < fromWaypointIndex)
            beforeIndex = fromWaypointIndex;

        return isMove;
    }

    void Disapear()
    {
        if (isDisapear)
            return;

        gameObject.SetActive(false);
        isDisapear = true;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isServer)
        {
            if (collision.gameObject.tag == "Player")
            {
                if (collision.transform.position.y > transform.position.y)
                    moveStart = true;
            }
        }
    }
}

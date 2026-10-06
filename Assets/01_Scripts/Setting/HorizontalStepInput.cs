using UnityEngine;
using UnityEngine.EventSystems;

// InputManager.dir은 누르고 있는 동안 계속 값이 남는다. 매 프레임 그대로 쓰면 한 번 눌러도 여러 칸을 건너뛰므로
// 누른 순간 한 칸, 반복을 켠 경우에만 첫 지연 뒤 일정 간격으로 한 칸씩 넘겨준다
public class HorizontalStepInput
{
    private readonly bool isRepeating;
    private readonly float initialDelay;
    private readonly float repeatInterval;

    private int heldDirection;
    private float nextRepeatTime;

    private HorizontalStepInput(bool isRepeating, float initialDelay, float repeatInterval)
    {
        this.isRepeating = isRepeating;
        this.initialDelay = initialDelay;
        this.repeatInterval = repeatInterval;
    }

    public static HorizontalStepInput PressOnly()
    {
        return new HorizontalStepInput(false, 0f, 0f);
    }

    public static HorizontalStepInput Repeating(float initialDelay, float repeatInterval)
    {
        return new HorizontalStepInput(true, initialDelay, repeatInterval);
    }

    // targetUI가 선택돼 있을 때만 좌우 입력을 받아 이번 프레임에 적용할 방향(-1, 0, 1)을 돌려준다
    public int Read(GameObject targetUI)
    {
        // 선택이 풀리면 누름 상태를 지운다. 다시 선택됐을 때 이전 누름이 이어지지 않게 하기 위함이다
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject != targetUI)
        {
            heldDirection = 0;
            return 0;
        }

        return Step(InputManager.instance.dir.x);
    }

    private int Step(float horizontal)
    {
        int direction = horizontal > 0f ? 1 : horizontal < 0f ? -1 : 0;

        if (direction == 0)
        {
            heldDirection = 0;
            return 0;
        }

        // 일시정지 중에도 설정 화면을 열 수 있어 timeScale에 영향받지 않는 시간을 쓴다
        float now = Time.unscaledTime;

        if (direction != heldDirection)
        {
            heldDirection = direction;
            nextRepeatTime = now + initialDelay;
            return direction;
        }

        if (!isRepeating || now < nextRepeatTime)
        {
            return 0;
        }

        nextRepeatTime = now + repeatInterval;
        return direction;
    }
}

using System;
using System.Linq;
using Photon.Deterministic;
using Quantum;

internal static class QuantumConveyorDirectChecks
{
    private delegate void Edit<T>(ref T value);

    // 재생 비교를 끝낸 일회용 러너의 최종 프레임에서 벨트 경계를 직접 검사한다. 원본 규칙의 FP 기대값과 바로 비교한다.
    public static int Run(Frame frame)
    {
        int checks = 0;
        void Check(bool valid, string why)
        {
            if (!valid) throw new InvalidOperationException("벨트 직접 검사: " + why);
            checks++;
        }
        var belts = frame.RuntimeConfig.Conveyors;
        int Belt(string name) => Array.FindIndex(belts, b => b.Id.EndsWith("/" + name, StringComparison.Ordinal));
        int fixedIndex = Belt("고정 벨트"), manualIndex = Belt("수동 벨트"), mixedIndex = Belt("혼합 벨트"), movingIndex = Belt("이동 벨트");
        Check(fixedIndex >= 0 && manualIndex >= 0 && mixedIndex >= 0 && movingIndex >= 0, "시험 벨트 4종");
        EntityRef BeltEntity(int index)
        {
            var q = frame.Filter<FlipFriendsConveyor>();
            while (q.Next(out var e, out var b)) if (b.ContentIndex == index) return e;
            throw new InvalidOperationException("벨트 프레임 상태 누락");
        }
        FlipFriendsConveyor BeltState(int index) => frame.Get<FlipFriendsConveyor>(BeltEntity(index));
        FlipFriendsMotion MotionOf(int index) => frame.Get<FlipFriendsMotion>(BeltState(index).Motion);
        FPVector2 HalfOf(int index) => frame.RuntimeConfig.Motions[belts[index].Motion].HalfSize;

        frame.FlipFriendsCompletedFrame = 0;
        var players = frame.Filter<FlipFriendsPlayer>();
        players.Next(out var first, out _);
        players.Next(out var second, out _);
        var boxes = frame.Filter<FlipFriendsBox>();
        boxes.Next(out var box, out _);
        var dt = frame.DeltaTime;
        var half = new FPVector2(FP.FromString("0.4"), FP._0_50);
        var parking = new FPVector2(-10, FP._0_50);
        var system = new FlipFriendsMotionSystem();
        var boxPark = new FPVector2(FP.FromString("-7.5"), FP.FromString("0.45"));

        FlipFriendsPlayer P(EntityRef e) => frame.Get<FlipFriendsPlayer>(e);
        Transform2D T(EntityRef e) => frame.Get<Transform2D>(e);
        void SetPos(EntityRef e, FPVector2 position) { var t = T(e); t.Position = position; frame.Set(e, t); }
        void EditP(EntityRef e, Edit<FlipFriendsPlayer> edit) { var p = P(e); edit(ref p); frame.Set(e, p); }
        void Prepare(EntityRef e, FPVector2 position, bool grounded, FP external, int contact)
        {
            var old = P(e);
            frame.Set(e, new FlipFriendsPlayer { Player = old.Player, HomePosition = old.HomePosition, RespawnPosition = old.RespawnPosition, Facing = 1,
                IsGrounded = grounded, ConveyorVelocityX = external, ConveyorContact = contact, PreviousPosition = position });
            SetPos(e, position);
        }
        void Step(EntityRef e, Input input = default)
        {
            var t = T(e); var p = P(e);
            FlipFriendsMovementSystem.Simulate(frame, ref t, ref p, input);
            frame.Set(e, t); frame.Set(e, p);
        }
        FPVector2 OnTop(int index, FP dx) => MotionOf(index).Position + new FPVector2(dx, HalfOf(index).Y + half.Y);
        Prepare(second, parking - new FPVector2(1, 0), true, 0, 0);
        SetPos(box, boxPark);

        // 반시계(오른쪽) 고정 벨트에서 초당 1 가속·틱당 50Hz 환산 가산·위 접촉 유지.
        bool fixedClockwise = BeltState(fixedIndex).Clockwise;
        FP sign = fixedClockwise ? -1 : 1;
        Prepare(first, OnTop(fixedIndex, 0), true, 0, fixedIndex + 1);
        Step(first);
        var expected = sign * dt;
        Check(P(first).ConveyorVelocityX == expected && P(first).Velocity.X == expected * 50 * dt, "가속 1/초와 50Hz 환산 가산");
        Check(P(first).ConveyorContact == fixedIndex + 1 && P(first).IsGrounded, "윗면 접촉 유지");
        Prepare(first, OnTop(fixedIndex, 0), true, sign * (1 - dt / 2), fixedIndex + 1);
        Step(first);
        Check(P(first).ConveyorVelocityX == sign, "외부 속도 ±1 상한");
        // 역방향 입력이어도 원본처럼 누적 외부 속도가 플레이어를 벨트 방향으로 민다.
        Prepare(first, OnTop(fixedIndex, -FP._0_50), true, sign, fixedIndex + 1);
        var start = T(first).Position.X;
        for (int i = 0; i < 10; i++) Step(first, new Input { Horizontal = -sign });
        Check((T(first).Position.X - start) * sign > 0 && P(first).Velocity.X * sign > 0, "역방향 입력 중 벨트 방향 이동");

        // 이탈 감쇠: 지면 Lerp(dt×5), 공중 Lerp(dt). 접촉은 이동 뒤 다시 계산한다.
        var external = FP.FromString("0.6");
        Prepare(first, parking, true, external, 0);
        Step(first);
        Check(P(first).ConveyorVelocityX == external - external * (dt * 5) && P(first).ConveyorContact == 0, "지면 감쇠");
        Prepare(first, parking + new FPVector2(0, 5), false, external, 0);
        Step(first);
        Check(P(first).ConveyorVelocityX == external - external * dt && P(first).ConveyorContact == 0, "공중 감쇠와 외부 속도 유지");

        // 원본은 머리 위 세로 충돌의 벨트도 접촉으로 본다. 옆면 접촉은 접촉이 아니다.
        var moving = MotionOf(movingIndex);
        var movingHalf = HalfOf(movingIndex);
        Prepare(first, moving.Position - new FPVector2(0, movingHalf.Y + half.Y + FP.FromString("0.05")), false, 0, 0);
        EditP(first, (ref FlipFriendsPlayer p) => p.Velocity.Y = 6);
        Step(first);
        Check(P(first).IsTouchingCeiling && P(first).ConveyorContact == movingIndex + 1, "천장 접촉");
        Prepare(first, moving.Position - new FPVector2(movingHalf.X + half.X + FP.FromString("0.01"), 0), false, 0, 0);
        EditP(first, (ref FlipFriendsPlayer p) => p.Velocity = new FPVector2(4, 0));
        Step(first, new Input { Horizontal = 1 });
        Check(P(first).ConveyorContact == 0, "옆면은 비접촉");

        // 운반된 플레이어는 이동 시뮬레이션을 건너뛰고, 들기·던지기·복귀·도착은 외부 속도와 접촉을 버린다.
        Prepare(first, OnTop(fixedIndex, 0), true, sign, fixedIndex + 1);
        Prepare(second, OnTop(fixedIndex, 0) + new FPVector2(FP.FromString("0.8"), 0), true, sign, fixedIndex + 1);
        Check(FlipFriendsInteractionSystem.TryPickup(frame, first) && P(first).Held == second && P(second).ConveyorVelocityX == 0 && P(second).ConveyorContact == 0, "들기 시 초기화");
        EditP(second, (ref FlipFriendsPlayer p) => p.ConveyorVelocityX = sign);
        EditP(second, (ref FlipFriendsPlayer p) => p.ConveyorContact = fixedIndex + 1);
        Step(second);
        Check(P(second).ConveyorVelocityX == sign, "운반 중 벨트 미적용");
        Step(first);
        Check(P(first).ConveyorVelocityX == sign && P(first).ConveyorContact == fixedIndex + 1, "운반자는 벨트 적용");
        Check(FlipFriendsInteractionSystem.Release(frame, first, false) && P(second).ConveyorVelocityX == 0 && P(second).ConveyorContact == 0, "던지기 시 초기화");
        EditP(first, (ref FlipFriendsPlayer p) => p.ConveyorVelocityX = sign);
        FlipFriendsRespawnSystem.QueuePlayer(frame, first);
        Check(P(first).ConveyorVelocityX == 0 && P(first).ConveyorContact == 0, "복귀 시 초기화");
        var goal = frame.RuntimeConfig.GoalCenters[0];
        Prepare(first, new FPVector2(goal.X, FP._0_50), true, sign, 0);
        Step(first, new Input { Vertical = 1 });
        Check(P(first).IsFinished && P(first).ConveyorVelocityX == 0, "도착 시 초기화");
        EditP(first, (ref FlipFriendsPlayer p) => p.IsFinished = false);

        // 느슨한 상자는 벨트 위에 고체로 서지만 외부 속도를 받지 않는다.
        var boxState = frame.Get<FlipFriendsBox>(box);
        boxState = new FlipFriendsBox { ContentIndex = boxState.ContentIndex, HalfSize = boxState.HalfSize, HomePosition = boxState.HomePosition, RespawnPosition = boxState.HomePosition };
        SetPos(box, MotionOf(fixedIndex).Position + new FPVector2(0, HalfOf(fixedIndex).Y + boxState.HalfSize.Y + FP.FromString("0.1")));
        Prepare(first, parking, true, 0, 0);
        Prepare(second, parking - new FPVector2(1, 0), true, 0, 0);
        var boxStart = T(box).Position;
        for (int i = 0; i < 30; i++)
        {
            var bt = T(box);
            FlipFriendsBoxSystem.Simulate(frame, box, ref bt, ref boxState);
            frame.Set(box, bt); frame.Set(box, boxState);
        }
        Check(T(box).Position.X == boxStart.X && boxState.IsGrounded &&
            T(box).Position.Y == MotionOf(fixedIndex).Position.Y + HalfOf(fixedIndex).Y + boxState.HalfSize.Y, "느슨한 상자 고체 착지·외부 속도 없음");
        SetPos(box, boxPark);

        // 이동 벨트: 발판 변위(운반)는 이동 장치가 먼저 적용하고, 벨트 외부 속도는 이동 시스템 속도로만 더한다.
        // 같은 프레임 번호에서 다시 갱신하도록 이동 장치의 마지막 갱신 틱을 지운다.
        void Advance() { var m = BeltState(movingIndex).Motion; var ms = frame.Get<FlipFriendsMotion>(m); ms.LastUpdateFrame = 0; frame.Set(m, ms); system.Update(frame); }
        for (int i = 0; i < 4 && MotionOf(movingIndex).Delta == default; i++) Advance();
        Prepare(first, OnTop(movingIndex, 0), true, FP._0_50 * (BeltState(movingIndex).Clockwise ? -1 : 1), movingIndex + 1);
        var before = T(first).Position;
        Advance();
        var platformDelta = MotionOf(movingIndex).Delta;
        var afterTransport = T(first).Position;
        Step(first);
        Check(platformDelta != default && afterTransport == before + platformDelta, "발판 운반 먼저 적용");
        Check(T(first).Position.X - afterTransport.X == P(first).Velocity.X * dt && P(first).ConveyorContact == movingIndex + 1,
            "외부 속도는 한 번만 더함·이동 벨트 접촉 유지");

        // 벨트만 있는 수동 그룹: 같은 버튼 두 명은 한 번, 반전은 같은 틱에 방향에 반영된다.
        var switches = frame.RuntimeConfig.StageSwitches;
        int buttonIndex = Array.FindIndex(switches, s => s.Target < 0);
        var button = switches[buttonIndex];
        EntityRef ButtonEntity(int index)
        {
            var q = frame.Filter<FlipFriendsStageSwitch>();
            while (q.Next(out var e, out var s)) if (s.ContentIndex == index) return e;
            throw new InvalidOperationException("스위치 상태 누락");
        }
        var buttonEntity = ButtonEntity(buttonIndex);
        void Press()
        {
            var below = button.Center - new FPVector2(0, button.HalfSize.Y + half.Y);
            Prepare(first, below, false, 0, 0); EditP(first, (ref FlipFriendsPlayer p) => p.IsTouchingCeiling = true);
            Prepare(second, below, false, 0, 0); EditP(second, (ref FlipFriendsPlayer p) => p.IsTouchingCeiling = true);
            var b = frame.Get<FlipFriendsStageSwitch>(buttonEntity); b.ReadyFrame = 0; frame.Set(buttonEntity, b);
        }
        var conveyors = (FlipFriendsConveyorConfig[])belts.Clone();
        frame.RuntimeConfig.Conveyors = conveyors;
        bool manualBefore = BeltState(manualIndex).Clockwise;
        int revision = BeltState(manualIndex).Revision;
        Press();
        system.Update(frame);
        Check(frame.Get<FlipFriendsStageSwitch>(buttonEntity).Contacts == 2 && BeltState(manualIndex).Clockwise != manualBefore &&
            BeltState(manualIndex).Revision == revision + 1, "두 명 동시 접촉도 한 번·같은 틱 방향 반전");
        Check(frame.Get<FlipFriendsStageSwitch>(buttonEntity).Active == (BeltState(manualIndex).Clockwise != conveyors[manualIndex].InitialClockwise), "버튼 표시가 그룹 상태와 일치");
        system.Update(frame);
        Check(BeltState(manualIndex).Clockwise != manualBefore, "접촉 유지 중 쿨다운");
        conveyors[manualIndex].GroupTicks = frame.Number - frame.FlipFriendsMotionStartedFrame;
        bool manualNow = BeltState(manualIndex).Clockwise;
        Press();
        system.Update(frame);
        Check(BeltState(manualIndex).Clockwise == manualNow, "벨트 전용 그룹의 자동·수동 동일 틱 상쇄");
        conveyors[manualIndex].GroupTicks = 0;
        Prepare(first, parking, true, 0, 0);
        Prepare(second, parking - new FPVector2(1, 0), true, 0, 0);
        system.Update(frame);
        frame.RuntimeConfig.StageSwitches = switches.Append(button).ToArray();
        var duplicate = frame.Create();
        frame.Add(duplicate, new FlipFriendsStageSwitch { ContentIndex = switches.Length });
        manualNow = BeltState(manualIndex).Clockwise;
        Press();
        system.Update(frame);
        Check(BeltState(manualIndex).Clockwise == manualNow, "서로 다른 버튼의 두 요청 상쇄");
        frame.Destroy(duplicate);
        frame.RuntimeConfig.StageSwitches = switches;

        // 지형과 묶인 벨트는 지형의 적용 상태를 따른다. 활성화가 점유로 대기하면 벨트 방향도 대기한다.
        var motionConfigs = (FlipFriendsMotionConfig[])frame.RuntimeConfig.Motions.Clone();
        frame.RuntimeConfig.Motions = motionConfigs;
        int terrain = belts[mixedIndex].TerrainAnchor;
        motionConfigs[terrain].ToggleTicks = 0;
        EntityRef TerrainEntity()
        {
            var q = frame.Filter<FlipFriendsMotion>();
            while (q.Next(out var e, out var s)) if (s.ContentIndex == terrain) return e;
            throw new InvalidOperationException("지형 상태 누락");
        }
        var terrainEntity = TerrainEntity();
        FlipFriendsMotion Terrain() => frame.Get<FlipFriendsMotion>(terrainEntity);
        void FlipTerrain() { var t = Terrain(); t.Cycle ^= 1; frame.Set(terrainEntity, t); }
        Prepare(first, parking, true, 0, 0);
        Prepare(second, parking - new FPVector2(1, 0), true, 0, 0);
        system.Update(frame);
        if (!Terrain().Collidable) { FlipTerrain(); system.Update(frame); }
        Check(Terrain().Collidable, "지형 활성 준비");
        bool mixedBefore = BeltState(mixedIndex).Clockwise;
        FlipTerrain();
        system.Update(frame);
        Check(!Terrain().Collidable && BeltState(mixedIndex).Clockwise != mixedBefore, "지형 비활성과 같은 틱 벨트 반전");
        SetPos(box, Terrain().Position);
        FlipTerrain();
        system.Update(frame);
        Check(!Terrain().Collidable && BeltState(mixedIndex).Clockwise != mixedBefore, "점유 대기 중 벨트 방향 유지");
        SetPos(box, boxPark);
        system.Update(frame);
        Check(Terrain().Collidable && BeltState(mixedIndex).Clockwise == mixedBefore, "점유 해소 틱에 지형과 벨트 함께 적용");
        Check(BeltState(fixedIndex).Clockwise == fixedClockwise && BeltState(fixedIndex).Revision == 0, "그룹 없는 벨트 방향 고정");
        frame.RuntimeConfig.Motions = (FlipFriendsMotionConfig[])motionConfigs.Clone();
        return checks;
    }
}

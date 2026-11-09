using System;
using System.Linq;
using Photon.Deterministic;
using Quantum;

internal static class QuantumSwitchDirectChecks
{
    // 재생 비교를 끝낸 일회용 러너의 최종 프레임에서 판정 경계를 직접 검사한다.
    public static int Run(Frame frame)
    {
        var configs = frame.RuntimeConfig.StageSwitches;
        int doorIndex = Array.FindIndex(configs, c => c.IsDoor);
        int mixedIndex = Array.FindIndex(configs, c => frame.RuntimeConfig.Motions[c.Target].ToggleGroup == "mixed");
        var door = configs[doorIndex];
        var mixed = configs[mixedIndex];
        var doorEntity = Motion(frame, door.Target);
        var mixedEntity = Motion(frame, mixed.Target);
        var buttonEntity = Switch(frame, mixedIndex);
        var contactsEntity = Switch(frame, doorIndex);
        FlipFriendsMotion Door() => frame.Get<FlipFriendsMotion>(doorEntity);
        FlipFriendsMotion Mixed() => frame.Get<FlipFriendsMotion>(mixedEntity);
        FlipFriendsStageSwitch Button() => frame.Get<FlipFriendsStageSwitch>(buttonEntity);
        int Contacts() => frame.Get<FlipFriendsStageSwitch>(contactsEntity).Contacts;
        void Ready(int value)
        {
            var button = Button();
            button.ReadyFrame = value;
            frame.Set(buttonEntity, button);
        }
        void Place(EntityRef entity, FPVector2 position)
        {
            var transform = frame.Get<Transform2D>(entity);
            transform.Position = position;
            frame.Set(entity, transform);
        }
        var system = new FlipFriendsMotionSystem();
        frame.FlipFriendsCompletedFrame = 0;
        var motionConfigs = (FlipFriendsMotionConfig[])frame.RuntimeConfig.Motions.Clone();
        frame.RuntimeConfig.Motions = motionConfigs;
        motionConfigs[mixed.Target].ToggleTicks = 0;
        var players = frame.Filter<FlipFriendsPlayer>();
        players.Next(out var first, out _);
        players.Next(out var second, out _);
        var p1 = frame.Get<FlipFriendsPlayer>(first);
        var p2 = frame.Get<FlipFriendsPlayer>(second);
        p1.IsFinished = p2.IsFinished = false;
        frame.Set(first, p1);
        frame.Set(second, p2);
        var boxes = frame.Filter<FlipFriendsBox>();
        boxes.Next(out var box, out _);
        var boxState = frame.Get<FlipFriendsBox>(box);
        var outside = new FPVector2(-11, 8);
        int checks = 0;
        void Check(bool valid, string why)
        {
            if (!valid) throw new InvalidOperationException("스위치 직접 검사: " + why);
            checks++;
        }

        Place(first, door.Center);
        Place(second, door.Center);
        system.Update(frame);
        Check(Contacts() == 2 && !Door().Collidable, "두 접촉으로 열림");
        Place(first, outside);
        system.Update(frame);
        Check(Contacts() == 1 && !Door().Collidable, "마지막 접촉까지 열림");
        var insideDoor = Door().Position + new FPVector2(0, 1);
        Place(second, insideDoor);
        system.Update(frame);
        Check(Contacts() == 0 && !Door().Collidable, "문 안 플레이어가 닫힘 보류");
        Place(second, outside);
        Place(box, insideDoor);
        system.Update(frame);
        Check(!Door().Collidable, "느슨한 상자가 닫힘 보류");
        p1.Held = box;
        boxState.Carrier = first;
        frame.Set(first, p1);
        frame.Set(box, boxState);
        system.Update(frame);
        Check(!Door().Collidable, "운반 중 상자가 닫힘 보류");
        p1.Held = second;
        p2.Carrier = first;
        boxState.Carrier = default;
        frame.Set(first, p1);
        frame.Set(second, p2);
        frame.Set(box, boxState);
        Place(box, outside);
        Place(second, insideDoor);
        system.Update(frame);
        Check(!Door().Collidable, "운반 중 플레이어가 닫힘 보류");
        p1.Held = p2.Carrier = default;
        frame.Set(first, p1);
        frame.Set(second, p2);
        Place(second, outside);
        system.Update(frame);
        Check(Door().Collidable, "점유 해소 뒤 닫힘");
        Place(box, door.Center);
        system.Update(frame);
        Check(!Door().Collidable && Contacts() == 1, "상자 감지로 다시 열림");
        Place(box, outside);

        var below = mixed.Center - new FPVector2(0, mixed.HalfSize.Y + FlipFriendsInteractionSystem.HalfSize(p1).Y);
        Place(first, below);
        Place(second, below);
        p1.IsTouchingCeiling = p2.IsTouchingCeiling = true;
        frame.Set(first, p1);
        frame.Set(second, p2);
        Ready(0);
        var mixedState = Mixed();
        mixedState.Cycle = 0;
        frame.Set(mixedEntity, mixedState);
        system.Update(frame);
        Check(Button().Contacts == 2 && Mixed().Cycle == 1, "같은 버튼에 두 명이 닿아도 한 번");
        system.Update(frame);
        Check(Mixed().Cycle == 1, "접촉 유지 중 쿨다운");
        Ready(frame.Number);
        system.Update(frame);
        Check(Mixed().Cycle == 0, "쿨다운 후 계속 눌러 다시 반전");
        Place(first, outside);
        Place(second, outside);
        system.Update(frame);
        Check(Button().Contacts == 0 && Mixed().Cycle == 0, "이탈 시 반전 없음");
        Place(first, below);
        p1.RespawnFrame = frame.Number;
        frame.Set(first, p1);
        Ready(0);
        system.Update(frame);
        Check(Button().Contacts == 0 && Mixed().Cycle == 0, "복귀 틱의 이전 접촉 무시");
        p1.RespawnFrame = 0;
        frame.Set(first, p1);
        motionConfigs[mixed.Target].ToggleTicks = frame.Number - frame.FlipFriendsMotionStartedFrame;
        system.Update(frame);
        Check(Mixed().Cycle == 1 && Mixed().Collidable, "자동과 수동 동일 틱의 두 반전 상쇄");

        frame.RuntimeConfig.StageSwitches = configs.Append(mixed).ToArray();
        var duplicate = frame.Create();
        frame.Add(duplicate, new FlipFriendsStageSwitch { ContentIndex = configs.Length });
        Ready(0);
        system.Update(frame);
        Check(Mixed().Cycle == 1 && Mixed().Collidable,
            "서로 다른 스위치의 두 요청 상쇄");

        int manualIndex = Array.FindIndex(configs, c => frame.RuntimeConfig.Motions[c.Target].ToggleGroup == "manual");
        var manual = configs[manualIndex];
        var manualButton = Switch(frame, manualIndex);
        int initiallySolid = Array.FindIndex(motionConfigs, c => c.ToggleGroup == "manual" && c.InitialActive);
        int initiallyOpen = Array.FindIndex(motionConfigs, c => c.ToggleGroup == "manual" && !c.InitialActive);
        bool Solid(int index) => frame.Get<FlipFriendsMotion>(Motion(frame, index)).Collidable;
        Place(first, manual.Center - new FPVector2(0, manual.HalfSize.Y + FlipFriendsInteractionSystem.HalfSize(p1).Y));
        Place(box, motionConfigs[initiallySolid].Origin);
        system.Update(frame);
        Check(!Solid(initiallySolid) && Solid(initiallyOpen), "활성화 예정 지형에 상자가 있으면 그룹 전체 대기");
        var manualState = frame.Get<FlipFriendsStageSwitch>(manualButton);
        manualState.ReadyFrame = frame.Number;
        frame.Set(manualButton, manualState);
        system.Update(frame);
        Place(first, outside);
        Place(box, outside);
        system.Update(frame);
        Check(!Solid(initiallySolid) && Solid(initiallyOpen), "상쇄된 점유 대기 요청은 이탈 뒤 재실행되지 않음");

        var origin = Door().Position - new FPVector2(FP.FromString("1.5"), 0);
        var platform = new FlipFriendsMotionConfig
        {
            Kind = 0, Origin = origin, HalfSize = new FPVector2(FP._0_25, FP._0_25),
            Speed = 20, SegmentTicks = 3, Cyclic = true,
            Points = new[] { FPVector2.Zero, new FPVector2(6, 0) },
            Ease = Enumerable.Range(0, 257).Select(i => (FP)i / 256).ToArray(),
        };
        int platformIndex = frame.RuntimeConfig.Motions.Length;
        frame.RuntimeConfig.Motions = frame.RuntimeConfig.Motions.Append(platform).ToArray();
        var platformEntity = frame.Create();
        frame.Add(platformEntity, new FlipFriendsMotion { ContentIndex = platformIndex, Position = origin, Phase = 2, Collidable = true });
        system.Update(frame);
        var platformState = frame.Get<FlipFriendsMotion>(platformEntity);
        Check(platformState.Position == origin && platformState.BlockedTicks == 1, "닫힌 문을 향하는 발판 이동 보류");
        var doorState = Door();
        doorState.Collidable = false;
        frame.Set(doorEntity, doorState);
        platformState.Position = doorState.Position;
        frame.Set(platformEntity, platformState);
        system.Update(frame);
        Check(!Door().Collidable, "이동 발판과 겹치는 문 닫힘 보류");
        return checks;
    }

    private static EntityRef Motion(Frame frame, int index)
    {
        var states = frame.Filter<FlipFriendsMotion>();
        while (states.Next(out var entity, out var state))
            if (state.ContentIndex == index) return entity;
        throw new InvalidOperationException("전환 프레임 상태 누락");
    }

    private static EntityRef Switch(Frame frame, int index)
    {
        var states = frame.Filter<FlipFriendsStageSwitch>();
        while (states.Next(out var entity, out var state))
            if (state.ContentIndex == index) return entity;
        throw new InvalidOperationException("스위치 프레임 상태 누락");
    }
}

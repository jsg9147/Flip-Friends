using System;
using System.Globalization;
using System.Linq;
using FlipFriends.Maps;
using Quantum;

namespace FlipFriends.Network
{
    public static class QuantumGameContent
    {
        public const string NetworkVersion = "ff-product-9";
        public const string MovementRules = "이동2;60Hz;걷기4;달리기5.5;SmoothDamp0.4;반전0.7;중력44;점프13.2;최소sqrt105.6;달리기추가2;해제컷;로프18틱;Unity씬수동";
        public const string HazardRules = "피격1;정적트랩Box/볼록Poly;이동뒤판정;넉백20x20;지정방향비정규화;위치방향정규화;무적60;조작60;로프차단60;들림제외틱시작;운반자넉백방향내려놓기;ID순첫트랩;복귀초기화;상자피격없음";
        public static QuantumMapConversion Prepare(IQuantumMapContent map, RuntimeConfig config)
        {
            if (map == null) throw new InvalidOperationException("지원 맵을 선택하세요.");
            var simulation = UnityEngine.Resources.Load<SimulationConfig>("FlipFriendsProductSimulationConfig");
            if (simulation == null || simulation.AutoLoadSceneFromMap != SimulationConfig.AutoLoadSceneFromMapMode.Disabled)
                throw new InvalidOperationException("제품 Quantum 씬 수명 설정이 없거나 자동 씬 로딩이 활성화되어 있습니다.");
            config.SimulationConfig = simulation;
            var conversion = map.ValidateAndApply(config);
            if (conversion.Goals.Count == 0) throw new InvalidOperationException("도착점이 없습니다.");
            map.ApplyRespawn(config, conversion);
            config.EnableGoals = true;
            config.GoalCenters = conversion.Goals.Select(box => box.Center).ToArray();
            config.GoalSizes = conversion.Goals.Select(box => box.Size).ToArray();
            var goals = string.Join(";", conversion.Goals.Select(box => string.Join(",", new[]
            {
                box.Center.X.RawValue, box.Center.Y.RawValue, box.Size.X.RawValue, box.Size.Y.RawValue
            }.Select(value => value.ToString(CultureInfo.InvariantCulture)))));
            config.CustomMapAgreement += ":제품9:" + QuantumMapConverter.Hash(FormattableString.Invariant($"지형6;FP윤곽;미세단차8raw;플레이어경사30접선FP;급경사법선점프미끄럼;상자경사45;공식1;Reset조각합집합;장치5;CP단조증가;구간운반원자대기;소환진입성공쿨다운;안전점유;복귀4;리셋상승엣지;들림무시;FP경계={config.FallMinX.RawValue},{config.FallMaxX.RawValue},{config.FallMinY.RawValue};여백10;위쪽제외;점유대기;복귀틱입력차단;") + MovementRules + ";상호작용3;밟기13.2x1.2;밀림0.5;차단18/9;축소30;운반0.95;탈출90/15;해제9;던지기7/12/24;상자6/6/9.8/1.5;놓기앞/위/몸/실패유지;PlayerRef순;2인;위입력도착;" + HazardRules + ";" + goals);
            return conversion;
        }
    }
}

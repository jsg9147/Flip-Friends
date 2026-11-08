using System;
using System.Globalization;
using System.Linq;
using FlipFriends.Maps;
using Quantum;

namespace FlipFriends.Network
{
    public static class QuantumGameContent
    {
        public const string NetworkVersion = "ff-product-6";
        public const string MovementRules = "이동2;60Hz;걷기4;달리기5.5;SmoothDamp0.4;반전0.7;중력44;점프13.2;최소sqrt105.6;달리기추가2;해제컷;로프18틱;Unity씬수동";
        public static QuantumMapConversion Prepare(QuantumCustomMapAsset map, RuntimeConfig config)
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
            config.CustomMapAgreement += ":제품6:" + QuantumMapConverter.Hash(FormattableString.Invariant($"지형6;FP윤곽;Reset조각합집합;장치5;CP단조증가;구간운반원자대기;소환진입성공쿨다운;안전점유;복귀4;리셋상승엣지;들림무시;FP경계={config.FallMinX.RawValue},{config.FallMaxX.RawValue},{config.FallMinY.RawValue};여백10;위쪽제외;점유대기;복귀틱입력차단;") + MovementRules + ";상호작용3;밟기13.2x1.2;밀림0.5;차단18/9;축소30;운반0.95;탈출90/15;해제9;던지기7/12/24;상자6/6/9.8/1.5;놓기앞/위/몸/실패유지;PlayerRef순;2인;위입력도착;" + goals);
            return conversion;
        }
    }
}

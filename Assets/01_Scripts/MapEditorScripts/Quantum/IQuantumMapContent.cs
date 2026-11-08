using Quantum;

namespace FlipFriends.Maps
{
    // MapData 맵과 공식 스테이지가 같은 제품 시작 경로(검증→RuntimeConfig→합의)를 쓰게 하는 공통 계약.
    public interface IQuantumMapContent
    {
        string DisplayName { get; }
        QuantumMapConversion ValidateAndApply(RuntimeConfig config);
        void ApplyRespawn(RuntimeConfig config, QuantumMapConversion conversion);
    }
}

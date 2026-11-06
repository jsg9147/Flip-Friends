using Quantum;

public interface IQuantumMovementEvidence
{
    string Summary { get; }
    void Observe(Frame frame);
    void AssertComplete();
}

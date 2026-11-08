using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// What the removal asks of the carriers: the cause a road's carrier request takes, and the claim of the node a carrier that
/// waited takes up again. The carrier behaviour implements it.
/// </summary>
public interface IRoadCarriers
{
    /// <summary>The road no longer carries the cause of the command that built it.</summary>
    void ForgetCause(Road road);

    /// <summary>The carrier request of the road takes the cause of the command.</summary>
    void CarryCause(Road road, Cause command);

    /// <summary>The carrier in the slot, waiting at a node next to a flag, takes the node again and goes on.</summary>
    void Claim(int slot, Settler settler);
}

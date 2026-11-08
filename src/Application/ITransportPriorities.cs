namespace OpenSettlers.Simulation;

/// <summary>The rank of a good among those that wait for a road; the goods behaviour implements it.</summary>
public interface ITransportPriorities
{
    /// <summary>The transport priority of the good: of two goods that wait for a road the higher goes first.</summary>
    int TransportPriority(Good good);
}

using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The castle's list of waiting settlers (the word at 0x42 + 2 times the job in the storage item), which the requests for a
/// worker or a carrier take a settler from.
/// </summary>
public interface ICastleRoster
{
    /// <summary>The slot of the settler the list names for the job, 0 for none.</summary>
    int Waiting(Job job);

    /// <summary>
    /// The settler the list names for the job (the dispatch takes that one, 0x42be63, 0x42b455), else the first of the castle's
    /// settlers of the job that waits in the castle (state 1), or -1.
    /// </summary>
    int CastleSettler(Job job);

    /// <summary>The list no longer names the settler in the slot for the job, if it did (the dispatch clears the entry).</summary>
    void Take(Job job, int slot);

    /// <summary>A request sent a settler out of the castle: the castle's count of settlers that have not left yet rises.</summary>
    void WorkerSent();
}

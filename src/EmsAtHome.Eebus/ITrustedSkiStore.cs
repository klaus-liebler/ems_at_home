namespace EmsAtHome.Eebus;

/// <summary>
/// Persists which remote SKIs (SHIP pairing partners) are trusted across process restarts.
/// EEBUSManager's own trust list is in-memory only. Deliberately not SQLite yet - see
/// Design/decisions/0001-eebus-net-library-choice.md for why persistence stays this small
/// until a real schema need (device registry + limits + audit log) exists.
/// </summary>
public interface ITrustedSkiStore
{
    IReadOnlyCollection<string> GetTrustedSkis();
    void AddTrustedSki(string ski);
    void RemoveTrustedSki(string ski);
}

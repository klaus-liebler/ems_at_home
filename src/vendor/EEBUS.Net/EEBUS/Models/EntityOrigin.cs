namespace EEBUS.Models
{
    /// <summary>
    /// Whether an Entity models our own local device (safe to seed with configured/default
    /// placeholder data in a UseCase constructor) or a remote device discovered over SPINE (must
    /// never be seeded - only real wire-data replies/notifies may populate its feature data).
    /// </summary>
    public enum EntityOrigin
    {
        Local,
        Remote
    }
}

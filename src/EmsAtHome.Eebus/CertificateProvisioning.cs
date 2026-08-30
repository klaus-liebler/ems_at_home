namespace EmsAtHome.Eebus;

/// <summary>
/// Fails fast with a clear error if the CA-signed PFX isn't in place yet - EEBUS.Net's own
/// CertificateGenerator.GenerateCert silently falls back to generating a self-signed cert when
/// the expected file is missing, which would quietly defeat the "use our own root CA" decision
/// instead of failing loudly. See Design/decisions/0001-eebus-net-library-choice.md.
/// </summary>
public static class CertificateProvisioning
{
    public static void EnsureCertificateExists(EebusOptions options)
    {
        var path = Path.Combine(options.BasePath, options.Certificate + ".pfx");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"EEBus-Zertifikat nicht gefunden: '{path}'. " +
                "Bitte zuerst ein PFX (Passwort 'xcenter') aus der eigenen Root-CA erzeugen und dort " +
                "ablegen - siehe Design/decisions/0001-eebus-net-library-choice.md.");
        }
    }
}

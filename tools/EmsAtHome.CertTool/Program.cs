using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

// Generates the EEBus client/server certificate for ems_at_home using an explicitly named,
// persisted CNG key container - NOT ECDsa.Create() (ephemeral) and NOT a plain PFX re-import
// with X509KeyStorageFlags alone. Both of those produce a certificate that Windows/.NET
// (Schannel) silently refuses to present during outbound TLS client authentication, even
// though the private key itself is fully usable - see Design/decisions/0001-eebus-net-library-choice.md
// for the full investigation. A named CngKey survives PFX export/re-import (verified), so
// EEBUS.Net's own unmodified certificate-loading code works fine once the PFX is built this way.

const string KeyName = "ems-at-home-eebus";
const string PfxPassword = "xcenter"; // hardcoded by EEBUS.Net's CertificateGenerator, not our choice

if (args.Length < 1 || args[0] != "generate")
{
    Console.WriteLine("""
        Usage: EmsAtHome.CertTool generate --out <directory> [--subject ems-eebus] [--cn ems-at-home] [--years 5]

        Generates (or reuses) the named CNG key 'ems-at-home-eebus' and exports a self-signed
        certificate to <directory>/<subject>.pfx (password 'xcenter', as EEBUS.Net expects).
        Prints the certificate's SKI, needed to pair with the wallbox's EEBUS/HEMS UI.
        """);
    return 1;
}

string? outDir = null, subject = "ems-eebus", cn = "ems-at-home";
int years = 5;
for (int i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--out" when i + 1 < args.Length: outDir = args[++i]; break;
        case "--subject" when i + 1 < args.Length: subject = args[++i]; break;
        case "--cn" when i + 1 < args.Length: cn = args[++i]; break;
        case "--years" when i + 1 < args.Length: years = int.Parse(args[++i]); break;
        default:
            Console.Error.WriteLine($"Unbekannte Option: {args[i]}");
            return 1;
    }
}

if (outDir is null)
{
    Console.Error.WriteLine("--out <directory> ist erforderlich.");
    return 1;
}

Directory.CreateDirectory(outDir);
var pfxPath = Path.Combine(outDir, subject + ".pfx");

CngKey cngKey;
try
{
    cngKey = CngKey.Open(KeyName);
    Console.WriteLine($"Vorhandenen CNG-Schlüssel '{KeyName}' wiederverwendet.");
}
catch (CryptographicException)
{
    var keyParams = new CngKeyCreationParameters
    {
        ExportPolicy = CngExportPolicies.AllowExport | CngExportPolicies.AllowPlaintextExport,
        KeyUsage = CngKeyUsages.Signing,
        Provider = CngProvider.MicrosoftSoftwareKeyStorageProvider,
        KeyCreationOptions = CngKeyCreationOptions.None, // persisted, NOT ephemeral - this is the fix
    };
    cngKey = CngKey.Create(CngAlgorithm.ECDsaP256, KeyName, keyParams);
    Console.WriteLine($"Neuen benannten, dauerhaften CNG-Schlüssel '{KeyName}' erzeugt.");
}

using var ecdsa = new ECDsaCng(cngKey);
var req = new CertificateRequest($"CN={cn}", ecdsa, HashAlgorithmName.SHA256);
req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
    [new Oid("1.3.6.1.5.5.7.3.1"), new Oid("1.3.6.1.5.5.7.3.2")], false)); // serverAuth + clientAuth
var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(years));

File.WriteAllBytes(pfxPath, cert.Export(X509ContentType.Pfx, PfxPassword));

var ski = Convert.ToHexStringLower(SHA1.HashData(cert.GetPublicKey()));

Console.WriteLine($"\nZertifikat exportiert nach: {pfxPath}");
Console.WriteLine($"Subject: {cert.Subject}");
Console.WriteLine($"Gültig bis: {cert.NotAfter:yyyy-MM-dd}");
Console.WriteLine($"SKI (für das Pairing in der Wallbox-EEBUS/HEMS-UI): {ski}");
return 0;

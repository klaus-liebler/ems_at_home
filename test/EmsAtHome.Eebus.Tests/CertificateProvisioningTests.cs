using EmsAtHome.Eebus;

namespace EmsAtHome.Eebus.Tests;

public class CertificateProvisioningTests
{
    [Fact]
    public void EnsureCertificateExists_Throws_WhenPfxMissing()
    {
        var options = new EebusOptions
        {
            Certificate = "does-not-exist",
            BasePath = Path.GetTempPath(),
        };

        Assert.Throws<InvalidOperationException>(() => CertificateProvisioning.EnsureCertificateExists(options));
    }

    [Fact]
    public void EnsureCertificateExists_DoesNotThrow_WhenPfxPresent()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllBytes(Path.Combine(dir.FullName, "ems-eebus.pfx"), [0x01]);
            var options = new EebusOptions { Certificate = "ems-eebus", BasePath = dir.FullName };

            CertificateProvisioning.EnsureCertificateExists(options);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}

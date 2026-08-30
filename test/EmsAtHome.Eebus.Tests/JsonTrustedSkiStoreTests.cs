using EmsAtHome.Eebus;

namespace EmsAtHome.Eebus.Tests;

public class JsonTrustedSkiStoreTests
{
    [Fact]
    public void AddTrustedSki_PersistsAcrossNewInstance()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var options = new EebusOptions { Certificate = "ems-eebus", BasePath = dir.FullName };

            new JsonTrustedSkiStore(options).AddTrustedSki("abc123");
            var reloaded = new JsonTrustedSkiStore(options);

            Assert.Contains("abc123", reloaded.GetTrustedSkis());
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void RemoveTrustedSki_RemovesEntry()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var options = new EebusOptions { Certificate = "ems-eebus", BasePath = dir.FullName };
            var store = new JsonTrustedSkiStore(options);
            store.AddTrustedSki("abc123");

            store.RemoveTrustedSki("abc123");

            Assert.DoesNotContain("abc123", store.GetTrustedSkis());
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void GetTrustedSkis_ReturnsEmpty_WhenNoFileYet()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var options = new EebusOptions { Certificate = "ems-eebus", BasePath = dir.FullName };

            Assert.Empty(new JsonTrustedSkiStore(options).GetTrustedSkis());
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}

using wl.Services;

namespace wl.tests;

public class PathsServiceTests
{
    private static string TempFile() => Path.Combine(Path.GetTempPath(), $"wl-paths-{Guid.NewGuid():N}.json");

    [Fact]
    public void Get_MissingFile_ReturnsNull()
    {
        var file = TempFile();
        var svc = new PathsService(file);

        Assert.Null(svc.Get("FOO"));
        Assert.Empty(svc.All());
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void Set_CreatesFileAndRoundtrips()
    {
        var file = TempFile();
        try
        {
            new PathsService(file).Set("REPOS_ROOT", "D:/repos");

            var svc2 = new PathsService(file);
            Assert.Equal("D:/repos", svc2.Get("REPOS_ROOT"));
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void Set_UpsertsExistingKey()
    {
        var file = TempFile();
        try
        {
            var svc = new PathsService(file);
            svc.Set("FOO", "/first");
            svc.Set("FOO", "/second");

            Assert.Equal("/second", new PathsService(file).Get("FOO"));
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void Get_ValueWithTilde_ReturnsRaw()
    {
        // PathsService returns raw values; tilde expansion is the resolver's job.
        var file = TempFile();
        try
        {
            new PathsService(file).Set("DOCS", "~/OneDrive/docs");
            Assert.Equal("~/OneDrive/docs", new PathsService(file).Get("DOCS"));
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void Load_InvalidJson_TreatsAsEmpty()
    {
        var file = TempFile();
        try
        {
            File.WriteAllText(file, "{ not valid json");
            var err = Console.Error;
            using var sw = new StringWriter();
            Console.SetError(sw);
            try
            {
                var svc = new PathsService(file);
                Assert.Null(svc.Get("ANYTHING"));
                Assert.Empty(svc.All());
                Assert.Contains("not valid JSON", sw.ToString());
            }
            finally
            {
                Console.SetError(err);
            }
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("1LEADING_DIGIT")]
    [InlineData("has space")]
    [InlineData("has-dash")]
    [InlineData("has.dot")]
    public void Set_InvalidName_Throws(string name)
    {
        var file = TempFile();
        try
        {
            var svc = new PathsService(file);
            Assert.Throws<ArgumentException>(() => svc.Set(name, "/x"));
            Assert.False(File.Exists(file));
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Theory]
    [InlineData("REPOS")]
    [InlineData("_private")]
    [InlineData("R1")]
    [InlineData("Mixed_Case_123")]
    public void Set_ValidName_Accepted(string name)
    {
        var file = TempFile();
        try
        {
            new PathsService(file).Set(name, "/x");
            Assert.Equal("/x", new PathsService(file).Get(name));
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void All_ReflectsSetOperations()
    {
        var file = TempFile();
        try
        {
            var svc = new PathsService(file);
            svc.Set("A", "1");
            svc.Set("B", "2");

            var all = svc.All();
            Assert.Equal(2, all.Count);
            Assert.Equal("1", all["A"]);
            Assert.Equal("2", all["B"]);
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }
}

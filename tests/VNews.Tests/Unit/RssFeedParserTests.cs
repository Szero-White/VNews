using VNews.Infrastructure.News;

namespace VNews.Tests.Unit;

/// <summary>The feed reader on a fixed XML sample: no network, nothing from the feed is trusted.</summary>
public sealed class RssFeedParserTests
{
    private const string Sample = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0"><channel><title>Mẫu</title>
          <item>
            <title>Tiêu đề &lt;b&gt;thử&lt;/b&gt; một</title>
            <description><![CDATA[<a href="https://vnexpress.net/bai-thu-mot-5139282.html"><img src="https://i1-vnexpress.vnecdn.net/a.jpg"></a></br>Tóm tắt   thử &amp; ngắn.<script>alert(1)</script>]]></description>
            <pubDate>Sun, 01 Nov 2026 11:55:59 +0700</pubDate>
            <link>https://vnexpress.net/bai-thu-mot-5139282.html</link>
            <enclosure type="image/jpeg" length="1200" url="https://i1-vnexpress.vnecdn.net/a.jpg?w=1200"/>
          </item>
          <item><title>Link lạ</title><description>x</description><link>https://evil.example/bai-5139283.html</link></item>
          <item><title>Không https</title><description>x</description><link>http://vnexpress.net/bai-5139284.html</link></item>
          <item><title></title><description>x</description><link>https://vnexpress.net/bai-5139285.html</link></item>
          <item>
            <title>Ảnh ngoài</title><description>d</description><link>https://vnexpress.net/anh-ngoai-5139286.html</link>
            <enclosure type="image/jpeg" length="1" url="https://evil.example/x.jpg"/>
          </item>
        </channel></rss>
        """;

    [Fact]
    public void Parse_KeepsOnlyPublisherLinks_AndReducesHtmlToPlainText()
    {
        var items = RssFeedParser.Parse(Sample);

        Assert.Equal(2, items.Count); // foreign host, plain http and empty title are dropped
        var first = items[0];
        Assert.Equal("Tiêu đề thử một", first.Title);
        Assert.StartsWith("Tóm tắt thử & ngắn.", first.Summary);
        Assert.DoesNotContain("<", first.Summary);
        Assert.Equal("https://vnexpress.net/bai-thu-mot-5139282.html", first.Link);
        Assert.Equal(new DateTime(2026, 11, 1, 4, 55, 59, DateTimeKind.Utc), first.PublishedUtc);
        Assert.StartsWith("https://i1-vnexpress.vnecdn.net/", first.ImageUrl);
    }

    [Fact]
    public void Parse_IgnoresImagesFromOtherHosts()
    {
        Assert.Null(RssFeedParser.Parse(Sample).Single(i => i.Title == "Ảnh ngoài").ImageUrl);
    }

    [Fact]
    public void Parse_RefusesDtdsAndBrokenXml_AndHonoursTheLimit()
    {
        var withDtd = "<?xml version=\"1.0\"?><!DOCTYPE rss [<!ENTITY x SYSTEM \"file:///c:/windows/win.ini\">]><rss><channel><item><title>&x;</title><link>https://vnexpress.net/a-1.html</link></channel></rss>";
        Assert.Empty(RssFeedParser.Parse(withDtd));
        Assert.Empty(RssFeedParser.Parse("<rss><channel><item>"));
        Assert.Empty(RssFeedParser.Parse(""));
        Assert.Single(RssFeedParser.Parse(Sample, maxItems: 1));
    }

    [Theory]
    [InlineData("https://vnexpress.net/ten-bai-5139282.html", "5139282")]
    [InlineData("https://vnexpress.net/ten-bai-5139282.html?x=1", "5139282")]
    [InlineData("https://vnexpress.net/khong-co-id.html", null)]
    public void SourceId_IsTheNumberAtTheEndOfTheAddress(string link, string? expected) =>
        Assert.Equal(expected, RssFeedParser.SourceId(link));
}

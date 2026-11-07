using System.Net;
using HtmlAgilityPack;
using Yukari.Core.Models;
using Yukari.Core.Sources;
using Yukari.Core.Sources.Http;

namespace Yukari.Plugin.WeebCentral;

[ComicSourceMetadata(
    "WeebCentral",
    "1.4.0+core2.6.0",
    "https://github.com/Yukari-App/Plugin.WeebCentral/releases",
    "https://weebcentral.com/favicon.ico",
    "Access thousands of manga from WeebCentral's library."
)]
public partial class WeebCentralSource : IComicSource, IRequiresHttpClient
{
    private const int DefaultPageSize = 32;
    private const string SourceLanguage = "en";
    private const string BaseUrl = "https://weebcentral.com";

    private ISharedHttpClient? _httpClient;

    public IReadOnlyList<Filter> Filters => _filters;
    public IReadOnlyDictionary<string, string> Languages => _languages;

    public void SetHttpClient(ISharedHttpClient httpClient) => _httpClient = httpClient;

    public async Task<IReadOnlyList<Comic>> SearchAsync(
        string query,
        IReadOnlyDictionary<string, IReadOnlyList<string>> filters,
        int page = 1,
        CancellationToken ct = default
    )
    {
        var queryParams = new Dictionary<string, string[]>
        {
            ["limit"] = [DefaultPageSize.ToString()],
            ["offset"] = [((page - 1) * DefaultPageSize).ToString()],
            ["display_mode"] = ["Minimal Display"],
            ["text"] = [query],
        };

        foreach (var kvp in filters)
            queryParams[kvp.Key] = kvp.Value.ToArray();

        string searchUrl = $"{BaseUrl}/search/data?{ToQueryString(queryParams)}";

        var html = await GetHTMLAsync(searchUrl, ct);
        if (html == null)
            return Array.Empty<Comic>();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var articles = doc.DocumentNode.SelectNodes("//article[contains(@class, 'bg-base-300')]");
        if (articles is not { Count: > 0 })
            return Array.Empty<Comic>();

        var comics = new List<Comic>();
        foreach (var article in articles)
        {
            var titleLink = article.SelectSingleNode(".//a[contains(@class, 'link-hover')]");

            var id = ExtractIdFromUrl(titleLink?.GetAttributeValue("href", ""));
            if (id == null)
                continue;

            var title = titleLink?.InnerText.Trim() ?? "Unknown Title";

            comics.Add(
                new Comic(
                    Id: id,
                    ComicUrl: null,
                    Title: title,
                    Author: null,
                    Description: null,
                    Tags: [],
                    Year: null,
                    CoverImageUrl: GetCoverUrl(id),
                    Langs: []
                )
            );
        }

        return comics;
    }

    public async Task<IReadOnlyList<Comic>> GetTrendingAsync(
        IReadOnlyDictionary<string, IReadOnlyList<string>> filters,
        int page = 1,
        CancellationToken ct = default
    )
    {
        var trendingFilters = new Dictionary<string, IReadOnlyList<string>>(filters)
        {
            ["order"] = ["Descending"],
            ["sort"] = ["Popularity"],
        };

        return await SearchAsync(string.Empty, trendingFilters, page, ct);
    }

    public async Task<Comic?> GetDetailsAsync(string comicId, CancellationToken ct = default)
    {
        string detailsUrl = $"{BaseUrl}/series/{comicId}";

        var html = await GetHTMLAsync(detailsUrl, ct);
        if (html == null)
            return null;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var titleNode = doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'text-center')]");
        string title = titleNode?.InnerText.Trim() ?? "Unknown Title";

        var authorNodes = doc.DocumentNode.SelectNodes("//li[contains(., 'Author(s)')]//a");
        string[] authors =
            authorNodes?.Select(a => a.InnerText.Trim()).ToArray() ?? Array.Empty<string>();

        var tagNodes = doc.DocumentNode.SelectNodes("//li[contains(., 'Tags(s)')]//a");
        string[] tags =
            tagNodes?.Select(t => t.InnerText.Trim()).ToArray() ?? Array.Empty<string>();

        var statusNode = doc.DocumentNode.SelectSingleNode("//li[contains(., 'Status:')]//a");
        ComicStatus status = GetComicStatus(statusNode?.InnerText.Trim());

        var yearNode = doc.DocumentNode.SelectSingleNode("//li[contains(., 'Released:')]/span");
        string yearStr = yearNode?.InnerText.Trim() ?? "";
        int? year = int.TryParse(yearStr, out int y) ? y : null;

        var descriptionNode = doc.DocumentNode.SelectSingleNode(
            "//p[contains(@class, 'whitespace-pre-wrap')]"
        );
        string? description = descriptionNode?.InnerText.Trim();

        string? coverUrl = GetCoverUrl(comicId);

        return new Comic(
            Id: comicId,
            ComicUrl: detailsUrl,
            Title: title,
            Author: authors.FirstOrDefault(),
            Description: description,
            Tags: tags,
            Year: year,
            CoverImageUrl: coverUrl,
            Langs: [SourceLanguage],
            Status: status
        );
    }

    public async Task<IReadOnlyList<Chapter>> GetAllChaptersAsync(
        string comicId,
        string language,
        CancellationToken ct = default
    )
    {
        var chaptersUrl = $"{BaseUrl}/series/{comicId}/full-chapter-list";

        var html = await GetHTMLAsync(chaptersUrl, ct);
        if (html == null)
            return Array.Empty<Chapter>();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var chapterNodes = doc.DocumentNode.SelectNodes(
            "//div[contains(@class, 'flex items-center')]//a[contains(@href, '/chapters/')]/.."
        );

        if (chapterNodes is not { Count: > 0 })
            return Array.Empty<Chapter>();

        var chapters = new List<Chapter>();

        // WeebCentral returns chapters in descending order (newest first).
        // Reverse to get chronological order (oldest first).
        foreach (var node in chapterNodes.Reverse())
        {
            var linkNode = node.SelectSingleNode(".//a[contains(@href, '/chapters/')]");
            if (linkNode == null)
                continue;

            string href = linkNode.GetAttributeValue("href", "");
            string? chapterId = ExtractChapterIdFromUrl(href);
            if (string.IsNullOrEmpty(chapterId))
                continue;

            var titleSpan = linkNode.SelectSingleNode(".//span[contains(@class, 'grow')]//span");
            string title = titleSpan?.InnerText.Trim() ?? "Unknown";

            var timeNode = linkNode.SelectSingleNode(".//time");
            string? dateStr =
                timeNode?.GetAttributeValue("datetime", null!) ?? timeNode?.InnerText.Trim();

            DateOnly? lastUpdate = null;
            if (!string.IsNullOrEmpty(dateStr))
                if (DateTime.TryParse(dateStr, out DateTime dt))
                    lastUpdate = DateOnly.FromDateTime(dt.ToLocalTime());

            chapters.Add(
                new Chapter(
                    Id: chapterId,
                    Title: title,
                    Number: null,
                    Volume: null,
                    Language: language,
                    Groups: Array.Empty<string>(),
                    LastUpdate: lastUpdate,
                    Pages: null
                )
            );
        }

        return chapters;
    }

    public async Task<IReadOnlyList<ChapterPage>> GetChapterPagesAsync(
        string comicId,
        string chapterId,
        CancellationToken ct = default
    )
    {
        var pagesUrl =
            $"{BaseUrl}/chapters/{chapterId}/images?is_prev=False&reading_style=long_strip";

        var html = await GetHTMLAsync(pagesUrl, ct);
        if (html == null)
            return Array.Empty<ChapterPage>();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var imgNodes = doc.DocumentNode.SelectNodes("//section//img");
        if (imgNodes is not { Count: > 0 })
            return Array.Empty<ChapterPage>();

        var pages = new List<ChapterPage>(imgNodes.Count);
        for (int i = 0; i < imgNodes.Count; i++)
        {
            var img = imgNodes[i];
            string imageUrl = img.GetAttributeValue("src", "");
            if (string.IsNullOrEmpty(imageUrl))
                continue;

            pages.Add(new ChapterPage(Number: i + 1, ImageUrl: imageUrl));
        }

        return pages;
    }

    public async Task<byte[]?> GetImageBytesAsync(string imageUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return null;

        using var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
        request.Headers.Add("Referer", BaseUrl);

        using var response = await _httpClient!.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new HttpRequestException(
                "WeebCentral Rate Limit Exceeded. Try again later.",
                null,
                HttpStatusCode.TooManyRequests
            );

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

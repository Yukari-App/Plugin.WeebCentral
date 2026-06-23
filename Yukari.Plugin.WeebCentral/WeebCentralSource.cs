using System.Net;
using HtmlAgilityPack;
using Yukari.Core.Models;
using Yukari.Core.Sources;

namespace Yukari.Plugin.WeebCentral;

[ComicSourceMetadata(
    "WeebCentral",
    "1.0.0+core2.2.0",
    "https://github.com/Yukari-App/Plugin.WeebCentral/releases",
    "https://weebcentral.com/favicon.ico",
    "Access thousands of manga from WeebCentral's library."
)]
public class WeebCentralSource : IComicSource
{
    private const int DefaultPageSize = 32;
    private const string SourceLanguage = "en";

    private static IReadOnlyList<Filter>? _filters;
    private static IReadOnlyDictionary<string, string>? _languages;

    public IReadOnlyList<Filter> Filters =>
        _filters ??= [
            new Filter(
                Key: "adult",
                DisplayName: "Adult Content",
                [
                    new FilterOption("Any", "Any", true),
                    new FilterOption("True", "Yes", false),
                    new FilterOption("False", "No", false),
                ],
                AllowMultiple: false
            ),
            new Filter(
                Key: "included_type",
                DisplayName: "Types",
                [
                    new FilterOption("Manga", "Manga", false),
                    new FilterOption("Manhwa", "Manhwa", false),
                    new FilterOption("Manhua", "Manhua", false),
                    new FilterOption("OEL", "OEL", false),
                ],
                AllowMultiple: true
            ),
            new Filter(
                Key: "included_status",
                DisplayName: "Status",
                [
                    new FilterOption("Ongoing", "Ongoing", false),
                    new FilterOption("Complete", "Complete", false),
                    new FilterOption("Hiatus", "Hiatus", false),
                    new FilterOption("Canceled", "Canceled", false),
                ],
                AllowMultiple: true
            ),
            new Filter(
                Key: "included_tag",
                DisplayName: "Tags",
                [
                    new FilterOption("Action", "Action", false),
                    new FilterOption("Adult", "Adult", false),
                    new FilterOption("Adventure", "Adventure", false),
                    new FilterOption("Comedy", "Comedy", false),
                    new FilterOption("Doujinshi", "Doujinshi", false),
                    new FilterOption("Drama", "Drama", false),
                    new FilterOption("Ecchi", "Ecchi", false),
                    new FilterOption("Fantasy", "Fantasy", false),
                    new FilterOption("Gender Bender", "Gender Bender", false),
                    new FilterOption("Harem", "Harem", false),
                    new FilterOption("Hentai", "Hentai", false),
                    new FilterOption("Historical", "Historical", false),
                    new FilterOption("Horror", "Horror", false),
                    new FilterOption("Isekai", "Isekai", false),
                    new FilterOption("Josei", "Josei", false),
                    new FilterOption("Lolicon", "Lolicon", false),
                    new FilterOption("Martial Arts", "Martial Arts", false),
                    new FilterOption("Mature", "Mature", false),
                    new FilterOption("Mecha", "Mecha", false),
                    new FilterOption("Mystery", "Mystery", false),
                    new FilterOption("Psychological", "Psychological", false),
                    new FilterOption("Romance", "Romance", false),
                    new FilterOption("School Life", "School Life", false),
                    new FilterOption("Sci-fi", "Sci-fi", false),
                    new FilterOption("Seinen", "Seinen", false),
                    new FilterOption("Shotacon", "Shotacon", false),
                    new FilterOption("Shoujo", "Shoujo", false),
                    new FilterOption("Shoujo Ai", "Shoujo Ai", false),
                    new FilterOption("Shounen", "Shounen", false),
                    new FilterOption("Shounen Ai", "Shounen Ai", false),
                    new FilterOption("Slice of Life", "Slice of Life", false),
                    new FilterOption("Smut", "Smut", false),
                    new FilterOption("Sports", "Sports", false),
                    new FilterOption("Supernatural", "Supernatural", false),
                    new FilterOption("Tragedy", "Tragedy", false),
                    new FilterOption("Yaoi", "Yaoi", false),
                    new FilterOption("Yuri", "Yuri", false),
                    new FilterOption("Other", "Other", false),
                ],
                AllowMultiple: true
            ),
        ];

    public IReadOnlyDictionary<string, string> Languages =>
        _languages ??= new Dictionary<string, string> { { "en", "English" } };

    private const string BaseUrl = "https://weebcentral.com";

    private static readonly HttpClient _httpClient = new HttpClient();

    static WeebCentralSource()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Yukari.Plugin.WeebCentral/1.0.0");
    }

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
                    Slug: null,
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
            Slug: null,
            Title: title,
            Author: authors.FirstOrDefault(),
            Description: description,
            Tags: tags,
            Year: year,
            coverUrl,
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

            DateTime? lastUpdate = null;
            if (!string.IsNullOrEmpty(dateStr))
                if (DateTime.TryParse(dateStr, out DateTime dt))
                    lastUpdate = dt.ToLocalTime();

            chapters.Add(
                new Chapter(
                    Id: chapterId,
                    Title: title,
                    Number: null,
                    Volume: null,
                    Language: language,
                    Groups: Array.Empty<string>(),
                    LastUpdate: lastUpdate.HasValue
                        ? DateOnly.FromDateTime(lastUpdate.Value)
                        : DateOnly.MinValue,
                    Pages: 0
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

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private async Task<string?> GetHTMLAsync(string url, CancellationToken ct = default)
    {
        using var response = await _httpClient.GetAsync(url, ct);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new HttpRequestException(
                "WeebCentral Rate Limit Exceeded. Try again later.",
                null,
                HttpStatusCode.TooManyRequests
            );

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
            return default;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(ct);
    }

    private ComicStatus GetComicStatus(string? status) =>
        status switch
        {
            "Ongoing" => ComicStatus.Ongoing,
            "Completed" => ComicStatus.Completed,
            "Hiatus" => ComicStatus.Hiatus,
            "Canceled" => ComicStatus.Cancelled,
            _ => ComicStatus.Unknown,
        };

    private string? GetCoverUrl(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        return $"https://temp.compsci88.com/cover/normal/{id}.webp";
    }

    private string? ExtractIdFromUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return null;
        var parts = url.TrimEnd('/').Split('/');
        return parts.Length >= 2 ? parts[^2] : null;
    }

    private string? ExtractChapterIdFromUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return null;
        var parts = url.TrimEnd('/').Split('/');
        return parts.Length > 0 ? parts[^1] : null;
    }

    private static string ToQueryString(Dictionary<string, string[]> source) =>
        string.Join(
            "&",
            source.SelectMany(kvp =>
                kvp.Value.Select(v => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(v)}")
            )
        );
}

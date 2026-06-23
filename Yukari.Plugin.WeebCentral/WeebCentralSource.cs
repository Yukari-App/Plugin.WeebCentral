using System.Net;
using HtmlAgilityPack;
using Yukari.Core.Models;
using Yukari.Core.Sources;

namespace Yukari.Plugin.WeebCentral;

[ComicSourceMetadata(
    "WeebCentral",
    "0.1.0-Alpha+core2.2.0",
    "https://weebcentral.com/favicon.ico",
    "Access thousands of manga from WeebCentral's library."
)]
public class WeebCentralSource : IComicSource
{
    private const int DefaultPageSize = 32;

    private static IReadOnlyList<Filter>? _filters;
    private static IReadOnlyDictionary<string, string>? _languages;

    public IReadOnlyList<Filter> Filters =>
        _filters ??= [
            new Filter(
                Key: "adult",
                DisplayName: "Adult Content",
                [
                    new FilterOption("Any", "Any"),
                    new FilterOption("True", "Yes"),
                    new FilterOption("False", "No"),
                ],
                AllowMultiple: false
            ),
            new Filter(
                Key: "included_type",
                DisplayName: "Types",
                [
                    new FilterOption("Manga", "Manga"),
                    new FilterOption("Manhwa", "Manhwa"),
                    new FilterOption("Manhua", "Manhua"),
                    new FilterOption("OEL", "OEL"),
                ],
                AllowMultiple: true
            ),
            new Filter(
                Key: "included_status",
                DisplayName: "Status",
                [
                    new FilterOption("Ongoing", "Ongoing"),
                    new FilterOption("Complete", "Complete"),
                    new FilterOption("Hiatus", "Hiatus"),
                    new FilterOption("Canceled", "Canceled"),
                ],
                AllowMultiple: true
            ),
            new Filter(
                Key: "included_tag",
                DisplayName: "Tags",
                [
                    new FilterOption("Action", "Action"),
                    new FilterOption("Adult", "Adult"),
                    new FilterOption("Adventure", "Adventure"),
                    new FilterOption("Comedy", "Comedy"),
                    new FilterOption("Doujinshi", "Doujinshi"),
                    new FilterOption("Drama", "Drama"),
                    new FilterOption("Ecchi", "Ecchi"),
                    new FilterOption("Fantasy", "Fantasy"),
                    new FilterOption("Gender Bender", "Gender Bender"),
                    new FilterOption("Harem", "Harem"),
                    new FilterOption("Hentai", "Hentai"),
                    new FilterOption("Historical", "Historical"),
                    new FilterOption("Horror", "Horror"),
                    new FilterOption("Isekai", "Isekai"),
                    new FilterOption("Josei", "Josei"),
                    new FilterOption("Lolicon", "Lolicon"),
                    new FilterOption("Martial Arts", "Martial Arts"),
                    new FilterOption("Mature", "Mature"),
                    new FilterOption("Mecha", "Mecha"),
                    new FilterOption("Mystery", "Mystery"),
                    new FilterOption("Psychological", "Psychological"),
                    new FilterOption("Romance", "Romance"),
                    new FilterOption("School Life", "School Life"),
                    new FilterOption("Sci-fi", "Sci-fi"),
                    new FilterOption("Seinen", "Seinen"),
                    new FilterOption("Shotacon", "Shotacon"),
                    new FilterOption("Shoujo", "Shoujo"),
                    new FilterOption("Shoujo Ai", "Shoujo Ai"),
                    new FilterOption("Shounen", "Shounen"),
                    new FilterOption("Shounen Ai", "Shounen Ai"),
                    new FilterOption("Slice of Life", "Slice of Life"),
                    new FilterOption("Smut", "Smut"),
                    new FilterOption("Sports", "Sports"),
                    new FilterOption("Supernatural", "Supernatural"),
                    new FilterOption("Tragedy", "Tragedy"),
                    new FilterOption("Yaoi", "Yaoi"),
                    new FilterOption("Yuri", "Yuri"),
                    new FilterOption("Other", "Other"),
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
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Yukari.Plugin.WeebCentral/0.1.0");
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

        return articles
            .Select(article =>
            {
                var titleLink = article.SelectSingleNode(".//a[contains(@class, 'link-hover')]");
                var id = ExtractIdFromUrl(titleLink.GetAttributeValue("href", ""));

                if (id == null)
                    return null;

                return new Comic(
                    Id: id,
                    ComicUrl: null,
                    Slug: null,
                    Title: titleLink.InnerText.Trim(),
                    Author: null,
                    Description: null,
                    Tags: [],
                    Year: null,
                    CoverImageUrl: GetCoverUrl(id),
                    Langs: []
                );
            })
            .Where(c => c != null)
            .ToList()!;
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

    public Task<Comic?> GetDetailsAsync(string comicId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Chapter>> GetAllChaptersAsync(
        string comicId,
        string language,
        CancellationToken ct = default
    )
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<ChapterPage>> GetChapterPagesAsync(
        string comicId,
        string chapterId,
        CancellationToken ct = default
    )
    {
        throw new NotImplementedException();
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

    private static string ToQueryString(Dictionary<string, string[]> source) =>
        string.Join(
            "&",
            source.SelectMany(kvp =>
                kvp.Value.Select(v => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(v)}")
            )
        );
}

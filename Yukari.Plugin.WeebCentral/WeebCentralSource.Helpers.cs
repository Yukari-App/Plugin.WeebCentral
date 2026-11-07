using System.Net;
using Yukari.Core.Models;

namespace Yukari.Plugin.WeebCentral;

public partial class WeebCentralSource
{
    private static readonly IReadOnlyList<Filter> _filters =
    [
        new Filter(
            Key: "adult",
            DisplayName: "Adult Content",
            Options:
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
            Options:
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
            Options:
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
            Options:
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

    private static readonly IReadOnlyDictionary<string, string> _languages = new Dictionary<
        string,
        string
    >
    {
        { "en", "English" },
    };

    private async Task<string?> GetHTMLAsync(string url, CancellationToken ct = default)
    {
        using var response = await _httpClient!.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, url),
            ct
        );

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

    private static ComicStatus GetComicStatus(string? status) =>
        status switch
        {
            "Ongoing" => ComicStatus.Ongoing,
            "Complete" => ComicStatus.Completed,
            "Hiatus" => ComicStatus.Hiatus,
            "Canceled" => ComicStatus.Cancelled,
            _ => ComicStatus.Unknown,
        };

    private static string? GetCoverUrl(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        return $"https://temp.compsci88.com/cover/normal/{id}.webp";
    }

    private static string? ExtractIdFromUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return null;
        var parts = url.TrimEnd('/').Split('/');
        return parts.Length >= 2 ? parts[^2] : null;
    }

    private static string? ExtractChapterIdFromUrl(string? url)
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

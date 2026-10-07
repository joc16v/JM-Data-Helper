using System.Text.RegularExpressions;
using JMDataHelper.Models;

namespace JMDataHelper.Sources.Dhmz;

public static class DhmzParser
{
    private static readonly Regex MapRegionRegex = new(
        "<(?:g|a)\\b(?=[^>]*\\bid=\\\"(?<id>[^\\\"]+_pointer)\\\")(?=[^>]*\\bclass=\\\"[^\\\"]*fd-c-map__regija--(?<color>green|yellow|orange|red)[^\\\"]*\\\")[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));

    private static readonly IReadOnlyDictionary<string, string> CountyNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["grad_zagreb"] = "Grad Zagreb",
            ["zagrebacka_zupanija"] = "Zagrebačka županija",
            ["krapinsko-zagorska_zupanija"] = "Krapinsko-zagorska županija",
            ["varazdinska_zupanija"] = "Varaždinska županija",
            ["koprivnicko-krizevacka_zupanija"] = "Koprivničko-križevačka županija",
            ["medimurska_zupanija"] = "Međimurska županija",
            ["bjelovarsko-bilogorska_zupanija"] = "Bjelovarsko-bilogorska županija",
            ["viroviticko-podravska_zupanija"] = "Virovitičko-podravska županija",
            ["pozesko-slavonska_zupanija"] = "Požeško-slavonska županija",
            ["brodsko-posavska_zupanija"] = "Brodsko-posavska županija",
            ["osjecko-baranjska_zupanija"] = "Osječko-baranjska županija",
            ["vukovarsko-srijemska_zupanija"] = "Vukovarsko-srijemska županija",
            ["sisacko-moslavacka_zupanija"] = "Sisačko-moslavačka županija",
            ["karlovacka_zupanija"] = "Karlovačka županija",
            ["primorsko-goranska_zupanija"] = "Primorsko-goranska županija",
            ["istarska_zupanija"] = "Istarska županija",
            ["licko-senjska_zupanija"] = "Ličko-senjska županija",
            ["zadarska_zupanija"] = "Zadarska županija",
            ["sibensko-kninska_zupanija"] = "Šibensko-kninska županija",
            ["splitsko-dalmatinska_zupanija"] = "Splitsko-dalmatinska županija",
            ["dubrovacko-neretvanska_zupanija"] = "Dubrovačko-neretvanska županija"
        };

    private static readonly IReadOnlyDictionary<string, string> SeaNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["zapadna_obala_istre"] = "Zapadna obala Istre",
            ["kvarner_i_kvarneric"] = "Kvarner i Kvarnerić",
            ["velebitski_kanal"] = "Velebitski kanal",
            ["sjeverna_dalmacija"] = "Sjeverna Dalmacija",
            ["srednja_dalmacija"] = "Srednja Dalmacija",
            ["juzna_dalmacija"] = "Južna Dalmacija"
        };

    public static DhmzWarningsResponse Parse(int day, string sourceUrl, string html, DateTimeOffset? fetchedAt = null)
    {
        var colorsById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in MapRegionRegex.Matches(html))
        {
            var pointerId = match.Groups["id"].Value;
            var color = match.Groups["color"].Value.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(pointerId) || string.IsNullOrWhiteSpace(color))
                continue;

            var id = pointerId.EndsWith("_pointer", StringComparison.OrdinalIgnoreCase)
                ? pointerId[..^"_pointer".Length]
                : pointerId;

            colorsById[id] = color;
        }

        var missingCounties = CountyNames.Keys.Where(id => !colorsById.ContainsKey(id)).ToArray();
        if (missingCounties.Length > 0)
        {
            throw new InvalidDataException(
                "DHMZ HTML se promijenio ili nije potpun. Nedostaju županije: " +
                string.Join(", ", missingCounties));
        }

        var missingSea = SeaNames.Keys.Where(id => !colorsById.ContainsKey(id)).ToArray();
        if (missingSea.Length > 0)
        {
            throw new InvalidDataException(
                "DHMZ HTML se promijenio ili nije potpun. Nedostaju morska područja: " +
                string.Join(", ", missingSea));
        }

        var counties = CountyNames
            .Select(pair => ToRegion(pair.Key, pair.Value, colorsById[pair.Key]))
            .ToArray();

        var sea = SeaNames
            .Select(pair => ToRegion(pair.Key, pair.Value, colorsById[pair.Key]))
            .ToArray();

        return new DhmzWarningsResponse(
            Ok: true,
            Day: day,
            SourceUrl: sourceUrl,
            FetchedAt: fetchedAt ?? DateTimeOffset.Now,
            Counties: counties,
            Sea: sea);
    }

    private static WarningRegion ToRegion(string id, string name, string color) =>
        new(id, name, color, ColorToLevel(color));

    // Pilot/Viz convention for this project:
    // 0 = no color/manual, 1 = green, 2 = yellow, 3 = orange, 4 = red.
    private static int ColorToLevel(string color) => color.ToLowerInvariant() switch
    {
        "green" => 1,
        "yellow" => 2,
        "orange" => 3,
        "red" => 4,
        _ => 0
    };

}

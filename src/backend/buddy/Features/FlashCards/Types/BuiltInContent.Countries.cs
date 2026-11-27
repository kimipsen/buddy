using System.Globalization;

namespace buddy.Features.FlashCards;

// Card content of the Countries sets (D11, D14): the flag emoji on the front, the Danish name on the back.
// Key: flag:<ISO 3166-1 alpha-2>. The flag is built from the code as two regional indicator symbols, so
// there is no image asset (Windows renders the pair as letters; accepted in the requirements).
// The registry is BuiltInSetDescriptors and is not touched here.
public static partial class BuiltInContent
{
    // Level 1: Denmark, its Nordic neighbours and the countries around it (15).
    private static readonly (string Iso, string Name)[] CountriesLevel1 =
    [
        ("DK", "Danmark"), ("SE", "Sverige"), ("NO", "Norge"), ("FI", "Finland"), ("IS", "Island"),
        ("FO", "Færøerne"), ("DE", "Tyskland"), ("PL", "Polen"), ("NL", "Holland"), ("BE", "Belgien"),
        ("GB", "Storbritannien"), ("IE", "Irland"), ("FR", "Frankrig"), ("ES", "Spanien"), ("IT", "Italien"),
    ];

    // Level 2: the rest of Europe (35).
    private static readonly (string Iso, string Name)[] CountriesLevel2 =
    [
        ("PT", "Portugal"), ("CH", "Schweiz"), ("AT", "Østrig"), ("CZ", "Tjekkiet"), ("SK", "Slovakiet"),
        ("HU", "Ungarn"), ("RO", "Rumænien"), ("BG", "Bulgarien"), ("GR", "Grækenland"), ("HR", "Kroatien"),
        ("SI", "Slovenien"), ("RS", "Serbien"), ("BA", "Bosnien-Hercegovina"), ("ME", "Montenegro"), ("MK", "Nordmakedonien"),
        ("AL", "Albanien"), ("EE", "Estland"), ("LV", "Letland"), ("LT", "Litauen"), ("UA", "Ukraine"),
        ("BY", "Hviderusland"), ("MD", "Moldova"), ("LU", "Luxembourg"), ("MT", "Malta"), ("CY", "Cypern"),
        ("LI", "Liechtenstein"), ("MC", "Monaco"), ("AD", "Andorra"), ("SM", "San Marino"), ("VA", "Vatikanstaten"),
        ("TR", "Tyrkiet"), ("RU", "Rusland"), ("GE", "Georgien"), ("AM", "Armenien"), ("AZ", "Aserbajdsjan"),
    ];

    // Level 3: well-known countries from the rest of the world (50).
    private static readonly (string Iso, string Name)[] CountriesLevel3 =
    [
        ("US", "USA"), ("CA", "Canada"), ("MX", "Mexico"), ("BR", "Brasilien"), ("AR", "Argentina"),
        ("CL", "Chile"), ("PE", "Peru"), ("CO", "Colombia"), ("VE", "Venezuela"), ("CU", "Cuba"),
        ("EC", "Ecuador"), ("UY", "Uruguay"), ("CN", "Kina"), ("JP", "Japan"), ("KR", "Sydkorea"),
        ("KP", "Nordkorea"), ("IN", "Indien"), ("PK", "Pakistan"), ("BD", "Bangladesh"), ("TH", "Thailand"),
        ("VN", "Vietnam"), ("ID", "Indonesien"), ("PH", "Filippinerne"), ("MY", "Malaysia"), ("SG", "Singapore"),
        ("AU", "Australien"), ("NZ", "New Zealand"), ("EG", "Egypten"), ("MA", "Marokko"), ("ZA", "Sydafrika"),
        ("NG", "Nigeria"), ("KE", "Kenya"), ("ET", "Etiopien"), ("GH", "Ghana"), ("TZ", "Tanzania"),
        ("SA", "Saudi-Arabien"), ("IR", "Iran"), ("IQ", "Irak"), ("IL", "Israel"), ("AE", "Forenede Arabiske Emirater"),
        ("AF", "Afghanistan"), ("NP", "Nepal"), ("LK", "Sri Lanka"), ("MN", "Mongoliet"), ("KZ", "Kasakhstan"),
        ("DZ", "Algeriet"), ("TN", "Tunesien"), ("SN", "Senegal"), ("MG", "Madagaskar"), ("UG", "Uganda"),
    ];

    public static IReadOnlyList<BuiltInCard> Countries(int level) => level switch
    {
        1 => CountryCards(CountriesLevel1),
        2 => CountryCards(CountriesLevel2),
        3 => CountryCards(CountriesLevel3),
        _ => [],
    };

    // The flag of an ISO 3166-1 alpha-2 code: each letter becomes its regional indicator symbol (U+1F1E6 + offset).
    public static string FlagEmoji(string iso)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(iso.Length, 2);

        return string.Concat(iso.Select(c => char.ConvertFromUtf32(0x1F1E6 + (c - 'A'))));
    }

    private static List<BuiltInCard> CountryCards((string Iso, string Name)[] countries) =>
        [.. countries.Select(c => new BuiltInCard(string.Create(CultureInfo.InvariantCulture, $"flag:{c.Iso}"), FlagEmoji(c.Iso), c.Name))];
}

using buddy.Features.FlashCards;

using Xunit;

namespace buddy.IntegrationTests.Features.FlashCards.BuiltIn;

// The golden card keys of the clock and countries sets (D8), plain unit tests on BuiltInCatalogue.Default.
// A key is a contract: results, history and a stale client refer to it. A key that disappears from a set
// fails here; a deliberate removal moves the key to BuiltInCatalogue.BuiltInRetiredKeys and out of this list.
public sealed class ClockAndCountriesKeysAreStable
{
    private static readonly string[] Clock1 =
    [
        "clock:1:00", "clock:2:00", "clock:3:00", "clock:4:00", "clock:5:00", "clock:6:00",
        "clock:7:00", "clock:8:00", "clock:9:00", "clock:10:00", "clock:11:00", "clock:12:00"
    ];

    private static readonly string[] Clock2 =
    [
        "clock:1:30", "clock:2:30", "clock:3:30", "clock:4:30", "clock:5:30", "clock:6:30",
        "clock:7:30", "clock:8:30", "clock:9:30", "clock:10:30", "clock:11:30", "clock:12:30"
    ];

    private static readonly string[] Clock3 =
    [
        "clock:1:15", "clock:1:45", "clock:2:15", "clock:2:45", "clock:3:15", "clock:3:45",
        "clock:4:15", "clock:4:45", "clock:5:15", "clock:5:45", "clock:6:15", "clock:6:45",
        "clock:7:15", "clock:7:45", "clock:8:15", "clock:8:45", "clock:9:15", "clock:9:45",
        "clock:10:15", "clock:10:45", "clock:11:15", "clock:11:45", "clock:12:15", "clock:12:45"
    ];

    private static readonly string[] Clock4 =
    [
        "clock:1:05", "clock:1:10", "clock:1:20", "clock:1:25", "clock:1:35", "clock:1:40",
        "clock:1:50", "clock:1:55", "clock:2:05", "clock:2:10", "clock:2:20", "clock:2:25",
        "clock:2:35", "clock:2:40", "clock:2:50", "clock:2:55", "clock:3:05", "clock:3:10",
        "clock:3:20", "clock:3:25", "clock:3:35", "clock:3:40", "clock:3:50", "clock:3:55",
        "clock:4:05", "clock:4:10", "clock:4:20", "clock:4:25", "clock:4:35", "clock:4:40",
        "clock:4:50", "clock:4:55", "clock:5:05", "clock:5:10", "clock:5:20", "clock:5:25",
        "clock:5:35", "clock:5:40", "clock:5:50", "clock:5:55", "clock:6:05", "clock:6:10",
        "clock:6:20", "clock:6:25", "clock:6:35", "clock:6:40", "clock:6:50", "clock:6:55",
        "clock:7:05", "clock:7:10", "clock:7:20", "clock:7:25", "clock:7:35", "clock:7:40",
        "clock:7:50", "clock:7:55", "clock:8:05", "clock:8:10", "clock:8:20", "clock:8:25",
        "clock:8:35", "clock:8:40", "clock:8:50", "clock:8:55", "clock:9:05", "clock:9:10",
        "clock:9:20", "clock:9:25", "clock:9:35", "clock:9:40", "clock:9:50", "clock:9:55",
        "clock:10:05", "clock:10:10", "clock:10:20", "clock:10:25", "clock:10:35", "clock:10:40",
        "clock:10:50", "clock:10:55", "clock:11:05", "clock:11:10", "clock:11:20", "clock:11:25",
        "clock:11:35", "clock:11:40", "clock:11:50", "clock:11:55", "clock:12:05", "clock:12:10",
        "clock:12:20", "clock:12:25", "clock:12:35", "clock:12:40", "clock:12:50", "clock:12:55"
    ];

    private static readonly string[] Countries1 =
    [
        "flag:DK", "flag:SE", "flag:NO", "flag:FI", "flag:IS", "flag:FO",
        "flag:DE", "flag:PL", "flag:NL", "flag:BE", "flag:GB", "flag:IE",
        "flag:FR", "flag:ES", "flag:IT"
    ];

    private static readonly string[] Countries2 =
    [
        "flag:PT", "flag:CH", "flag:AT", "flag:CZ", "flag:SK", "flag:HU",
        "flag:RO", "flag:BG", "flag:GR", "flag:HR", "flag:SI", "flag:RS",
        "flag:BA", "flag:ME", "flag:MK", "flag:AL", "flag:EE", "flag:LV",
        "flag:LT", "flag:UA", "flag:BY", "flag:MD", "flag:LU", "flag:MT",
        "flag:CY", "flag:LI", "flag:MC", "flag:AD", "flag:SM", "flag:VA",
        "flag:TR", "flag:RU", "flag:GE", "flag:AM", "flag:AZ"
    ];

    private static readonly string[] Countries3 =
    [
        "flag:US", "flag:CA", "flag:MX", "flag:BR", "flag:AR", "flag:CL",
        "flag:PE", "flag:CO", "flag:VE", "flag:CU", "flag:EC", "flag:UY",
        "flag:CN", "flag:JP", "flag:KR", "flag:KP", "flag:IN", "flag:PK",
        "flag:BD", "flag:TH", "flag:VN", "flag:ID", "flag:PH", "flag:MY",
        "flag:SG", "flag:AU", "flag:NZ", "flag:EG", "flag:MA", "flag:ZA",
        "flag:NG", "flag:KE", "flag:ET", "flag:GH", "flag:TZ", "flag:SA",
        "flag:IR", "flag:IQ", "flag:IL", "flag:AE", "flag:AF", "flag:NP",
        "flag:LK", "flag:MN", "flag:KZ", "flag:DZ", "flag:TN", "flag:SN",
        "flag:MG", "flag:UG"
    ];

    public static TheoryData<string, string[]> Golden => new()
    {
        { "clock:1", Clock1 },
        { "clock:2", Clock2 },
        { "clock:3", Clock3 },
        { "clock:4", Clock4 },
        { "countries:1", Countries1 },
        { "countries:2", Countries2 },
        { "countries:3", Countries3 }
    };

    [Theory]
    [MemberData(nameof(Golden))]
    public void BuiltInKeysAreStable(string setKey, string[] expected)
    {
        var actual = BuiltInSetDescriptors.All.Single(d => d.Key == setKey).Cards.Select(c => c.Key).ToArray();

        Assert.Equal(expected, actual);
    }
}

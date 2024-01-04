using BankReport.MT940.Grammar;
using Sprache;

namespace GrammarTests;

public class MT940GrammarTests
{
    private string _report =
@":20:20231101-100102-9771
:25:NL58INGB9589603858
:28C:209/01
:60F:C231101BGN649436,14
:61:2311011101C21350,39N0222151  //067009992002151
TC14-TSC00
:86:020+14+
20ÇÀÏËÀÒÀ ÇÀ ÄÆÅÉ      +
21                              +
30        /      +
31NL16RABO9082037890+
32ÌÚÆÅ Â ×ÅÐÍÎ
:62F:C231101BGN670786,53
:64:C231101BGN670786,53
-
:20:20231101-100103-1489
:25:NL58INGB9589603858
:28C:012/01
:60F:C231101USD15812,65
:62F:C231101USD15812,65
:64:C231101USD15812,65
-
:20:20231101-100103-3364
:25:NL58INGB9589603858
:28C:034/01
:60F:C231101BGN25152,50
:62F:C231101BGN25152,50
:64:C231101BGN25152,50
-

        ";

    [Test]
    public void Test1()
    {
        var records = MT940Grammar.Report.Parse(_report.Replace(Environment.NewLine, ""));
        ;
    }
}
using Sprache;
using System.Globalization;

namespace BankReport.Grammar;

public class BankGrammar
{
    protected static readonly Parser<DateTime> ParseValueDate =
        from start in Parse.Regex("\\d{6}")
        select DateTime.ParseExact(start, "yyMMdd", new CultureInfo("bg-BG"));

    protected static readonly Parser<DateTime> ParseEntryDate =
        from start in Parse.Regex("\\d{4}")
        select DateTime.ParseExact(start, "MMdd", new CultureInfo("bg-BG"));

    protected static readonly Parser<string> ParseDirection =
        from entry in Parse.Regex("\\D{1,2}")
        select entry;

    protected static readonly Parser<decimal> Decimal =
        from open in Parse.Number
        from _ in Parse.Char(',')
        from next in Parse.Number
        select decimal.Parse(open + _ + next, new CultureInfo("bg-BG"));
}
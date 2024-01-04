using Sprache;
using BankReport.Grammar;
using BankReport.MT940.Grammar.Models;
namespace BankReport.MT940.Grammar;

public class MT940Grammar : BankGrammar
{
    private static readonly Parser<string> BeforeStartAfterStatementEnd =
        from startToken in Parse.IgnoreCase(":60F:")
            .Or(Parse.IgnoreCase(":62F:"))
            .Or(Parse.IgnoreCase(":64:"))
            .Token()
            .Text()
        select startToken;

    private static readonly Parser<string> NewStatement =
        from startsLiteral in Parse.IgnoreCase(":61:").Token()
        select string.Empty;

    private static readonly Parser<string> ParsePayReferece =
        from open in Parse.Letter.Many()
        from sequenceNumber in Parse.Number
        from whitespaces in (Parse.Char(' ').Many()).Optional()
        from next in Parse.Char('/').Many()
        from payref2 in Parse.AnyChar
            .Except(Parse.IgnoreCase(":NS:19").Token()
                .Or(Parse.IgnoreCase("+:86:").Token())
                .Or(Parse.IgnoreCase(":86:").Token()))
            .AtLeastOnce()
            .Text()
        select payref2;

    private static readonly Parser<MT940TransactionDto> ParseStatementLine =
        from startsLiteral in NewStatement
        from vd in BankGrammar.ParseValueDate
        from ed in BankGrammar.ParseEntryDate
        from d in BankGrammar.ParseDirection
        from amount in BankGrammar.Decimal
        from payref in ParsePayReferece
        select new MT940TransactionDto()
        {
            ValueDate = vd,
            EntryDate =
            new(vd.Year, ed.Month, ed.Day),
            Direction = d,
            Amount = amount,
            PayReference = payref.Replace(" ", "")
        };

    private static readonly Parser<string> StatementDetailsSubTagIndetifier =
        from s in Parse.Char('+').Once().Text()
        from dOne in Parse.Digit.Once().Text()
        from dTwo in Parse.Digit.Once().Text()
        select string.Concat(s, dOne, dTwo);

    private static readonly Parser<KeyValuePair<string, string>> StatementDetailsSubTag =
        from tagIdentifier in StatementDetailsSubTagIndetifier
        from content in Parse.Optional(Parse.AnyChar.Except(StatementDetailsSubTagIndetifier.Or(NewStatement).Or(BeforeStartAfterStatementEnd)).Many().Token())
        let contentValue = content.IsDefined ? string.Concat(content.Get()) : null
        select new KeyValuePair<string, string>(tagIdentifier, contentValue.Trim());

    private static readonly Parser<string> BookingTimeTag =
        from s in Parse.IgnoreCase(":NS:19").Token()
        from d1 in Parse.AnyChar.Except(Parse.IgnoreCase("+:86:").Token().Or(Parse.IgnoreCase(":86:").Token())).AtLeastOnce().Text()
        select d1;

    private static readonly Parser<string> StatementDetailsIndetifier =
        from tagIdentifier in Parse.IgnoreCase("+:86:").Or(Parse.IgnoreCase(":86:")).Token()
        from transactionCode in Parse.AnyChar.Except(StatementDetailsSubTagIndetifier.Or(NewStatement)).AtLeastOnce().Text()
        select transactionCode;

    private static readonly Parser<MT940TransactionDto> ParseStatement =
        from x in ParseStatementLine
        from booking in BookingTimeTag.Optional()
        from transactionCode in StatementDetailsIndetifier
        from subtags in StatementDetailsSubTag.Token().AtLeastOnce()
        let subtagsDictionary = subtags.ToDictionary(x => x.Key, x => x.Value)
        let payReference = (x.PayReference.Contains("TC") && x.PayReference.Contains("TSC")) ? x.PayReference[..x.PayReference.IndexOf("TC")] : x.PayReference
        let service = (x.PayReference.Contains("TC") && x.PayReference.Contains("TSC")) ? x.PayReference[x.PayReference.IndexOf("TC")..x.PayReference.IndexOf("-TSC")] : transactionCode
        select new MT940TransactionDto()
        {
            ValueDate = x.ValueDate,
            EntryDate = x.EntryDate,
            Direction = x.Direction?.Trim(),
            Amount = x.Amount,

            PayReference = payReference,
            Service = service,

            BookingTime = booking.GetOrDefault(),

            Description1 = subtagsDictionary.TryGetValue("+21", out var d1) ? d1.Trim() : null,
            Description2 = subtagsDictionary.TryGetValue("+20", out var d20) ? d20.Trim() : subtagsDictionary.TryGetValue("+22", out var d22) ? d22.Trim() : null,

            CustomerName = subtagsDictionary.TryGetValue("+32", out var cName32) ? string.IsNullOrWhiteSpace(cName32)
                                                                                    ? (subtagsDictionary.TryGetValue("+30", out var cName30) ? cName30.Trim() : null)
                                                                                    : cName32.Trim() : null,

            CustomerBranchCode = subtagsDictionary.TryGetValue("+30", out var bCode) ? bCode.Trim() : null,
            CustomerIban = subtagsDictionary.TryGetValue("+31", out var cIban) ? cIban.Trim() : null,
            CustomerBankName = subtagsDictionary.TryGetValue("+33", out var bName) ? bName.Trim() : null,
        };

    private static readonly Parser<MT940SummaryDto> Summary =
        from startToken in BeforeStartAfterStatementEnd
        from direction in ParseDirection
        from valueDate in ParseValueDate
        from currency in Parse.Regex("\\D{1,3}")
        from amount in BankGrammar.Decimal.Except(BeforeStartAfterStatementEnd)
        select new MT940SummaryDto()
        {
            Amount = amount,
            Currency = currency,
            Date = valueDate,
            FlagStart = startToken.Equals(":60F:") ? true : startToken.Equals(":62F:") ? false : null,
            Direction = direction,
        };

    public static readonly Parser<(IEnumerable<MT940SummaryDto>, IEnumerable<MT940TransactionDto>)> Report =
        from startToken in Parse.IgnoreCase(":20:").Text()
        from date in Parse.AnyChar.Except(Parse.IgnoreCase(":25:")).Many().Token().Text()
        from ibanIdentifier in Parse.IgnoreCase(":25:").Token().Text()
        from toIban in Parse.AnyChar.Except(Parse.IgnoreCase(":28C:").Or(Parse.IgnoreCase(":60F:")).Or(Parse.IgnoreCase(":28:"))).Many().Token().Text()
        from notNeeded in Parse.AnyChar.Except(Parse.IgnoreCase(":60F:")).Many().Token().Text()
        from summaryStart in Summary.Except(NewStatement)
        from statements in Parse.Optional(ParseStatement.AtLeastOnce()).Token()
        from summaryEnd in Summary.AtLeastOnce()
        let summary = summaryEnd.Append(summaryStart).Where(x => x.FlagStart != null)
        let summaryResult = summary.Select(x => x with { Iban = toIban })
        let statementsResult = statements.GetOrDefault()?.Select(x => x with { ReceiverIban = toIban })
        select (summaryResult, statementsResult);
}

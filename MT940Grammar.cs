public static class MT940Grammar
{
    #region START Define Exclusive Data Types

    private static readonly Parser<DateTime> ParseValueDate =
    from start in Parse.Regex("\\d{6}")
    select DateTime.ParseExact(start, "yyMMdd", new CultureInfo("bg-BG"));

    private static readonly Parser<DateTime> ParseEntryDate =
        from start in Parse.Regex("\\d{4}")
        select DateTime.ParseExact(start, "MMdd", new CultureInfo("bg-BG"));

    public static readonly Parser<string> BeforeStartAfterStatementEnd =
        from startToken in Parse.IgnoreCase(":60F:")
            .Or(Parse.IgnoreCase(":62F:"))
            .Or(Parse.IgnoreCase(":64:"))
            .Token()
            .Text()
        select startToken;

    private static readonly Parser<string> ParseDirection =
        from entry in Parse.Regex("\\D{1,2}")
        select entry;

    private static readonly Parser<double> Double =
        from open in Parse.Number
        from _ in Parse.Char(',')
        from next in Parse.Number
        select double.Parse(open + _ + next, new CultureInfo("bg-BG"));

    # endregion END Define Exclusive Data Types

    private static readonly Parser<string> NewStatement =
        from startsLiteral in Parse.IgnoreCase(":61:").Token()
        select string.Empty;

    private static readonly Parser<string> ParsePayReferece =
        from open in Parse.Letter.Many()
        from payref in Parse.Number
        from next in Parse.Char('/').Many()
        from payref2 in Parse.AnyChar
            .Except(
                Parse.IgnoreCase(":NS:19").Token()
                .Or(Parse.IgnoreCase("+:86:").Token())
                .Or(Parse.IgnoreCase(":86:").Token()))
            .AtLeastOnce()
            .Text()
        select payref2.Replace("TC14-TSC35", "");

    public static readonly Parser<MT940TrasactionDto> ParseStatementLine =
        from startsLiteral in NewStatement
        from vd in ParseValueDate
        from ed in ParseEntryDate
        from d in ParseDirection
        from amount in Double
        from payref in ParsePayReferece
        select new MT940TrasactionDto()
        {
            ValueDate = vd,
            EntryDate =
            new(vd.Year, ed.Month, ed.Day),
            Direction = d,
            Amount = amount,
            PayReference = payref
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

    private static readonly Parser<MT940TrasactionDto> ParseStatement =
        from x in ParseStatementLine
        from booking in BookingTimeTag.Optional()
        from transactionCode in StatementDetailsIndetifier
        from subtags in StatementDetailsSubTag.Token().AtLeastOnce()
        let subtagsDictionary = subtags.ToDictionary(x => x.Key, x => x.Value)
        select new MT940TrasactionDto()
        {
            ValueDate = x.ValueDate,
            EntryDate = x.EntryDate,
            Direction = x.Direction?.Trim(),
            Amount = x.Amount,
            PayReference = x.PayReference?.Trim(),

            BookingTime = booking.GetOrDefault(),

            Service = transactionCode?.Trim(),
            Description1 = subtagsDictionary.TryGetValue("+21", out var d1) ? d1.Trim() : null,
            Description2 = subtagsDictionary.TryGetValue("+20", out var d20)
                ? d20.Trim()
                : subtagsDictionary.TryGetValue("+22", out var d22)
                    ? d22.Trim() : null,
            CustomerName = subtagsDictionary.TryGetValue("+32", out var cName32)
                ? (string.IsNullOrWhiteSpace(cName32)
                    ? (subtagsDictionary.TryGetValue("+30", out var cName30) ? cName30.Trim() : null)
                    : cName32.Trim())
                : subtagsDictionary.TryGetValue("+30", out var cName302) ? cName302.Trim() : null,
            CustomerBranchCode = subtagsDictionary.TryGetValue("+30", out var bCode) ? bCode.Trim() : null,
            CustomerIban = subtagsDictionary.TryGetValue("+31", out var cIban) ? cIban.Trim() : null,
            CustomerBankName = subtagsDictionary.TryGetValue("+33", out var bName) ? bName.Trim() : null,
        };

    private static readonly Parser<MT940SummaryDto> Summary =
        from startToken in BeforeStartAfterStatementEnd
        from direction in ParseDirection
        from valueDate in ParseValueDate
        from currency in Parse.Regex("\\D{1,3}")
        from amount in Double.Except(BeforeStartAfterStatementEnd)
        select new MT940SummaryDto()
        {
            Amount = amount,
            Currency = currency,
            Date = valueDate,
            FlagStart = startToken.Equals(":60F:") ? true : startToken.Equals(":62F:") ? false : null,
            Direction = direction,
        };

    private static readonly Parser<(IEnumerable<MT940SummaryDto>, IEnumerable<MT940TrasactionDto>)> MT940 =
        from startToken in Parse.IgnoreCase(":20:").Text()
        from date in Parse.AnyChar.Except(Parse.IgnoreCase(":25:")).Many().Token().Text()
        from ibanIdentifier in Parse.IgnoreCase(":25:").Token().Text()
        from toIban in Parse.AnyChar.Except(Parse.IgnoreCase(":28C:").Or(Parse.IgnoreCase(":60F:"))).Many().Token().Text()
        from notNeeded in Parse.AnyChar.Except(Parse.IgnoreCase(":60F:")).Many().Token().Text()
        from summaryStart in Summary.Except(NewStatement)
        from statements in Parse.Optional(ParseStatement.AtLeastOnce()).Token()
        from summaryEnd in Summary.AtLeastOnce()
        let summary = summaryEnd.Append(summaryStart).Where(x=>x.FlagStart != null)
        select (summary, statements.GetOrDefault()?.Select(x => x.SetReceverIban(toIban)));
}

public class MT940TrasactionDto
{
    public int Id { get; set; }
    public string? ReceiverIban { get; set; }
    public DateTime ValueDate { get; set; }
    public DateTime EntryDate { get; set; }
    public string? BookingTime { get; set; }
    public string? Direction { get; set; }
    public double Amount { get; set; }
    public string? PayReference { get; set; }
    public string? Service { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerIban { get; set; }
    public string? Description1 { get; set; }
    public string? Description2 { get; set; }
    public string? CustomerBankName { get; set; }
    public string? CustomerBranchCode { get; set; }
    public int FileImportID { get; set; }
}

public static class MT940Extensions
{
    public static MT940TrasactionDto SetReceverIban(this MT940TrasactionDto statement, string iban)
    {
        statement.ReceiverIban = iban;
        return statement;
    }

    public static MT940SummaryDto SetReceverIban(this MT940SummaryDto summaryDto, string iban)
    {
        summaryDto.Iban = iban;
        return summaryDto;
    }

    public static string TrimNewLine(this string text) => text.Replace("\n", "").Replace("\r", "");
}

public class MT940SummaryDto
{
    public int Id { get; set; }
    public string Iban { get; set; }
    public DateTime Date { get; set; }
    public string Direction { get; set; }
    public string Currency { get; set; }
    public double Amount { get; set; }
    public bool? FlagStart { get; set; }
}
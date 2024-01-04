namespace BankReport.MT940.Grammar.Models;

public record MT940SummaryDto
{
    public string Iban { get; init; }
    public DateTime Date { get; init; }
    public string Direction { get; init; }
    public string Currency { get; init; }
    public decimal Amount { get; init; }
    public bool? FlagStart { get; init; }
}
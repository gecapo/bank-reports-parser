namespace BankReport.MT940.Grammar.Models;

public record MT940TransactionDto
{
    public string? ReceiverIban { get; init; }
    public DateTime ValueDate { get; init; }
    public DateTime EntryDate { get; init; }
    public string? BookingTime { get; init; }
    public string? Direction { get; init; }
    public decimal Amount { get; init; }
    public string? PayReference { get; init; }
    public string? Service { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerIban { get; init; }
    public string? Description1 { get; init; }
    public string? Description2 { get; init; }
    public string? CustomerBankName { get; init; }
    public string? CustomerBranchCode { get; init; }
}

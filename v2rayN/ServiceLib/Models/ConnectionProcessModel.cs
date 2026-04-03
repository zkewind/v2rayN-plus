namespace ServiceLib.Models;

public class ConnectionProcessModel
{
    public bool IsProcess { get; set; }
    public string? ProcessName { get; set; }
    public int ConnectionCount { get; set; }
    public bool IsExpanded { get; set; }
    public List<ConnectionProcessModel>? Children { get; set; }

    public string? Id { get; set; }
    public string? Host { get; set; }
    public string? Network { get; set; }
    public string? Type { get; set; }
    public string? Outbound { get; set; }
    public string? Chain { get; set; }
    public string? Elapsed { get; set; }
    public string? Status { get; set; }
    public bool IsFailed { get; set; }
    public string? RuleDomain { get; set; }
    public string? RuleIP { get; set; }
    public DateTime SortTime { get; set; }
    public ulong Upload { get; set; }
    public ulong Download { get; set; }
}

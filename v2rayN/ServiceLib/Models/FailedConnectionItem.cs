namespace ServiceLib.Models;

public class FailedConnectionItem
{
    public string Key { get; set; } = string.Empty;
    public string? TargetHost { get; set; }
    public string? TargetPort { get; set; }
    public string? RawTarget { get; set; }
    public string? Network { get; set; }
    public string? Type { get; set; }
    public string? ProcessName { get; set; }
    public string? ProcessPath { get; set; }
    public string? OutboundTag { get; set; }
    public string? FinalOutbound { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime LastSeen { get; set; }
}

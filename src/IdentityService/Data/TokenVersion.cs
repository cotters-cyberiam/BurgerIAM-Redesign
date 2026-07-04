namespace IdentityService.Data;

public class TokenVersion
{
    public int Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}

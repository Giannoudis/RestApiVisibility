
namespace RestApiVisibility;

/// <summary>The REST API configuration</summary>
public class ApiConfiguration
{
    /// <summary>The visible controllers</summary>
    public List<string> VisibleItems { get; set; } = new();

    /// <summary>The hidden controllers</summary>
    public List<string> HiddenItems { get; set; } = new();
}
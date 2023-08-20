using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;

namespace RestApiVisibility;

/// <summary>
/// API endpoint visibility conventions
/// </summary>
public sealed class ApiVisibilityConvention : IActionModelConvention
{
    private List<string> VisibleItems { get; }
    private List<string> HiddenItems { get; }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="visibleItems">List of visible items name masks (wildcards: *?)</param>
    /// <param name="hiddenItems">List of hidden items name masks (wildcards: *?)</param>
    public ApiVisibilityConvention(IEnumerable<string>? visibleItems = null,
        IEnumerable<string>? hiddenItems = null)
    {
        VisibleItems = visibleItems != null ? new(visibleItems) : new();
        HiddenItems = hiddenItems != null ? new(hiddenItems) : new();
    }

    public void Apply(ActionModel action)
    {
        // visible
        if (VisibleItems.Count > 0)
        {
            action.ApiExplorer.IsVisible = VisibleItems.Any(
                x => MatchItem(action.Controller.ControllerName, GetOperationId(action), x));
        }

        // hidden
        if (HiddenItems.Count > 0)
        {
            if (VisibleItems.Count > 0)
            {
                // exclude from visible
                if (action.ApiExplorer.IsVisible == true)
                {
                    action.ApiExplorer.IsVisible = !HiddenItems.Any(
                        x => MatchItem(action.Controller.ControllerName, GetOperationId(action), x));
                }
            }
            else
            {
                action.ApiExplorer.IsVisible = !HiddenItems.Any(
                    x => MatchItem(action.Controller.ControllerName, GetOperationId(action), x));
            }
        }
    }

    private static string? GetOperationId(ActionModel action) =>
        (action.Attributes.FirstOrDefault(x => x is HttpMethodAttribute) as HttpMethodAttribute)?.Name;

    private static bool MatchItem(string controllerName, string? operationId, string mask)
    {
        var controllerMask = mask;
        string? actionMask = null;

        var actionIndex = mask.IndexOf('.');
        if (actionIndex > 0)
        {
            controllerMask = mask.Substring(0, actionIndex);
            actionMask = mask.Substring(actionIndex + 1);
        }

        // controller mask only
        if (actionMask == null || string.IsNullOrWhiteSpace(operationId))
        {
            return MatchExpression(controllerName, controllerMask);
        }

        // controller and action mask
        return MatchExpression(controllerName, controllerMask) &&
               MatchExpression(operationId, actionMask);
    }

    private static bool MatchExpression(string text, string expression)
    {
        // no mask: simple string compare
        if (!expression.Contains('?') && !expression.Contains('*'))
        {
            return string.Equals(text, expression, StringComparison.InvariantCultureIgnoreCase);
        }

        // regex
        var regex = new Regex(expression.Replace(".", "[.]").Replace("*", ".*").Replace('?', '.'));
        return regex.IsMatch(text);
    }
}
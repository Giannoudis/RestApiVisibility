# REST API Visibility Control

Tools wie Swagger sind bei der Dokumentation von REST APIs weit verbreitet. Die REST Endpunkte mittels Controller gruppiert und können einen Namen (OperationId) besitzen.
Im UI werden im Normalfall alle REST Endpunkte angezeigt, was nicht immer erwünscht ist
- Ausblenden von Endpunkten welche nicht über das UI gesteuert werden können
- Ein oder Ausblenden von Endpunkten zum Anwendungskontext wie z.B. Audits, oder Experience-/Integration-Level
- Reduzieren von Endpunkten in der Entwicklung als Startoptimierung und UI Reduizierung

 Der folgende Lösungsansatz zeigt auf, wie konfigurationsbasierend REST Endpunkte ein- oder ausgeblendet werden.

 > 👉 Das Verbergen eines Endpunktes wirkt sich nicht auf dessen Verfügbarkeit aus, REST Clients könen diesen uneingeschränkt nutzen.

### Filterkonfiguration
Die Sichtbarkeit von Endpunkten wird in Konfigurationsdatei der Applikation bestimmt, wo die sichtbaren und/oder unsichtbarn Filter gesetzt werden. Folgende Varianten sind möglich:
- Filter nach sichtbaren Endpunkten
- Filter nach unsichtbaren Endpunkten
- Kombinierter Filer der sichtbaren Endpunkte mit einer Untermenge von den unsichtbaren Endpunkten

Der Endpunktilter ist ein Ausdruck mit dem Format `ControllerMask[.OperationMask]` und untersützt die Masken `?` und `*`.

Beispiele von Filterausdrücken:
- `WeatherForecast` - alle Endpunkte vom `WeatherForecast` Controller
- `*Audit` - alle Endpunkte vom Controllern deren Namen mit `Audit` endet
- `WeatherForecast.Get*` - alle Endpunkte vom `WeatherForecast` Controller deren Operationsnamen mit `Get*` beginnt
- `*.Get*` - alle Endpunkte deren Operationsnamen mit `Get*` beginnt

Daraus ergibt sich folgende Verwendungsmatrix:

| Mode     | Visible | Hidden | Example |
|:---|:---:|:---:|:---|
| Include  |  ✔️    | ❌     | `"VisibleItems": ["User.*", "WeatherForecast.Get*"]` |
| Exclude  | ❌     | ✔️     | `"HiddenItems": ["User.*", "WeatherForecast.DeleteWeatherForecast"]` |
| Mixed    | ✔️     | ✔️     | `"VisibleItems": ["*.Get*"],`<br />`"HiddenItems": ["User.Get*"]` |

Die Filter werden in der Konfigurationsdatei der Applikation `appsettings.json` im Bereich der `ApiConfiguration` bestimmt. Beispiel `Include` Filter:
```json
"ApiConfiguration": {
  "VisibleItems": [
    "User.*",
    "WeatherForecast.Get*"
  ]
}
```

Beispiel `Exclude` Filter:
```json
"ApiConfiguration": {
  "HiddenItems": [
    "User.*",
    "WeatherForecast.DeleteWeatherForecast"
  ]
}
```

Beispiel `Include` Filter kombiniert mit `Exclude` Filter:
```json
"ApiConfiguration": {
  "VisibleItems": [
    "*.Get*"
  ],
  "HiddenItems": [
    "User.Get*"
  ]
}
```

> Im Entwicklungsmodus empfiehlt sich die Endpunkt Konfiguration in die [User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) auszulagern.

### Filter Convention
ASP.NET bietet mittels [`IActionModelConvention`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.applicationmodels.iactionmodelconvention) die Möglichkeit, die Sichtbarkeit von Endpunkten zu bestimmen. 
Die Implementierung `ApiVisibilityConvention` steuert die Sichtbarkeit des Endpunktes gemäss den 

```csharp
internal sealed class ApiVisibilityConvention : IActionModelConvention
{
    private List<string> VisibleItems { get; }
    private List<string> HiddenItems { get; }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="visibleItems">List of visible items name masks (wildcards: *?)</param>
    /// <param name="hiddenItems">List of hidden items name masks (wildcards: *?)</param>
    internal ApiVisibilityConvention(IEnumerable<string>? visibleItems = null,
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
```

### Filter anwenden
Die Filter werden beim Programmstart aus der Konfiguration `ApiConfiguration` gelesen und die `ApiVisibilityConvention` beim Hinzufügen der Controller registriert:
```csharp
1 public class Program
2 {
3    public static void Main(string[] args)
4    {
5        var builder = WebApplication.CreateBuilder(args);
6
7        // configuration
8        IConfiguration Configuration = new ConfigurationBuilder()
9            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
10            .AddEnvironmentVariables()
11            .AddCommandLine(args)
12            .Build();
13        var apiConfiguration = Configuration.GetSection(nameof(ApiConfiguration)).Get<ApiConfiguration>();
14
15        // Add services to the container.
16        builder.Services.AddControllers(setupAction =>
17        {
18            if (apiConfiguration != null)
19            {
20                setupAction.Conventions.Add(new ApiVisibilityConvention(
21                    apiConfiguration.VisibleItems,
22                    apiConfiguration.HiddenItems));
23            }
24        });
25
26        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
27        builder.Services.AddEndpointsApiExplorer();
28        builder.Services.AddSwaggerGen();
29
30        var app = builder.Build();
31
32        // Configure the HTTP request pipeline.
33        if (app.Environment.IsDevelopment())
34        {
35            app.UseSwagger();
36            // show operation id
37            app.UseSwaggerUI(setupAction =>
38            {
39                setupAction.DisplayOperationId();
40            });
41        }
42
43        app.UseHttpsRedirection();
44
45        app.UseAuthorization();
46
47
48        app.MapControllers();
49
50        app.Run();
51    }
52 }
```

- `8-13` - load the filter configuration
- `16-24` - apply the visibility convention
- `39` - turn on the operation id (optional)

Ist kein Endpunktfilter aktiv, erscheinen im Web UI alle verfügbaren Endpunkte:
<p align="center">
    <img src="docs/AllEndpoints.png" width="500" alt="All Endpoints" />
</p>

Endpunkte mit dem `Include` Filter `"VisibleItems": ["User.*", "WeatherForecast.Get*"]`:
<p align="center">
    <img src="docs/FilterIncludeEndpoints.png" width="500" alt="All Endpoints" />
</p>

Endpunkte mit dem `Exclude` Filter `"HiddenItems": ["User.*", "WeatherForecast.DeleteWeatherForecast"]`:
<p align="center">
    <img src="docs/FilterExcludeEndpoints.png" width="500" alt="All Endpoints" />
</p>

Endpunkte mit dem `Exclude` und `Include` Filter `"VisibleItems": ["*.Get*"],` und `"HiddenItems": ["User.Get*"]`:
<p align="center">
    <img src="docs/FilterMixedEndpoints.png" width="500" alt="All Endpoints" />
</p>


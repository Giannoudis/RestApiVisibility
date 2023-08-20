using RestApiVisibility;

namespace RestApiVisibilityDemo;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // configuration
        IConfiguration Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();
        var apiConfiguration = Configuration.GetSection(nameof(ApiConfiguration)).Get<ApiConfiguration>();

        // Add services to the container.
        builder.Services.AddControllers(setupAction =>
        {
            if (apiConfiguration != null)
            {
                setupAction.Conventions.Add(new ApiVisibilityConvention(
                    apiConfiguration.VisibleItems,
                    apiConfiguration.HiddenItems));
            }
        });

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            // show operation id
            app.UseSwaggerUI(setupAction =>
            {
                setupAction.DisplayOperationId();
            });
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();


        app.MapControllers();

        app.Run();
    }
}
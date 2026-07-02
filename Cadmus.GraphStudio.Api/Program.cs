using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Collections;
using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using Scalar.AspNetCore;

namespace Cadmus.GraphStudio.Api;

public static class Program
{
    private static void DumpEnvironmentVars()
    {
        Console.WriteLine("ENVIRONMENT VARIABLES:");
        IDictionary dct = Environment.GetEnvironmentVariables();
        List<string> keys = [];
        var enumerator = dct.GetEnumerator();
        while (enumerator.MoveNext())
        {
            keys.Add(((DictionaryEntry)enumerator.Current).Key.ToString()!);
        }

        foreach (string key in keys.OrderBy(s => s))
            Console.WriteLine($"{key} = {dct[key]}");
    }

    private static void ConfigureCorsServices(IServiceCollection services,
        ConfigurationManager configuration)
    {
        string[] origins = ["http://localhost:4200"];

        IConfigurationSection section = configuration.GetSection("AllowedOrigins");
        if (section.Exists())
        {
            origins = [.. section.AsEnumerable()
                .Where(p => !string.IsNullOrEmpty(p.Value))
                .Select(p => p.Value!)];
        }

        services.AddCors(o => o.AddPolicy("CorsPolicy", builder =>
        {
            builder.AllowAnyMethod()
                .AllowAnyHeader()
                // https://github.com/aspnet/SignalR/issues/2110 for AllowCredentials
                .AllowCredentials()
                .WithOrigins(origins);
        }));
    }

    public static int Main(string[] args)
    {
        try
        {
            Console.WriteLine("Starting Cadmus Graph Studio API host");
            DumpEnvironmentVars();

            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            // CORS
            ConfigureCorsServices(builder.Services, builder.Configuration);

            // add services to the container
            builder.Services.AddControllers(options =>
                // register custom exception filter
                options.Filters.Add(new CustomExceptionFilterAttribute()));

            // Swagger/OpenAPI: https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddOpenApi();

            var app = builder.Build();

            app.UseCors("CorsPolicy");
            app.UseAuthorization();
            app.MapControllers();
            app.MapOpenApi();
            app.MapScalarApiReference(options =>
                options.WithTitle("Cadmus Graph Studio API"));

            app.Run();
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Cadmus Graph Studio API host terminated unexpectedly");
            Debug.WriteLine(ex.ToString());
            Console.WriteLine(ex.ToString());
            return 1;
        }
    }
}
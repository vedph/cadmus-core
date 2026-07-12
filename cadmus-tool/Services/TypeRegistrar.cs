using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using System;

namespace Cadmus.Cli.Services;

/// <summary>
/// Spectre.Console.Cli type registrar backed by
/// <see cref="IServiceCollection"/>, used to inject dependencies (e.g. from
/// Fusi.Cli.Auth) into commands.
/// </summary>
internal sealed class TypeRegistrar(IServiceCollection builder) : ITypeRegistrar
{
    private readonly IServiceCollection _builder = builder;

    public ITypeResolver Build()
    {
        return new TypeResolver(_builder.BuildServiceProvider());
    }

    public void Register(Type service, Type implementation)
    {
        _builder.AddSingleton(service, implementation);
    }

    public void RegisterInstance(Type service, object implementation)
    {
        _builder.AddSingleton(service, implementation);
    }

    public void RegisterLazy(Type service, Func<object> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        _builder.AddSingleton(service, _ => func());
    }
}

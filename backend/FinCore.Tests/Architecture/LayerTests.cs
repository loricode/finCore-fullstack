using NetArchTest.Rules;

namespace FinCore.Tests.Architecture;

/// <summary>Reglas de dependencia de Clean Architecture (docs/Architecture/README.md).</summary>
public sealed class LayerTests
{
    private static readonly System.Reflection.Assembly Domain = typeof(FinCore.Domain.Security.User).Assembly;
    private static readonly System.Reflection.Assembly Application = typeof(FinCore.Application.DependencyInjection).Assembly;
    private static readonly System.Reflection.Assembly Infrastructure = typeof(FinCore.Infrastructure.DependencyInjection).Assembly;
    private static readonly System.Reflection.Assembly Api = typeof(Program).Assembly;

    private static void AssertSuccess(TestResult result, string rule) =>
        Assert.True(result.IsSuccessful,
            $"{rule}. Tipos que la incumplen: {string.Join(", ", result.FailingTypeNames ?? [])}");

    [Fact]
    public void Domain_no_depende_de_ninguna_otra_capa_ni_de_frameworks() =>
        AssertSuccess(Types.InAssembly(Domain).ShouldNot()
            .HaveDependencyOnAny("FinCore.Application", "FinCore.Infrastructure", "FinCore.Api",
                "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "FluentValidation")
            .GetResult(), "Domain debe ser independiente");

    [Fact]
    public void Application_no_depende_de_Infrastructure_Api_EF_Core_ni_ASP_NET() =>
        AssertSuccess(Types.InAssembly(Application).ShouldNot()
            .HaveDependencyOnAny("FinCore.Infrastructure", "FinCore.Api", "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore", "Microsoft.Data.SqlClient")
            .GetResult(), "Application solo conoce Domain y sus propias interfaces");

    [Fact]
    public void Infrastructure_no_depende_de_Api() =>
        AssertSuccess(Types.InAssembly(Infrastructure).ShouldNot()
            .HaveDependencyOn("FinCore.Api").GetResult(), "Infrastructure no conoce la API");

    [Fact]
    public void Los_controladores_no_usan_persistencia_directamente() =>
        AssertSuccess(Types.InAssembly(Api).That().ResideInNamespace("FinCore.Api.Controllers").ShouldNot()
            .HaveDependencyOnAny("FinCore.Infrastructure.Persistence", "Microsoft.EntityFrameworkCore", "Microsoft.Data.SqlClient")
            .GetResult(), "Los controladores solo hablan con casos de uso");

    [Fact]
    public void Los_casos_de_uso_son_sellados() =>
        AssertSuccess(Types.InAssembly(Application).That().HaveNameEndingWith("Handler").Should().BeSealed()
            .GetResult(), "Los handlers son sealed");
}

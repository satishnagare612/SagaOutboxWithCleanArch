using System.Reflection;
using SagaOutBoxCleanArch.Application.Messaging;
using SagaOutBoxCleanArch.Contracts;
using SagaOutBoxCleanArch.Domain.Orders;
using SagaOutBoxCleanArch.Infrastructure.Persistence;

namespace SagaOutBoxCleanArch.Tests;

/// <summary>Guards the Clean Architecture dependency rule: dependencies point inward only.</summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Order).Assembly;
    private static readonly Assembly Application = typeof(IOutbox).Assembly;
    private static readonly Assembly Contracts = typeof(MessageTypes).Assembly;
    private static readonly Assembly Infrastructure = typeof(ServiceDbContext).Assembly;

    private static IEnumerable<string> ProjectReferences(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith("SagaOutBoxCleanArch.", StringComparison.Ordinal));

    [Fact]
    public void DomainDependsOnNoOtherProject() =>
        Assert.Empty(ProjectReferences(Domain));

    [Fact]
    public void ContractsDependOnNoOtherProject() =>
        Assert.Empty(ProjectReferences(Contracts));

    [Fact]
    public void ApplicationDoesNotDependOnInfrastructure() =>
        Assert.DoesNotContain(ProjectReferences(Application), name => name == Infrastructure.GetName().Name);

    [Fact]
    public void ApplicationDoesNotReferenceEntityFrameworkOrServiceBus() =>
        Assert.DoesNotContain(Application.GetReferencedAssemblies(), reference =>
            reference.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || reference.Name.StartsWith("Azure.Messaging", StringComparison.Ordinal));
}

using Eryph.Core;
using Eryph.ModuleCore.Authorization;
using FluentAssertions;

namespace Eryph.ModuleCore.Tests.Authorization;

public class ScopeHierarchyTests
{
    [Fact]
    public void GetImpliedScopes_WithNullScope_ReturnsEmpty()
    {
        // Act
        var result = ScopeHierarchy.GetImpliedScopes(null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetImpliedScopes_WithEmptyScope_ReturnsEmpty()
    {
        // Act
        var result = ScopeHierarchy.GetImpliedScopes("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetImpliedScopes_WithBasicScope_ReturnsOnlyItself()
    {
        // Act
        var result = ScopeHierarchy.GetImpliedScopes(EryphConstants.Authorization.Scopes.CatletsRead);

        // Assert
        result.Should().BeEquivalentTo(EryphConstants.Authorization.Scopes.CatletsRead);
    }

    [Fact]
    public void GetImpliedScopes_WithWriteScope_ReturnsImpliedScopes()
    {
        // Act
        var result = ScopeHierarchy.GetImpliedScopes(EryphConstants.Authorization.Scopes.CatletsWrite);

        // Assert
        result.Should().BeEquivalentTo(
            EryphConstants.Authorization.Scopes.CatletsWrite,
            EryphConstants.Authorization.Scopes.CatletsRead,
            EryphConstants.Authorization.Scopes.CatletsControl);
    }

    [Fact]
    public void GetImpliedScopes_WithCatletsControl_ReturnsCatletsRead()
    {
        // Act
        var result = ScopeHierarchy.GetImpliedScopes(EryphConstants.Authorization.Scopes.CatletsControl);

        // Assert
        result.Should().BeEquivalentTo(
            EryphConstants.Authorization.Scopes.CatletsControl,
            EryphConstants.Authorization.Scopes.CatletsRead);
    }

    [Fact]
    public void GetImpliedScopes_WithComputeWrite_ReturnsAllComputeScopes()
    {
        // Act
        var result = ScopeHierarchy.GetImpliedScopes(EryphConstants.Authorization.Scopes.ComputeWrite);

        // Assert
        result.Should().BeEquivalentTo(
            EryphConstants.Authorization.Scopes.ComputeWrite,
            EryphConstants.Authorization.Scopes.ComputeRead,
            EryphConstants.Authorization.Scopes.CatletsWrite,
            EryphConstants.Authorization.Scopes.CatletsRead,
            EryphConstants.Authorization.Scopes.CatletsControl,
            EryphConstants.Authorization.Scopes.GenesWrite,
            EryphConstants.Authorization.Scopes.GenesRead,
            EryphConstants.Authorization.Scopes.ProjectsWrite,
            EryphConstants.Authorization.Scopes.ProjectsRead);
    }

    [Fact]
    public void GetImpliedScopes_WithIdentityWrite_ReturnsAllIdentityScopes()
    {
        // Act
        var result = ScopeHierarchy.GetImpliedScopes(EryphConstants.Authorization.Scopes.IdentityWrite);

        // Assert
        result.Should().BeEquivalentTo(
            EryphConstants.Authorization.Scopes.IdentityWrite,
            EryphConstants.Authorization.Scopes.IdentityRead,
            EryphConstants.Authorization.Scopes.IdentityClientsWrite,
            EryphConstants.Authorization.Scopes.IdentityClientsRead);
    }

    [Theory]
    [InlineData("compute:catlets:write", "compute:catlets:read")]
    [InlineData("compute:catlets:control", "compute:catlets:read")]
    [InlineData("compute:genes:write", "compute:genes:read")]
    [InlineData("compute:projects:write", "compute:projects:read")]
    [InlineData("identity:clients:write", "identity:clients:read")]
    public void GetImpliedScopes_WriteScope_ReturnsReadScope(string writeScope, string readScope)
    {
        // Act
        var result = ScopeHierarchy.GetImpliedScopes(writeScope);

        // Assert
        result.Should().Contain(readScope, $"{writeScope} should imply {readScope}");
    }

    [Fact]
    public void ExpandScopes_WithMultipleScopes_ReturnsDistinctExpandedScopes()
    {
        // Arrange
        var scopes = new[]
        {
            EryphConstants.Authorization.Scopes.CatletsWrite,
            EryphConstants.Authorization.Scopes.GenesRead
        };

        // Act
        var result = ScopeHierarchy.ExpandScopes(scopes);

        // Assert
        result.Should().BeEquivalentTo(
            EryphConstants.Authorization.Scopes.CatletsWrite,
            EryphConstants.Authorization.Scopes.CatletsRead,
            EryphConstants.Authorization.Scopes.CatletsControl,
            EryphConstants.Authorization.Scopes.GenesRead
        );
    }

    [Fact]
    public void ExpandScopes_WithMultipleGrantedScopes_ReturnsAllImpliedScopes()
    {
        // Arrange
        var assignedScopes = new[]
        {
            EryphConstants.Authorization.Scopes.CatletsWrite,
            EryphConstants.Authorization.Scopes.GenesRead
        };

        // Act
        var result = ScopeHierarchy.ExpandScopes(assignedScopes);

        // Assert
        result.Should().BeEquivalentTo(
            EryphConstants.Authorization.Scopes.CatletsWrite,
            EryphConstants.Authorization.Scopes.CatletsRead,
            EryphConstants.Authorization.Scopes.CatletsControl,
            EryphConstants.Authorization.Scopes.GenesRead
        );
    }

    [Fact]
    public void GetGrantingScopes_WithNullScope_ReturnsEmpty()
    {
        // Act
        var result = ScopeHierarchy.GetGrantingScopes(null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetGrantingScopes_WithEmptyScope_ReturnsEmpty()
    {
        // Act
        var result = ScopeHierarchy.GetGrantingScopes("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetGrantingScopes_WithBasicScope_ReturnsOnlyItself()
    {
        // Act
        var result = ScopeHierarchy.GetGrantingScopes(EryphConstants.Authorization.Scopes.ComputeWrite);

        // Assert
        result.Should().ContainSingle()
            .Which.Should().Be(EryphConstants.Authorization.Scopes.ComputeWrite);
    }

    [Fact]
    public void GetGrantingScopes_WithCatletsRead_ReturnsAllGrantingScopes()
    {
        // Act
        var result = ScopeHierarchy.GetGrantingScopes(EryphConstants.Authorization.Scopes.CatletsRead);

        // Assert
        result.Should().BeEquivalentTo(
            EryphConstants.Authorization.Scopes.CatletsRead,      // The scope itself
            EryphConstants.Authorization.Scopes.CatletsWrite,     // Direct parent
            EryphConstants.Authorization.Scopes.CatletsControl,   // Another parent  
            EryphConstants.Authorization.Scopes.ComputeWrite,     // Higher level parent
            EryphConstants.Authorization.Scopes.ComputeRead       // Higher level parent
        );
    }

    [Fact]
    public void GetGrantingScopes_WithIdentityClientsRead_ReturnsAllGrantingScopes()
    {
        // Act
        var result = ScopeHierarchy.GetGrantingScopes(EryphConstants.Authorization.Scopes.IdentityClientsRead);

        // Assert
        result.Should().BeEquivalentTo(
            EryphConstants.Authorization.Scopes.IdentityClientsRead,   // The scope itself
            EryphConstants.Authorization.Scopes.IdentityClientsWrite,  // Direct parent
            EryphConstants.Authorization.Scopes.IdentityRead,          // Higher level parent
            EryphConstants.Authorization.Scopes.IdentityWrite          // Highest level parent
        );
    }
}

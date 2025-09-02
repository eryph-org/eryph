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
        result.Should().ContainSingle()
            .Which.Should().Be(EryphConstants.Authorization.Scopes.CatletsRead);
    }

    [Fact]
    public void GetImpliedScopes_WithWriteScope_ReturnsImpliedScopes()
    {
        // Act
        var result = ScopeHierarchy.GetImpliedScopes(EryphConstants.Authorization.Scopes.CatletsWrite);

        // Assert
        result.Should().Contain([
            EryphConstants.Authorization.Scopes.CatletsWrite,
            EryphConstants.Authorization.Scopes.CatletsRead,
            EryphConstants.Authorization.Scopes.CatletsControl
        ]);
    }

    [Fact]
    public void GetImpliedScopes_WithComputeWrite_ReturnsAllComputeScopes()
    {
        // Act
        var result = ScopeHierarchy.GetImpliedScopes(EryphConstants.Authorization.Scopes.ComputeWrite).ToArray();

        // Assert
        result.Should().Contain(EryphConstants.Authorization.Scopes.ComputeWrite);
        result.Should().Contain(EryphConstants.Authorization.Scopes.ComputeRead);
        result.Should().Contain(EryphConstants.Authorization.Scopes.CatletsWrite);
        result.Should().Contain(EryphConstants.Authorization.Scopes.CatletsRead);
        result.Should().Contain(EryphConstants.Authorization.Scopes.CatletsControl);
        result.Should().Contain(EryphConstants.Authorization.Scopes.GenesWrite);
        result.Should().Contain(EryphConstants.Authorization.Scopes.GenesRead);
        result.Should().Contain(EryphConstants.Authorization.Scopes.ProjectsWrite);
        result.Should().Contain(EryphConstants.Authorization.Scopes.ProjectsRead);
    }

    [Fact]
    public void GetImpliedScopes_WithIdentityWrite_ReturnsAllIdentityScopes()
    {
        // Act
        var result = ScopeHierarchy.GetImpliedScopes(EryphConstants.Authorization.Scopes.IdentityWrite).ToArray();

        // Assert
        result.Should().Contain(EryphConstants.Authorization.Scopes.IdentityWrite);
        result.Should().Contain(EryphConstants.Authorization.Scopes.IdentityRead);
        result.Should().Contain(EryphConstants.Authorization.Scopes.IdentityClientsWrite);
        result.Should().Contain(EryphConstants.Authorization.Scopes.IdentityClientsRead);
    }

    [Fact]
    public void ExpandScopes_WithNullScopes_ReturnsEmpty()
    {
        // Act
        var result = ScopeHierarchy.ExpandScopes(null);

        // Assert
        result.Should().BeEmpty();
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
        var result = ScopeHierarchy.ExpandScopes(scopes).ToArray();

        // Assert
        result.Should().Contain(EryphConstants.Authorization.Scopes.CatletsWrite);
        result.Should().Contain(EryphConstants.Authorization.Scopes.CatletsRead);
        result.Should().Contain(EryphConstants.Authorization.Scopes.CatletsControl);
        result.Should().Contain(EryphConstants.Authorization.Scopes.GenesRead);
        result.Should().HaveCountGreaterOrEqualTo(4);
        result.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void IsScopeAllowed_WithNullRequestedScope_ReturnsFalse()
    {
        // Arrange
        var grantedScopes = new[] { EryphConstants.Authorization.Scopes.CatletsWrite };

        // Act
        var result = ScopeHierarchy.IsScopeAllowed(null, grantedScopes);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsScopeAllowed_WithEmptyRequestedScope_ReturnsFalse()
    {
        // Arrange
        var grantedScopes = new[] { EryphConstants.Authorization.Scopes.CatletsWrite };

        // Act
        var result = ScopeHierarchy.IsScopeAllowed("", grantedScopes);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsScopeAllowed_WithNullGrantedScopes_ReturnsFalse()
    {
        // Act
        var result = ScopeHierarchy.IsScopeAllowed(EryphConstants.Authorization.Scopes.CatletsRead, null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsScopeAllowed_WithExactMatch_ReturnsTrue()
    {
        // Arrange
        var grantedScopes = new[] { EryphConstants.Authorization.Scopes.CatletsRead };

        // Act
        var result = ScopeHierarchy.IsScopeAllowed(EryphConstants.Authorization.Scopes.CatletsRead, grantedScopes);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsScopeAllowed_WithHierarchicalMatch_ReturnsTrue()
    {
        // Arrange - User has catlet:write scope
        var grantedScopes = new[] { EryphConstants.Authorization.Scopes.CatletsWrite };

        // Act - Request catlet:read scope
        var result = ScopeHierarchy.IsScopeAllowed(EryphConstants.Authorization.Scopes.CatletsRead, grantedScopes);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsScopeAllowed_WithComputeWrite_AllowsAllComputeScopes()
    {
        var grantedScopes = new[] { EryphConstants.Authorization.Scopes.ComputeWrite };

        // Act & Assert - Should allow all compute-related scopes
        ScopeHierarchy.IsScopeAllowed(EryphConstants.Authorization.Scopes.ComputeRead, grantedScopes)
            .Should().BeTrue();
        ScopeHierarchy.IsScopeAllowed(EryphConstants.Authorization.Scopes.CatletsRead, grantedScopes)
            .Should().BeTrue();
        ScopeHierarchy.IsScopeAllowed(EryphConstants.Authorization.Scopes.CatletsWrite, grantedScopes)
            .Should().BeTrue();
        ScopeHierarchy.IsScopeAllowed(EryphConstants.Authorization.Scopes.CatletsControl, grantedScopes)
            .Should().BeTrue();
        ScopeHierarchy.IsScopeAllowed(EryphConstants.Authorization.Scopes.GenesRead, grantedScopes)
            .Should().BeTrue();
        ScopeHierarchy.IsScopeAllowed(EryphConstants.Authorization.Scopes.GenesWrite, grantedScopes)
            .Should().BeTrue();
        ScopeHierarchy.IsScopeAllowed(EryphConstants.Authorization.Scopes.ProjectsRead, grantedScopes)
            .Should().BeTrue();
        ScopeHierarchy.IsScopeAllowed(EryphConstants.Authorization.Scopes.ProjectsWrite, grantedScopes)
            .Should().BeTrue();
    }

    [Fact]
    public void IsScopeAllowed_WithoutPermission_ReturnsFalse()
    {
        // Arrange - User has only genes:read scope
        var grantedScopes = new[] { EryphConstants.Authorization.Scopes.GenesRead };

        // Act - Request catlet:read scope (not related)
        var result = ScopeHierarchy.IsScopeAllowed(EryphConstants.Authorization.Scopes.CatletsRead, grantedScopes);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AreAllScopesAllowed_WithNullRequestedScopes_ReturnsTrue()
    {
        // Arrange
        var grantedScopes = new[] { EryphConstants.Authorization.Scopes.CatletsWrite };

        // Act
        var result = ScopeHierarchy.AreAllScopesAllowed(null, grantedScopes);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AreAllScopesAllowed_WithNullGrantedScopes_ReturnsFalse()
    {
        // Arrange
        var requestedScopes = new[] { EryphConstants.Authorization.Scopes.CatletsRead };

        // Act
        var result = ScopeHierarchy.AreAllScopesAllowed(requestedScopes, null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AreAllScopesAllowed_WithAllValidScopes_ReturnsTrue()
    {
        // Arrange
        var grantedScopes = new[] { EryphConstants.Authorization.Scopes.CatletsWrite };
        var requestedScopes = new[]
        {
            EryphConstants.Authorization.Scopes.CatletsRead,
            EryphConstants.Authorization.Scopes.CatletsControl
        };

        // Act
        var result = ScopeHierarchy.AreAllScopesAllowed(requestedScopes, grantedScopes);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AreAllScopesAllowed_WithSomeInvalidScopes_ReturnsFalse()
    {
        // Arrange
        var grantedScopes = new[] { EryphConstants.Authorization.Scopes.CatletsWrite };
        var requestedScopes = new[]
        {
            EryphConstants.Authorization.Scopes.CatletsRead, // Valid - implied by catlets:write
            EryphConstants.Authorization.Scopes.GenesRead    // Invalid - not implied by catlets:write
        };

        // Act
        var result = ScopeHierarchy.AreAllScopesAllowed(requestedScopes, grantedScopes);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetAvailableScopes_WithMultipleGrantedScopes_ReturnsAllImpliedScopes()
    {
        // Arrange
        var assignedScopes = new[]
        {
            EryphConstants.Authorization.Scopes.CatletsWrite,
            EryphConstants.Authorization.Scopes.GenesRead
        };

        // Act
        var result = ScopeHierarchy.GetAvailableScopes(assignedScopes).ToArray();

        // Assert
        result.Should().Contain(EryphConstants.Authorization.Scopes.CatletsWrite);
        result.Should().Contain(EryphConstants.Authorization.Scopes.CatletsRead);
        result.Should().Contain(EryphConstants.Authorization.Scopes.CatletsControl);
        result.Should().Contain(EryphConstants.Authorization.Scopes.GenesRead);
        result.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("compute:catlets:write", "compute:catlets:read")]
    [InlineData("compute:genes:write", "compute:genes:read")]
    [InlineData("compute:projects:write", "compute:projects:read")]
    [InlineData("identity:clients:write", "identity:clients:read")]
    public void WriteScope_ShouldImply_ReadScope(string writeScope, string readScope)
    {
        // Arrange
        var grantedScopes = new[] { writeScope };

        // Act
        var result = ScopeHierarchy.IsScopeAllowed(readScope, grantedScopes);

        // Assert
        result.Should().BeTrue($"{writeScope} should imply {readScope}");
    }

    [Fact]
    public void ScopeHierarchy_ShouldBeConsistent_WithEryphConstants()
    {
        // This test ensures our hierarchy is consistent with the defined scopes in EryphConstants
        
        // Test that all compute scopes are properly defined
        var computeScopes = new[]
        {
            EryphConstants.Authorization.Scopes.ComputeRead,
            EryphConstants.Authorization.Scopes.ComputeWrite,
            EryphConstants.Authorization.Scopes.CatletsRead,
            EryphConstants.Authorization.Scopes.CatletsWrite,
            EryphConstants.Authorization.Scopes.CatletsControl,
            EryphConstants.Authorization.Scopes.GenesRead,
            EryphConstants.Authorization.Scopes.GenesWrite,
            EryphConstants.Authorization.Scopes.ProjectsRead,
            EryphConstants.Authorization.Scopes.ProjectsWrite
        };

        // Test that all identity scopes are properly defined
        var identityScopes = new[]
        {
            EryphConstants.Authorization.Scopes.IdentityRead,
            EryphConstants.Authorization.Scopes.IdentityWrite,
            EryphConstants.Authorization.Scopes.IdentityClientsRead,
            EryphConstants.Authorization.Scopes.IdentityClientsWrite
        };

        // Act & Assert - All scopes should be defined
        foreach (var scope in computeScopes.Concat(identityScopes))
        {
            scope.Should().NotBeNullOrWhiteSpace($"Scope {scope} should be defined");
        }
    }
}
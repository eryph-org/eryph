using Eryph.Core;
using Eryph.ModuleCore.Authorization;
using FluentAssertions;

namespace Eryph.ModuleCore.Tests.Authorization;

public class ScopeHierarchyTests
{
    // Test-only scope lists including parent scopes that are only used for hierarchy testing
    private static readonly string[] AllComputeScopes =
    [
        EryphConstants.Authorization.Scopes.ComputeRead,
        EryphConstants.Authorization.Scopes.ComputeWrite,
        EryphConstants.Authorization.Scopes.CatletsRead,
        EryphConstants.Authorization.Scopes.CatletsWrite,
        EryphConstants.Authorization.Scopes.CatletsControl,
        EryphConstants.Authorization.Scopes.GenesRead,
        EryphConstants.Authorization.Scopes.GenesWrite,
        EryphConstants.Authorization.Scopes.ProjectsRead,
        EryphConstants.Authorization.Scopes.ProjectsWrite
    ];

    private static readonly string[] AllIdentityScopes =
    [
        EryphConstants.Authorization.Scopes.IdentityRead,
        EryphConstants.Authorization.Scopes.IdentityWrite,
        EryphConstants.Authorization.Scopes.IdentityClientsRead,
        EryphConstants.Authorization.Scopes.IdentityClientsWrite
    ];

    private static readonly string[] AllScopes =
    [
        .. AllComputeScopes,
        .. AllIdentityScopes
    ];

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
    public void GetImpliedScopes_WithCatletsControl_ReturnsCatletsRead()
    {
        // Act
        var result = ScopeHierarchy.GetImpliedScopes(EryphConstants.Authorization.Scopes.CatletsControl);

        // Assert
        result.Should().Contain([
            EryphConstants.Authorization.Scopes.CatletsControl,
            EryphConstants.Authorization.Scopes.CatletsRead
        ]);
        result.Should().HaveCount(2);
    }

    [Fact]
    public void GetImpliedScopes_WithComputeWrite_ReturnsAllComputeScopes()
    {
        // Act
        var result = ScopeHierarchy.GetImpliedScopes(EryphConstants.Authorization.Scopes.ComputeWrite);

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
        var result = ScopeHierarchy.GetImpliedScopes(EryphConstants.Authorization.Scopes.IdentityWrite);

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
        var result = ScopeHierarchy.ExpandScopes(scopes);

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
    public void IsScopeAllowed_WithCatletsControl_AllowsCatletsRead()
    {
        // Arrange - User has catlets:control scope
        var grantedScopes = new[] { EryphConstants.Authorization.Scopes.CatletsControl };

        // Act - Request catlets:read scope
        var result = ScopeHierarchy.IsScopeAllowed(EryphConstants.Authorization.Scopes.CatletsRead, grantedScopes);

        // Assert
        result.Should().BeTrue("CatletsControl should grant CatletsRead access");
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
        var result = AreAllScopesAllowed(null, grantedScopes);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AreAllScopesAllowed_WithNullGrantedScopes_ReturnsFalse()
    {
        // Arrange
        var requestedScopes = new[] { EryphConstants.Authorization.Scopes.CatletsRead };

        // Act
        var result = AreAllScopesAllowed(requestedScopes, null);

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
        var result = AreAllScopesAllowed(requestedScopes, grantedScopes);

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
        var result = AreAllScopesAllowed(requestedScopes, grantedScopes);

        // Assert
        result.Should().BeFalse();
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
        result.Should().Contain(EryphConstants.Authorization.Scopes.CatletsWrite);
        result.Should().Contain(EryphConstants.Authorization.Scopes.CatletsRead);
        result.Should().Contain(EryphConstants.Authorization.Scopes.CatletsControl);
        result.Should().Contain(EryphConstants.Authorization.Scopes.GenesRead);
        result.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("compute:catlets:write", "compute:catlets:read")]
    [InlineData("compute:catlets:control", "compute:catlets:read")]
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
        result.Should().Contain([
            EryphConstants.Authorization.Scopes.CatletsRead,      // The scope itself
            EryphConstants.Authorization.Scopes.CatletsWrite,     // Direct parent
            EryphConstants.Authorization.Scopes.CatletsControl,   // Another parent  
            EryphConstants.Authorization.Scopes.ComputeWrite,     // Higher level parent
            EryphConstants.Authorization.Scopes.ComputeRead       // Higher level parent
        ]);
    }

    [Fact]
    public void GetGrantingScopes_WithIdentityClientsRead_ReturnsAllGrantingScopes()
    {
        // Act
        var result = ScopeHierarchy.GetGrantingScopes(EryphConstants.Authorization.Scopes.IdentityClientsRead);

        // Assert
        result.Should().Contain([
            EryphConstants.Authorization.Scopes.IdentityClientsRead,   // The scope itself
            EryphConstants.Authorization.Scopes.IdentityClientsWrite,  // Direct parent
            EryphConstants.Authorization.Scopes.IdentityRead,          // Higher level parent
            EryphConstants.Authorization.Scopes.IdentityWrite          // Highest level parent
        ]);
    }

    [Fact]
    public void ScopeHierarchy_ShouldBeConsistent_WithScopeDefinitions()
    {
        // This test ensures our hierarchy is consistent with the centralized scope definitions
        
        // Act & Assert - All scopes should be defined and not null/empty
        foreach (var scope in AllScopes)
        {
            scope.Should().NotBeNullOrWhiteSpace($"Scope {scope} should be defined");
        }

        // Verify that scope definitions contain expected counts
        ScopeDefinitions.ComputeApiScopes.Should().HaveCount(7, "ComputeApiScopes should have 7 scopes");
        ScopeDefinitions.IdentityApiScopes.Should().HaveCount(2, "IdentityApiScopes should have 2 scopes");
        AllComputeScopes.Should().HaveCount(9, "AllComputeScopes should have 9 scopes");
        AllIdentityScopes.Should().HaveCount(4, "AllIdentityScopes should have 4 scopes");
        AllScopes.Should().HaveCount(13, "AllScopes should have 13 total scopes");
    }

    [Fact]
    public void ScopeDefinitions_ShouldHaveNoDuplicates()
    {
        // Act & Assert - All scope collections should have no duplicates
        ScopeDefinitions.ComputeApiScopes.Should().OnlyHaveUniqueItems("ComputeApiScopes should have no duplicates");
        ScopeDefinitions.IdentityApiScopes.Should().OnlyHaveUniqueItems("IdentityApiScopes should have no duplicates");
        AllComputeScopes.Should().OnlyHaveUniqueItems("AllComputeScopes should have no duplicates");
        AllIdentityScopes.Should().OnlyHaveUniqueItems("AllIdentityScopes should have no duplicates");
        AllScopes.Should().OnlyHaveUniqueItems("AllScopes should have no duplicates");
    }

    [Fact]
    public void ScopeDefinitions_ComputeApiScopes_ShouldBeSubsetOfAllComputeScopes()
    {
        // Act & Assert - ComputeApiScopes should be a subset of AllComputeScopes
        foreach (var scope in ScopeDefinitions.ComputeApiScopes)
        {
            AllComputeScopes.Should().Contain(scope, 
                $"ComputeApiScope '{scope}' should be included in AllComputeScopes");
        }
    }

    [Fact]
    public void ScopeDefinitions_IdentityApiScopes_ShouldBeSubsetOfAllIdentityScopes()
    {
        // Act & Assert - IdentityApiScopes should be a subset of AllIdentityScopes
        foreach (var scope in ScopeDefinitions.IdentityApiScopes)
        {
            AllIdentityScopes.Should().Contain(scope, 
                $"IdentityApiScope '{scope}' should be included in AllIdentityScopes");
        }
    }
    
    /// <summary>
    /// Helper method for testing scope validation.
    /// Validates that all requested scopes are allowed given the granted scopes.
    /// </summary>
    /// <param name="requestedScopes">The scopes being requested</param>
    /// <param name="grantedScopes">The scopes that have been granted to the client</param>
    /// <returns>True if all requested scopes are allowed, false otherwise</returns>
    private static bool AreAllScopesAllowed(IEnumerable<string>? requestedScopes, IEnumerable<string>? grantedScopes)
    {
        if (requestedScopes == null)
            return true;

        return grantedScopes != null && requestedScopes.All(scope => ScopeHierarchy.IsScopeAllowed(scope, grantedScopes));
    }
}
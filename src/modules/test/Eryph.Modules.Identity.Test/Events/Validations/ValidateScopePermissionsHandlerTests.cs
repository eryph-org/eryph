using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core;
using Eryph.Modules.Identity.Events.Validations;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Xunit;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Eryph.Modules.Identity.Test.Events.Validations;

public class ValidateScopePermissionsHandlerTests
{
    private readonly Mock<IOpenIddictApplicationManager> _mockApplicationManager;
    private readonly Mock<IOpenIddictScopeManager> _mockScopeManager;
    private readonly Mock<ILogger<ValidateScopePermissionsHandler>> _mockLogger;
    private readonly ValidateScopePermissionsHandler _handler;

    public ValidateScopePermissionsHandlerTests()
    {
        _mockApplicationManager = new Mock<IOpenIddictApplicationManager>();
        _mockScopeManager = new Mock<IOpenIddictScopeManager>();
        _mockLogger = new Mock<ILogger<ValidateScopePermissionsHandler>>();
        _handler = new ValidateScopePermissionsHandler(_mockApplicationManager.Object, _mockScopeManager.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullApplicationManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ValidateScopePermissionsHandler(null!, _mockScopeManager.Object, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullScopeManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ValidateScopePermissionsHandler(_mockApplicationManager.Object, null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ValidateScopePermissionsHandler(_mockApplicationManager.Object, _mockScopeManager.Object, null!));
    }

    [Fact]
    public async Task HandleAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await _handler.HandleAsync(null!));
    }

    [Fact]
    public async Task HandleAsync_WithNoRequestedScopes_DoesNothing()
    {
        // Arrange
        var context = CreateContext("test-client", []);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.IsRejected.Should().BeFalse();
        _mockApplicationManager.Verify(x => x.FindByClientIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentClient_RejectsWithInvalidClient()
    {
        // Arrange
        var context = CreateContext("non-existent-client", [EryphConstants.Authorization.Scopes.CatletsRead]);
        _mockApplicationManager
            .Setup(x => x.FindByClientIdAsync("non-existent-client", It.IsAny<CancellationToken>()))
            .ReturnsAsync(null!);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.IsRejected.Should().BeTrue();
        context.Error.Should().Be(OpenIddictConstants.Errors.InvalidClient);
        context.ErrorDescription.Should().Be("The client application cannot be found.");
    }

    [Fact]
    public async Task HandleAsync_WithValidExactScopeMatch_Succeeds()
    {
        // Arrange
        var clientId = "test-client";
        var requestedScopes = new[] { EryphConstants.Authorization.Scopes.CatletsRead };
        var applicationPermissions = new[] { $"{OpenIddictConstants.Permissions.Prefixes.Scope}{EryphConstants.Authorization.Scopes.CatletsRead}" };
        
        var context = CreateContext(clientId, requestedScopes);
        var mockApplication = new Mock<object>();
        
        _mockApplicationManager
            .Setup(x => x.FindByClientIdAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockApplication.Object);
        
        _mockApplicationManager
            .Setup(x => x.GetPermissionsAsync(mockApplication.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync([..applicationPermissions]);
        
        _mockScopeManager
            .Setup(x => x.FindByNameAsync(EryphConstants.Authorization.Scopes.CatletsRead, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<object>().Object);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.IsRejected.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WithValidHierarchicalScopeMatch_Succeeds()
    {
        // Arrange
        var clientId = "test-client";
        var requestedScopes = new[] { EryphConstants.Authorization.Scopes.CatletsRead }; // Request read scope
        var applicationPermissions = new[] { $"{OpenIddictConstants.Permissions.Prefixes.Scope}{EryphConstants.Authorization.Scopes.CatletsWrite}" }; 

        var context = CreateContext(clientId, requestedScopes);
        var mockApplication = new Mock<object>();
        
        _mockApplicationManager
            .Setup(x => x.FindByClientIdAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockApplication.Object);
        
        _mockApplicationManager
            .Setup(x => x.GetPermissionsAsync(mockApplication.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync([..applicationPermissions]);
        
        _mockScopeManager
            .Setup(x => x.FindByNameAsync(EryphConstants.Authorization.Scopes.CatletsRead, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<object>().Object);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.IsRejected.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WithComputeWriteScope_AllowsAllComputeScopes()
    {
        // Arrange
        var clientId = "test-client";
        var requestedScopes = new[]
        {
            EryphConstants.Authorization.Scopes.CatletsRead,
            EryphConstants.Authorization.Scopes.GenesRead,
            EryphConstants.Authorization.Scopes.ProjectsRead,
            EryphConstants.Authorization.Scopes.ComputeRead
        };
        var applicationPermissions = new[] { $"{OpenIddictConstants.Permissions.Prefixes.Scope}{EryphConstants.Authorization.Scopes.ComputeWrite}" };
        
        var context = CreateContext(clientId, requestedScopes);
        var mockApplication = new Mock<object>();
        
        _mockApplicationManager
            .Setup(x => x.FindByClientIdAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockApplication.Object);
        
        _mockApplicationManager
            .Setup(x => x.GetPermissionsAsync(mockApplication.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync([..applicationPermissions]);
        
        // Mock all requested scopes exist
        foreach (var scope in requestedScopes)
        {
            _mockScopeManager
                .Setup(x => x.FindByNameAsync(scope, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<object>().Object);
        }

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.IsRejected.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WithInvalidScope_RejectsWithInvalidScope()
    {
        // Arrange
        var clientId = "test-client";
        var requestedScopes = new[] { EryphConstants.Authorization.Scopes.GenesRead }; // Request genes:read
        var applicationPermissions = new[] { $"{OpenIddictConstants.Permissions.Prefixes.Scope}{EryphConstants.Authorization.Scopes.CatletsWrite}" }; // Client only has catlets:write
        
        var context = CreateContext(clientId, requestedScopes);
        var mockApplication = new Mock<object>();
        
        _mockApplicationManager
            .Setup(x => x.FindByClientIdAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockApplication.Object);
        
        _mockApplicationManager
            .Setup(x => x.GetPermissionsAsync(mockApplication.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync([..applicationPermissions]);
        
        // Mock scope exists for the invalid scope test (scope exists but permission is missing)
        _mockScopeManager
            .Setup(x => x.FindByNameAsync(EryphConstants.Authorization.Scopes.GenesRead, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<object>().Object);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.IsRejected.Should().BeTrue();
        context.Error.Should().Be(OpenIddictConstants.Errors.InvalidScope);
        context.ErrorDescription.Should().Be("The specified scope is not supported.");
    }

    [Fact]
    public async Task HandleAsync_WithMixedValidAndInvalidScopes_RejectsWithInvalidScope()
    {
        // Arrange
        var clientId = "test-client";
        var requestedScopes = new[]
        {
            EryphConstants.Authorization.Scopes.CatletsRead, // Valid - implied by catlets:write
            EryphConstants.Authorization.Scopes.GenesRead    // Invalid - not implied by catlets:write
        };
        var applicationPermissions = new[] { $"{OpenIddictConstants.Permissions.Prefixes.Scope}{EryphConstants.Authorization.Scopes.CatletsWrite}" };
        
        var context = CreateContext(clientId, requestedScopes);
        var mockApplication = new Mock<object>();
        
        _mockApplicationManager
            .Setup(x => x.FindByClientIdAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockApplication.Object);
        
        _mockApplicationManager
            .Setup(x => x.GetPermissionsAsync(mockApplication.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync([..applicationPermissions]);
        
        // Mock all requested scopes exist
        foreach (var scope in requestedScopes)
        {
            _mockScopeManager
                .Setup(x => x.FindByNameAsync(scope, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<object>().Object);
        }

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.IsRejected.Should().BeTrue();
        context.Error.Should().Be(OpenIddictConstants.Errors.InvalidScope);
    }

    [Fact]
    public async Task HandleAsync_WithIdentityWriteScope_AllowsAllIdentityScopes()
    {
        // Arrange
        var clientId = "test-client";
        var requestedScopes = new[]
        {
            EryphConstants.Authorization.Scopes.IdentityRead,
            EryphConstants.Authorization.Scopes.IdentityClientsRead,
            EryphConstants.Authorization.Scopes.IdentityClientsWrite
        };
        var applicationPermissions = new[] { $"{OpenIddictConstants.Permissions.Prefixes.Scope}{EryphConstants.Authorization.Scopes.IdentityWrite}" };
        
        var context = CreateContext(clientId, requestedScopes);
        var mockApplication = new Mock<object>();
        
        _mockApplicationManager
            .Setup(x => x.FindByClientIdAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockApplication.Object);
        
        _mockApplicationManager
            .Setup(x => x.GetPermissionsAsync(mockApplication.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync([..applicationPermissions]);
        
        // Mock all requested scopes exist
        foreach (var scope in requestedScopes)
        {
            _mockScopeManager
                .Setup(x => x.FindByNameAsync(scope, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<object>().Object);
        }

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.IsRejected.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WithNonScopePermissions_IgnoresNonScopePermissions()
    {
        // Arrange
        var clientId = "test-client";
        var requestedScopes = new[] { EryphConstants.Authorization.Scopes.CatletsRead };
        var applicationPermissions = new[]
        {
            $"{OpenIddictConstants.Permissions.Prefixes.Scope}{EryphConstants.Authorization.Scopes.CatletsWrite}",
            "some:other:permission", // Non-scope permission should be ignored
            OpenIddictConstants.Permissions.Endpoints.Token // Non-scope permission should be ignored
        };
        
        var context = CreateContext(clientId, requestedScopes);
        var mockApplication = new Mock<object>();
        
        _mockApplicationManager
            .Setup(x => x.FindByClientIdAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockApplication.Object);
        
        _mockApplicationManager
            .Setup(x => x.GetPermissionsAsync(mockApplication.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync([..applicationPermissions]);
        
        _mockScopeManager
            .Setup(x => x.FindByNameAsync(EryphConstants.Authorization.Scopes.CatletsRead, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<object>().Object);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.IsRejected.Should().BeFalse();
    }

    [Theory]
    [InlineData("compute:catlets:write", "compute:catlets:read")]
    [InlineData("compute:catlets:write", "compute:catlets:control")]
    [InlineData("compute:genes:write", "compute:genes:read")]
    [InlineData("compute:projects:write", "compute:projects:read")]
    [InlineData("identity:write", "identity:read")]
    [InlineData("identity:clients:write", "identity:clients:read")]
    public async Task HandleAsync_WriteScope_AllowsCorrespondingReadScope(string grantedScope, string requestedScope)
    {
        // Arrange
        var clientId = "test-client";
        var requestedScopes = new[] { requestedScope };
        var applicationPermissions = new[] { $"{OpenIddictConstants.Permissions.Prefixes.Scope}{grantedScope}" };
        
        var context = CreateContext(clientId, requestedScopes);
        var mockApplication = new Mock<object>();
        
        _mockApplicationManager
            .Setup(x => x.FindByClientIdAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockApplication.Object);
        
        _mockApplicationManager
            .Setup(x => x.GetPermissionsAsync(mockApplication.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync([..applicationPermissions]);

        _mockScopeManager
            .Setup(x => x.FindByNameAsync(requestedScope, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<object>().Object);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.IsRejected.Should().BeFalse($"{grantedScope} should allow {requestedScope}");
    }

    [Fact]
    public async Task HandleAsync_WithBuiltInScopes_AllowsOpenIdAndOfflineAccess()
    {
        // Arrange
        var clientId = "test-client";
        var requestedScopes = new[] { OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.OfflineAccess };
        var applicationPermissions = new[] { $"{OpenIddictConstants.Permissions.Prefixes.Scope}some:other:scope" }; // Client has different permissions
        
        var context = CreateContext(clientId, requestedScopes);
        var mockApplication = new Mock<object>();
        
        _mockApplicationManager
            .Setup(x => x.FindByClientIdAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockApplication.Object);
        
        _mockApplicationManager
            .Setup(x => x.GetPermissionsAsync(mockApplication.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync([..applicationPermissions]);
        
        // Mock scope existence for built-in scopes
        _mockScopeManager
            .Setup(x => x.FindByNameAsync(OpenIddictConstants.Scopes.OpenId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<object>().Object);
        
        _mockScopeManager
            .Setup(x => x.FindByNameAsync(OpenIddictConstants.Scopes.OfflineAccess, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<object>().Object);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.IsRejected.Should().BeFalse("Built-in OpenID scopes should be automatically allowed");
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentScope_RejectsWithInvalidScope()
    {
        // Arrange
        var clientId = "test-client";
        var requestedScopes = new[] { "non-existent-scope" };
        var applicationPermissions = new[] { $"{OpenIddictConstants.Permissions.Prefixes.Scope}{EryphConstants.Authorization.Scopes.CatletsWrite}" };
        
        var context = CreateContext(clientId, requestedScopes);
        var mockApplication = new Mock<object>();
        
        _mockApplicationManager
            .Setup(x => x.FindByClientIdAsync(clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockApplication.Object);
        
        _mockApplicationManager
            .Setup(x => x.GetPermissionsAsync(mockApplication.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync([..applicationPermissions]);
        
        // Mock that the scope doesn't exist
        _mockScopeManager
            .Setup(x => x.FindByNameAsync("non-existent-scope", It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        context.IsRejected.Should().BeTrue();
        context.Error.Should().Be(OpenIddictConstants.Errors.InvalidScope);
        context.ErrorDescription.Should().Be("The specified scope is not supported.");
    }

    private static ValidateTokenRequestContext CreateContext(string clientId, IEnumerable<string> requestedScopes)
    {
        // Create a real OpenIddictRequest and populate it with scopes
        var request = new OpenIddictRequest
        {
            Scope = string.Join(" ", requestedScopes)
        };

        var mockTransaction = new Mock<OpenIddictServerTransaction>();
        mockTransaction.SetupGet(t => t.Request).Returns(request);

        var mockContext = new Mock<ValidateTokenRequestContext>(mockTransaction.Object) { CallBase = true };
        mockContext.SetupGet(c => c.ClientId).Returns(clientId);

        return mockContext.Object;
    }
}


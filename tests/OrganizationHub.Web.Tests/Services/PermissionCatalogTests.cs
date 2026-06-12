using System.Collections.Generic;
using System.Linq;
using OrganizationHub.Web.Domain;
using OrganizationHub.Web.Models;
using Xunit;

namespace OrganizationHub.Web.Tests.Services;

public sealed class PermissionCatalogTests
{
    [Fact]
    public void PermissionDefinitionsHaveUniqueKeys()
    {
        IReadOnlyList<PermissionDefinition> definitions = PermissionCatalog.GetDefinitions();

        List<string> duplicateKeys = definitions
            .GroupBy(definition => definition.Key)
            .Where(definitionGroup => definitionGroup.Count() > 1)
            .Select(definitionGroup => definitionGroup.Key)
            .ToList();

        Assert.Empty(duplicateKeys);
    }

    [Fact]
    public void PermissionDefinitionsHaveDisplayMetadata()
    {
        IReadOnlyList<PermissionDefinition> definitions = PermissionCatalog.GetDefinitions();

        Assert.NotEmpty(definitions);
        Assert.All(definitions, definition =>
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Key));
            Assert.False(string.IsNullOrWhiteSpace(definition.Category));
            Assert.False(string.IsNullOrWhiteSpace(definition.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(definition.Description));
        });
    }
}

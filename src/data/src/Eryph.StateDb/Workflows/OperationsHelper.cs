using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Eryph.Messages;
using Eryph.Messages.Resources;
using Eryph.StateDb.Model;
using Microsoft.EntityFrameworkCore;
using Resource = Eryph.Resources.Resource;

namespace Eryph.StateDb.Workflows;

public static class OperationsHelper
{
    public static async Task<(List<OperationResourceModel> Resources, List<OperationProjectModel> Projects)> GetCommandProjectsAndResources(object command, StateStoreContext db)
    {
        var projects = new List<Guid>();

        if (command is IHasProjectId hasProjectId)
            projects.Add(hasProjectId.ProjectId);

        if (command is IHasProjectName hasProjectName)
        {
            var project = await db.Projects.FirstOrDefaultAsync(x =>
                x.TenantId == hasProjectName.TenantId && x.Name == hasProjectName.ProjectName);

            if(project != null)
                projects.Add(project.Id);
        }

        var resources = command switch
        {
            IHasResources { Resources: not null } resourcesCommand => resourcesCommand.Resources,
            IHasResource resourceCommand when resourceCommand.Resource.Id != Guid.Empty => new[] { resourceCommand.Resource },
            _ => Array.Empty<Resource>()
        };

        resources = resources.Where(x=>x.Id!= Guid.Empty).Distinct().ToArray();

        var resourceIds = resources.Select(x => x.Id);
        var projectsOfResources = await db.Resources
            .Where(x => resourceIds.Contains(x.Id)).Select(x => x.ProjectId)
            .Distinct()
            .ToArrayAsync();

        projects.AddRange(projectsOfResources);
        projects = projects.Distinct().ToList();

        return (resources.Select(x => new OperationResourceModel
                { ResourceId = x.Id, ResourceType = x.Type })
            .ToList(), projects.Select(p => new OperationProjectModel { ProjectId = p })
            .ToList());

    }
}
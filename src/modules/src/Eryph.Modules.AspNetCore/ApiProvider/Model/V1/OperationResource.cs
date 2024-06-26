﻿using System.ComponentModel.DataAnnotations;
using Eryph.Resources;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1
{
    public class OperationResource
    {
        [Key] public string Id { get; set; } = null!;

        public string? ResourceId { get; set; }
        public ResourceType ResourceType { get; set; }
    }
}
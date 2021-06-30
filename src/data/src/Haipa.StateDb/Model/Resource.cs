using System;
using System.ComponentModel.DataAnnotations;
using Haipa.Resources;

namespace Haipa.StateDb.Model
{
    public class Resource
    {
        [Key] public Guid Id { get; set; }

        public ResourceType ResourceType { get; set; }

        public string Name { get; set; }
    }
}
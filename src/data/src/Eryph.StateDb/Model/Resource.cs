using System;
using System.ComponentModel.DataAnnotations;
using Eryph.Resources;

namespace Eryph.StateDb.Model
{
    public class Resource
    {
        [Key] public Guid Id { get; set; }

        public ResourceType ResourceType { get; set; }

        public string Name { get; set; }
    }
}
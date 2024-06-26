﻿using System;
using Eryph.ConfigModel;

namespace Eryph.Resources.Disks
{
    public class DiskInfo
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string StorageIdentifier { get; set; }
        public string DataStore { get; set; }

        public Guid? ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string Environment { get; set; }
        public string Geneset { get; set; }
        public Guid DiskIdentifier { get; set; }


        public bool Frozen { get; set; }


        [PrivateIdentifier]

        public string Path { get; set; }

        [PrivateIdentifier]
        public string FileName { get; set; }

        public long? SizeBytes { get; set; }
        public long? UsedSizeBytes { get; set; }

        public DiskInfo Parent { get; set; }
    }
}
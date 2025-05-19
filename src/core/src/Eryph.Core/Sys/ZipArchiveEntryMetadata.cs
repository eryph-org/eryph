using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Core.Sys;

public record ZipArchiveEntryMetadata(
    string FullName,
    long Length,
    uint Crc32);

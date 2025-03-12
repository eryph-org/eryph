using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Eryph.VmManagement.Storage;
using FluentAssertions;
using FluentAssertions.LanguageExt;
using LanguageExt;
using LanguageExt.Common;
using Xunit;

namespace Eryph.VmManagement.Test.Storage
{
    public class DiskGenerationNamesTests
    {

        [Fact]
        public void AddGenerationSuffix_GenerationZero_ReturnsPathWithoutSuffix()
        {
            var result = DiskGenerationNames.AddGenerationSuffix(@"Z:\disk\test-disk.vhdx", 0);

            result.Should().BeRight()
                .Which.Should().Be(@"Z:\disk\test-disk.vhdx");
        }

        [Fact]
        public void AddGenerationSuffix_GenerationOne_ReturnsPathWithSuffix()
        {
            var result = DiskGenerationNames.AddGenerationSuffix(@"Z:\disk\test-disk.vhdx", 1);

            result.Should().BeRight()
                .Which.Should().Be(@"Z:\disk\test-disk_g1.vhdx");
        }

        [Fact]
        public void AddGenerationSuffix_InvalidPath_ReturnsError()
        {
            var result = DiskGenerationNames.AddGenerationSuffix(@"Z:\disk\test-disk$.vhdx", 1);

            result.Should().BeRight()
                .Which.Should().Be(@"Z:\disk\test-disk_g1.vhdx");
        }
    }
}

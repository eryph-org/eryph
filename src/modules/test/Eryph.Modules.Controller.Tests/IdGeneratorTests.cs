using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Eryph.Modules.Controller.Tests;

public class IdGeneratorTests
{
    [Fact]
    public void Generator_settings_are_correct()
    {
        var idGenerator = IdGeneratorFactory.CreateIdGenerator();

        idGenerator.Options.TimeSource.Epoch.Should().Be(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        idGenerator.Options.TimeSource.TickDuration.Should().Be(TimeSpan.FromMilliseconds(1));
        idGenerator.Options.IdStructure.GeneratorIdBits.Should().Be(10);
        idGenerator.Options.IdStructure.SequenceBits.Should().Be(12);
        idGenerator.Options.IdStructure.TimestampBits.Should().Be(41);
    }
}

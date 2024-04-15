using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IdGen;

namespace Eryph.Modules.Controller;

public static class IdGeneratorFactory
{
    // The next values must not be changed as they define the structure
    // of the generated IDs. Any changes can lead to the generation of
    // conflicting IDs.
    private static readonly DateTimeOffset Epoch = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan TickDuration = TimeSpan.FromMilliseconds(1);
    private static readonly IdStructure IdStructure = new(41, 10, 12);

    // The time source is guaranteed to be monotonic. We use a static property
    // as an additional insurance against the creation of multiple time sources.
    // The ID generator should be a singleton anyway.
    private static readonly ITimeSource TimeSource = new DefaultTimeSource(Epoch, TickDuration);

    /// <summary>
    /// Create the <see cref="IdGenerator"/>. The returned instance should
    /// be used as a singleton.
    /// </summary>
    public static IdGenerator CreateIdGenerator()
    {
        return new IdGenerator(0, new IdGeneratorOptions(
            IdStructure, TimeSource, SequenceOverflowStrategy.SpinWait));
    }
}

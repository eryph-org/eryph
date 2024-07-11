using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using FluentAssertions;
using Moq;
using Rebus.Persistence.InMem;
using Rebus.Sagas;

namespace Eryph.ModuleCore.Tests;

public class EryphSagaDataTests
{
    [Fact]
    public async Task Can_serialize_and_deserialize()
    {
        var id = Guid.NewGuid();
        var sagaTaskId = Guid.NewGuid();

        var sagaData = new EryphSagaData<TestData>
        {
            Id = id,
            SagaTaskId = sagaTaskId
        };
        sagaData.Data.GeneIds =
        [
            GeneIdentifier.New("gene:acme/acme-fodder/1.0:first-fodder"),
            GeneIdentifier.New("gene:acme/acme-fodder/1.0:second-fodder"),
        ];
        sagaData.Data.Ancestors =
        [
            new AncestorInfo(
                GeneSetIdentifier.New("acme/acme-parent/latest"),
                GeneSetIdentifier.New("acme/acme-parent/1.0")),
            new AncestorInfo(
                GeneSetIdentifier.New("acme/acme-grand-parent/latest"),
                GeneSetIdentifier.New("acme/acme-grand-parent/1.0")),
        ];
        sagaData.Data.GeneIdsWithTypes =
        [
            new GeneIdentifierWithType(GeneType.Fodder,
                GeneIdentifier.New("gene:acme/acme-fodder/1.0:first-fodder")),
            new GeneIdentifierWithType(GeneType.Volume,
                GeneIdentifier.New("gene:acme/acme-parent/1.0:sda")),
        ];
        sagaData.Data.GeneSetMap = new Dictionary<GeneSetIdentifier, GeneSetIdentifier>
        {
            [GeneSetIdentifier.New("acme/acme-parent/latest")] = GeneSetIdentifier.New("acme/acme-parent/1.0"),
            [GeneSetIdentifier.New("acme/acme-grand-parent/latest")] = GeneSetIdentifier.New("acme/acme-grand-parent/1.0"),
        };

        // The Rebus saga store uses a hardcoded Newtonsoft.Json serializer.
        // Hence, we test the roundtrip with the actual InMemorySagaStorage.
        var correlationProperty = Mock.Of<ISagaCorrelationProperty>(
            p => p.SagaDataType == typeof(EryphSagaData<TestData>)
                 && p.PropertyName == nameof(EryphSagaData<TestData>.SagaTaskId));
        var sagaStore = new InMemorySagaStorage();
        
        await sagaStore.Insert(sagaData, [correlationProperty]);
        var result = await sagaStore.Find(
            typeof(EryphSagaData<TestData>),
            nameof(EryphSagaData<TestData>.SagaTaskId),
            sagaTaskId);

        var resultData = result.Should().BeOfType<EryphSagaData<TestData>>().Subject;
        
        resultData.Data.Should().BeEquivalentTo(sagaData.Data);
    }

    [Fact]
    public void Inner_data_is_always_initialized()
    {
        var data = new EryphSagaData<TestData>();
        data.Data.Should().NotBeNull();
    }

    private sealed class TestData
    {
        public List<GeneIdentifier> GeneIds { get; set; }

        public List<AncestorInfo> Ancestors { get; set; }

        public List<GeneIdentifierWithType> GeneIdsWithTypes { get; set; }

        public IReadOnlyDictionary<GeneSetIdentifier, GeneSetIdentifier> GeneSetMap { get; set; }
    }
}

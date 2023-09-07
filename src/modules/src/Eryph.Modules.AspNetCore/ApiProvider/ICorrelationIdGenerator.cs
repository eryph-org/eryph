namespace Eryph.Modules.AspNetCore.ApiProvider;

public interface ICorrelationIdGenerator
{
    string Get();
    void Set(string correlationId);
}
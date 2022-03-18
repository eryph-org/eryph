namespace Eryph.Modules.Controller;

public interface IStorageManagementAgentLocator
{
    string FindAgentForDataStore(string dataStore, string environment);
}
namespace PPrePorter.Core.Interfaces
{
    public interface IConnectionStringResolverService
    {
        Task<string> ResolveConnectionStringAsync(string connectionStringTemplate);
        void ClearCaches();
    }
}
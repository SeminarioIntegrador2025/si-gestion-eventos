namespace si_td_gestion_eventos.Services.Contracts
{
    public interface IClienteBusinessRules
    {
        Task<bool> IsCedulaUniqueAsync(string cedula, int? excludeClienteId = null);
        Task<bool> CanDeactivateClienteAsync(int clienteId);
        Task<bool> HasActiveEventsAsync(int clienteId);
    }
}
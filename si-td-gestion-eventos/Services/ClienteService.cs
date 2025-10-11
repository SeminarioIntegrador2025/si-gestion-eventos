using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Repositories;

namespace si_td_gestion_eventos.Services
{
    public class ClienteService(GenericRepository<Cliente> _clienteRepository) //: IClienteService
    {
        public async Task<IEnumerable<ClienteVM>> GetAllAsync()
        {
            var clientes = await _clienteRepository.GetAllAsync();

            var clientesVM = clientes.Select(c => 
            new ClienteVM
            {
                ClienteId = c.ClienteId,
                Nombre = c.Nombre,
                Apellido = c.Apellido,
                CedulaIdentidad = c.CedulaIdentidad,
                Domicilio = c.Domicilio,
                Telefono = c.Telefono,
                Activo = c.Activo
            }).ToList();

            return clientesVM;
        }

        public async Task AddAsync(ClienteVM viewModel)
        {
            var entity = new Cliente
            {
                Nombre = viewModel.Nombre,
                Apellido = viewModel.Apellido,
                CedulaIdentidad = viewModel.CedulaIdentidad,
                Telefono = viewModel.Telefono,
                Activo = viewModel.Activo,
                Domicilio = viewModel.Domicilio
            };

            await _clienteRepository.AddAsync(entity);
        }

        public async Task GetByIdAsync(int id)
        {
            await _clienteRepository.GetByIdAsync(id);         
        }

    }
}
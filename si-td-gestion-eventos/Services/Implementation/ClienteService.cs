using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Rendering;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Repositories;
using si_td_gestion_eventos.Services.Common;
using si_td_gestion_eventos.Services.Contracts;
using si_td_gestion_eventos.Services.Implementation;

namespace si_td_gestion_eventos.Services.Implementation
{
    public class ClienteService : IClienteService
    {
        private readonly IGenericRepository<Cliente> _clienteRepository;

        private readonly IValidator<ClienteVM> _validator;
        private readonly IClienteBusinessRules _businessRules;
        private readonly IMapper _mapper;

        public ClienteService(
            IGenericRepository<Cliente> clienteRepository,
            IValidator<ClienteVM> validator,
            IClienteBusinessRules businessRules,
            IMapper mapper)
        {
            _clienteRepository = clienteRepository;
            _validator = validator;
            _businessRules = businessRules;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ClienteVM>> GetAllAsync()
        {
            var clientes = await _clienteRepository.GetAllAsync();
            return _mapper.Map<IEnumerable<ClienteVM>>(clientes);
        }

        public async Task<ClienteVM?> GetByIdAsync(int id)
        {
            var cliente = await _clienteRepository.GetByIdAsync(id);
            return cliente != null ? _mapper.Map<ClienteVM>(cliente) : null;
        }

        public async Task<ClienteVM?> GetByCedulaAsync(string cedula)
        {
            if (string.IsNullOrWhiteSpace(cedula)) return null;

            // Esto permite que si el usuario escribe "1.234.567-8" y en BD está "12345678", lo encuentre igual.
            string cedulaLimpia = cedula.Replace(".", "").Replace("-", "").Replace(" ", "").Trim();

    
            var clientes = await _clienteRepository.FindAsync(c =>
                c.CedulaIdentidad != null &&
                (c.CedulaIdentidad == cedula || c.CedulaIdentidad == cedulaLimpia)
            );

            var cliente = clientes.FirstOrDefault();

            if (cliente == null) return null;

            return _mapper.Map<ClienteVM>(cliente);
        }

        public async Task<ServiceResult<ClienteVM>> CreateAsync(ClienteVM clienteVM)
        {
            var validationResult = await _validator.ValidateAsync(clienteVM);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                return ServiceResult<ClienteVM>.FailureResult(errors);
            }

            try
            {
                var entity = _mapper.Map<Cliente>(clienteVM);
                await _clienteRepository.AddAsync(entity);

                // El servicio es responsable de confirmar la transacción
                await _clienteRepository.SaveChangesAsync();

                clienteVM.ClienteId = entity.ClienteId;
                return ServiceResult<ClienteVM>.SuccessResult(clienteVM, "Cliente creado con éxito.");
            }
            catch (Exception ex)
            {
                // Aquí deberías loguear el error (ex.Message)
                return ServiceResult<ClienteVM>.FailureResult("Ocurrió un error inesperado al crear el cliente.");
            }
        }

        public async Task<ServiceResult<ClienteVM>> UpdateAsync(ClienteVM clienteVM)
        {
            var validationResult = await _validator.ValidateAsync(clienteVM);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                return ServiceResult<ClienteVM>.FailureResult(errors);
            }

            try
            {
                var cliente = await _clienteRepository.GetByIdAsync(clienteVM.ClienteId);
                if (cliente == null)
                {
                    return ServiceResult<ClienteVM>.FailureResult("Cliente no encontrado.");
                }

                _mapper.Map(clienteVM, cliente);

                // Se notifica al repositorio del cambio y luego se guardan
                _clienteRepository.Update(cliente);
                await _clienteRepository.SaveChangesAsync();

                return ServiceResult<ClienteVM>.SuccessResult(clienteVM, "Cliente actualizado con éxito.");
            }
            catch (Exception ex)
            {
                // Loguear error
                return ServiceResult<ClienteVM>.FailureResult("Ocurrió un error inesperado al actualizar el cliente.");
            }
        }

        public async Task<ServiceResult<bool>> DeactivateAsync(int id)
        {
            try
            {
                if (!await _businessRules.CanDeactivateClienteAsync(id))
                {
                    return ServiceResult<bool>.FailureResult("No se puede dar de baja al cliente porque tiene eventos activos.");
                }

                var cliente = await _clienteRepository.GetByIdAsync(id);
                if (cliente == null)
                {
                    return ServiceResult<bool>.FailureResult("Cliente no encontrado.");
                }

                cliente.Activo = false;
                _clienteRepository.Update(cliente);
                await _clienteRepository.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true, "Cliente dado de baja correctamente.");
            }
            catch (Exception ex)
            {
                // Loguear error
                return ServiceResult<bool>.FailureResult("Ocurrió un error inesperado al dar de baja al cliente.");
            }
        }

        public async Task<ServiceResult<bool>> ActivateAsync(int id)
        {
            try
            {
                var cliente = await _clienteRepository.GetByIdAsync(id);
                if (cliente == null)
                {
                    return ServiceResult<bool>.FailureResult("Cliente no encontrado.");
                }

                cliente.Activo = true;
                _clienteRepository.Update(cliente);
                await _clienteRepository.SaveChangesAsync();

                return ServiceResult<bool>.SuccessResult(true, "Cliente activado correctamente.");
            }
            catch (Exception ex)
            {
                // Loguear error
                return ServiceResult<bool>.FailureResult("Ocurrió un error inesperado al activar el cliente.");
            }
        }

        public async Task<IEnumerable<SelectListItem>> GetClientesActivosParaDropdownAsync()
        {
            var clientesActivos = await _clienteRepository.FindAsync(c => c.Activo);
            
            if (clientesActivos == null || !clientesActivos.Any())
            {
                return new List<SelectListItem>();
            }
    
                return clientesActivos
                
                .OrderBy(c => c.Apellido ?? c.Nombre) // Si Apellido es null, ordena por Nombre
                .ThenBy(c => c.Nombre)
                
                .Select(c => new SelectListItem
                {
                    Value = c.ClienteId.ToString(),
                    Text = (c.Tipo == si_td_gestion_eventos.Models.Enums.TipoCliente.PersonaFisica)
                           // Si es Física: "Apellido, Nombre" 
                           ? $"{(c.Apellido ?? "")}, {c.Nombre}"
                           // Si es Jurídica: "Empresa: Nombre"
                           : $"Empresa: {c.Nombre}"
                });
        }
    }
}
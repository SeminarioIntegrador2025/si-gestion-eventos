using FluentValidation;
using Microsoft.AspNetCore.Mvc.Rendering;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Repositories;
using si_td_gestion_eventos.Services.Common;
using si_td_gestion_eventos.Services.Contracts;

namespace si_td_gestion_eventos.Services
{
    public class ClienteService : IClienteService
    {
        private readonly GenericRepository<Cliente> _clienteRepository;
        private readonly IValidator<ClienteVM> _validator;
        private readonly IClienteBusinessRules _businessRules;

        public ClienteService(
            GenericRepository<Cliente> clienteRepository,
            IValidator<ClienteVM> validator,
            IClienteBusinessRules businessRules)
        {
            _clienteRepository = clienteRepository;
            _validator = validator;
            _businessRules = businessRules;
        }

        public async Task<IEnumerable<ClienteVM>> GetAllAsync()
        {
            var clientes = await _clienteRepository.GetAllAsync();
            return clientes.Select(MapToViewModel);
        }

        public async Task<ClienteVM?> GetByIdAsync(int id)
        {
            var cliente = await _clienteRepository.GetByIdAsync(id);
            return cliente != null ? MapToViewModel(cliente) : null;
        }

        public async Task<ServiceResult<ClienteVM>> CreateAsync(ClienteVM clienteVM)
        {
            // Normalizar datos antes de validar
            NormalizeClienteData(clienteVM);

            // Validar usando FluentValidation
            var validationResult = await _validator.ValidateAsync(clienteVM);
            
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                return ServiceResult<ClienteVM>.FailureResult(errors);
            }

            try
            {
                // Crear entidad
                var entity = MapToEntity(clienteVM);
                await _clienteRepository.AddAsync(entity);
                
                // Actualizar el ID del ViewModel con el ID generado
                clienteVM.ClienteId = entity.ClienteId;
                
                return ServiceResult<ClienteVM>.SuccessResult(
                    clienteVM, 
                    "Cliente creado con éxito.");
            }
            catch (Exception ex)
            {
                return ServiceResult<ClienteVM>.FailureResult($"Error al crear cliente: {ex.Message}");
            }
        }

        public async Task<ServiceResult<ClienteVM>> UpdateAsync(ClienteVM clienteVM)
        {
            // Normalizar datos antes de validar
            NormalizeClienteData(clienteVM);

            // Validar usando FluentValidation
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

                // Actualizar propiedades
                UpdateEntityFromViewModel(cliente, clienteVM);
                
                await _clienteRepository.SaveChangesAsync();
                
                return ServiceResult<ClienteVM>.SuccessResult(
                    clienteVM, 
                    "Cliente actualizado con éxito.");
            }
            catch (Exception ex)
            {
                return ServiceResult<ClienteVM>.FailureResult($"Error al actualizar cliente: {ex.Message}");
            }
        }

        public async Task<ServiceResult<bool>> DeactivateAsync(int id)
        {
            try
            {
                // Verificar reglas de negocio antes de desactivar
                if (!await _businessRules.CanDeactivateClienteAsync(id))
                {
                    return ServiceResult<bool>.FailureResult(
                        "No se puede dar de baja al cliente porque tiene eventos activos.");
                }

                var cliente = await _clienteRepository.GetByIdAsync(id);
                if (cliente == null)
                {
                    return ServiceResult<bool>.FailureResult("Cliente no encontrado.");
                }

                cliente.Activo = false;
                await _clienteRepository.SaveChangesAsync();
                
                return ServiceResult<bool>.SuccessResult(true, "Cliente dado de baja correctamente.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult($"Error al dar de baja cliente: {ex.Message}");
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
                await _clienteRepository.SaveChangesAsync();
                
                return ServiceResult<bool>.SuccessResult(true, "Cliente activado correctamente.");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.FailureResult($"Error al activar cliente: {ex.Message}");
            }
        }

        public IEnumerable<SelectListItem> GetClientesActivosParaDropdown()
        {
            var clientes = _clienteRepository.GetAllAsync().Result.Where(c => c.Activo);
            return clientes.Select(c => new SelectListItem
            {
                Value = c.ClienteId.ToString(),
                Text = $"{c.Nombre} {c.Apellido}"
            });
        }

        #region Métodos Auxiliares y Mappers

        private ClienteVM MapToViewModel(Cliente cliente)
        {
            return new ClienteVM
            {
                ClienteId = cliente.ClienteId,
                Nombre = cliente.Nombre,
                Apellido = cliente.Apellido,
                CedulaIdentidad = cliente.CedulaIdentidad,
                Domicilio = cliente.Domicilio,
                Telefono = cliente.Telefono,
                Activo = cliente.Activo
            };
        }

        private Cliente MapToEntity(ClienteVM clienteVM)
        {
            return new Cliente
            {
                Nombre = clienteVM.Nombre,
                Apellido = clienteVM.Apellido,
                CedulaIdentidad = clienteVM.CedulaIdentidad,
                Domicilio = clienteVM.Domicilio,
                Telefono = clienteVM.Telefono,
                Activo = clienteVM.Activo
            };
        }

        private void UpdateEntityFromViewModel(Cliente cliente, ClienteVM clienteVM)
        {
            cliente.Nombre = clienteVM.Nombre;
            cliente.Apellido = clienteVM.Apellido;
            cliente.CedulaIdentidad = clienteVM.CedulaIdentidad;
            cliente.Domicilio = clienteVM.Domicilio;
            cliente.Telefono = clienteVM.Telefono;
            cliente.Activo = clienteVM.Activo;
        }

        private void NormalizeClienteData(ClienteVM clienteVM)
        {
            clienteVM.Nombre = clienteVM.Nombre?.Trim().ToTitleCase() ?? string.Empty;
            clienteVM.Apellido = clienteVM.Apellido?.Trim().ToTitleCase() ?? string.Empty;
            clienteVM.CedulaIdentidad = clienteVM.CedulaIdentidad?.Trim() ?? string.Empty;
            clienteVM.Domicilio = clienteVM.Domicilio?.Trim() ?? string.Empty;
            clienteVM.Telefono = clienteVM.Telefono?.Trim() ?? string.Empty;
        }

        #endregion

        #region Métodos Legacy (para compatibilidad con el controlador actual)

        /// <summary>
        /// Método legacy para compatibilidad. Use CreateAsync en su lugar.
        /// </summary>
        [Obsolete("Use CreateAsync method instead")]
        public async Task AddAsync(ClienteVM viewModel)
        {
            var result = await CreateAsync(viewModel);
            if (!result.Success)
            {
                throw new InvalidOperationException(string.Join(", ", result.Errors));
            }
        }

        /// <summary>
        /// Método legacy para compatibilidad. Use UpdateAsync en su lugar.
        /// </summary>
        [Obsolete("Use UpdateAsync method instead")]
        public async Task EditAsync(ClienteVM viewModel)
        {
            var result = await UpdateAsync(viewModel);
            if (!result.Success)
            {
                throw new InvalidOperationException(string.Join(", ", result.Errors));
            }
        }

        #endregion
    }

    public static class StringExtensions
    {
        public static string ToTitleCase(this string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower());
        }
    }
}
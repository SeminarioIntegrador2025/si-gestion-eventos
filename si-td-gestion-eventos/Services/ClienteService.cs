using AutoMapper;
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
        private readonly IMapper _mapper;

        public ClienteService(
            GenericRepository<Cliente> clienteRepository,
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

        public async Task<ServiceResult<ClienteVM>> CreateAsync(ClienteVM clienteVM)
        {
            // Validar usando FluentValidation
            var validationResult = await _validator.ValidateAsync(clienteVM);
            
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                return ServiceResult<ClienteVM>.FailureResult(errors);
            }

            try
            {
                // Crear entidad usando AutoMapper
                var entity = _mapper.Map<Cliente>(clienteVM);
                await _clienteRepository.AddAsync(entity);
                
                // Actualizar el ID del ViewModel
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

                // Actualizar usando AutoMapper
                _mapper.Map(clienteVM, cliente);
                
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
                // Verificar reglas de negocio
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
    }
}
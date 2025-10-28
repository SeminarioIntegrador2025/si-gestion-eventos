using FluentValidation;
using si_td_gestion_eventos.Models.Enums; // <-- IMPORTANTE
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;
using System.Text.RegularExpressions;

namespace si_td_gestion_eventos.Validators
{
    public class ClienteValidator : AbstractValidator<ClienteVM>
    {
        private readonly IClienteBusinessRules _businessRules;

        public ClienteValidator(IClienteBusinessRules businessRules)
        {
            _businessRules = businessRules;

            RuleFor(x => x.Tipo).NotEmpty().WithMessage("Debe seleccionar un tipo de cliente.");

            // --- REGLAS COMUNES ---
            RuleFor(x => x.Telefono)
                .NotEmpty().WithMessage("El teléfono es obligatorio.")
                .Must(BeValidPhoneNumber).WithMessage("El teléfono debe contener solo números, espacios, guiones o paréntesis.")
                .Must(HaveMinimumDigits).WithMessage("El teléfono debe tener al menos 8 dígitos.");

            RuleFor(x => x.Domicilio)
                .NotEmpty().WithMessage("El domicilio es obligatorio.")
                .Length(5, 120).WithMessage("El domicilio debe tener entre 5 y 120 caracteres.");

            // --- REGLAS  PARA PERSONA FÍSICA ---
            When(x => x.Tipo == TipoCliente.PersonaFisica, () =>
            {
                RuleFor(x => x.Nombre)
                    .NotEmpty().WithMessage("El nombre es obligatorio.")
                    .Length(2, 60).WithMessage("El nombre debe tener entre 2 y 60 caracteres.")
                    .Must(BeOnlyLettersAndSpaces).WithMessage("El nombre solo puede contener letras y espacios.")
                    .Must(NotContainNumbers).WithMessage("El nombre no puede contener números.");

                RuleFor(x => x.Apellido)
                    .NotEmpty().WithMessage("El apellido es obligatorio.");

                RuleFor(x => x.CedulaIdentidad)
                    .NotEmpty().WithMessage("La cédula de identidad es obligatoria.")
                    .Must(BeValidCedulaFormat).WithMessage("La cédula debe tener formato válido (ej: 1.234.567-8).")
                    .MustAsync(BeUniqueCedulaAsync).WithMessage("Ya existe un cliente con esta cédula de identidad.");                    

                RuleFor(x => x.RUT).Empty().WithMessage("El RUT debe estar vacío para una persona física.");
            });


            // --- REGLAS  PARA PERSONA JURÍDICA ---

            When(x => x.Tipo == TipoCliente.PersonaJuridica, () =>
            {
                RuleFor(x => x.Nombre) 
                    .NotEmpty().WithMessage("La Razón Social es obligatoria.")
                    .Length(2, 100).WithMessage("La Razón Social debe tener entre 2 y 100 caracteres.");

                RuleFor(x => x.RUT)
                    .NotEmpty().WithMessage("El RUT es obligatorio.")
                    .Must(BeValidRUTFormat).WithMessage("El RUT debe tener un formato válido (12 dígitos).")
                    .MustAsync(BeUniqueRUTAsync).WithMessage("Ya existe un cliente con este RUT.");

                
                RuleFor(x => x.CedulaIdentidad).Empty().WithMessage("La Cédula debe estar vacía para una persona jurídica.");
            });
        }

        // --- helpers ---
        #region Métodos Helper
        private bool BeOnlyLettersAndSpaces(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return Regex.IsMatch(value, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s]+$");
        }

        private bool NotContainNumbers(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return !Regex.IsMatch(value, @"\d");
        }

        private bool BeValidCedulaFormat(string? cedula)
        {
            if (string.IsNullOrWhiteSpace(cedula)) return false;
            var cleanCedula = Regex.Replace(cedula, @"[\.\-]", "");
            return Regex.IsMatch(cleanCedula, @"^\d{7,8}$");
        }

        private bool BeValidPhoneNumber(string telefono)
        {
            if (string.IsNullOrWhiteSpace(telefono)) return false;
            return Regex.IsMatch(telefono, @"^[\d\s\-\(\)\+]+$");
        }

        private bool HaveMinimumDigits(string telefono)
        {
            if (string.IsNullOrWhiteSpace(telefono)) return false;
            var digitsOnly = Regex.Replace(telefono, @"[^\d]", "");
            return digitsOnly.Length >= 8;
        }
        #endregion

        // VALIDAR RUT 
        private bool BeValidRUTFormat(string? rut)
        {
            if (string.IsNullOrWhiteSpace(rut)) return false;
            var cleanRut = Regex.Replace(rut, @"[\.\-]", "");
            return cleanRut.Length == 12 && cleanRut.All(char.IsDigit);
        }

        // Ayncs
        private async Task<bool> BeUniqueCedulaAsync(ClienteVM cliente, string? cedula, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(cedula)) return true; // No lo valida si está vacío (el 'NotEmpty' se encarga)
            return await _businessRules.IsCedulaUniqueAsync(cedula, cliente.ClienteId == 0 ? null : cliente.ClienteId);
        }

        // Aync -> RUT
        private async Task<bool> BeUniqueRUTAsync(ClienteVM cliente, string? rut, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(rut)) return true;
            // Necesitarás agregar 'IsRUTUniqueAsync' a tu IClienteBusinessRules
            // return await _businessRules.IsRUTUniqueAsync(rut, cliente.ClienteId == 0 ? null : cliente.ClienteId);

            // Por ahora, lo dejamos pasar (simulación):
            return await Task.FromResult(true); // REEMPLAZAR LUEGO
        }
    }
}
using FluentValidation;
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

            RuleFor(x => x.Nombre)
                .NotEmpty().WithMessage("El nombre es obligatorio.")
                .Length(2, 60).WithMessage("El nombre debe tener entre 2 y 60 caracteres.")
                .Must(BeOnlyLettersAndSpaces).WithMessage("El nombre solo puede contener letras y espacios.")
                .Must(NotContainNumbers).WithMessage("El nombre no puede contener números.");

            RuleFor(x => x.Apellido)
                .NotEmpty().WithMessage("El apellido es obligatorio.")
                .Length(2, 60).WithMessage("El apellido debe tener entre 2 y 60 caracteres.")
                .Must(BeOnlyLettersAndSpaces).WithMessage("El apellido solo puede contener letras y espacios.")
                .Must(NotContainNumbers).WithMessage("El apellido no puede contener números.");

            RuleFor(x => x.CedulaIdentidad)
                .NotEmpty().WithMessage("La cédula de identidad es obligatoria.")
                .Must(BeValidCedulaFormat).WithMessage("La cédula debe tener formato válido (ej: 1.234.567-8).")
                .MustAsync(BeUniqueCedula).WithMessage("Ya existe un cliente con esta cédula de identidad.");

            RuleFor(x => x.Telefono)
                .NotEmpty().WithMessage("El teléfono es obligatorio.")
                .Must(BeValidPhoneNumber).WithMessage("El teléfono debe contener solo números, espacios, guiones o paréntesis.")
                .Must(HaveMinimumDigits).WithMessage("El teléfono debe tener al menos 8 dígitos.");

            RuleFor(x => x.Domicilio)
                .NotEmpty().WithMessage("El domicilio es obligatorio.")
                .Length(5, 120).WithMessage("El domicilio debe tener entre 5 y 120 caracteres.");
        }

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

        private bool BeValidCedulaFormat(string cedula)
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

        private async Task<bool> BeUniqueCedula(ClienteVM cliente, string cedula, CancellationToken cancellationToken)
        {
            return await _businessRules.IsCedulaUniqueAsync(cedula, cliente.ClienteId == 0 ? null : cliente.ClienteId);
        }
    }
}

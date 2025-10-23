using System;
using System.Linq;
using System.Text.RegularExpressions;
using FluentValidation;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;

namespace si_td_gestion_eventos.Validators
{
    public class EventoValidator : AbstractValidator<EventoVM>
    {
        private readonly IEventoBusinessRules _businessRules;

        public EventoValidator(IEventoBusinessRules businessRules)
        {
            _businessRules = businessRules;

            // ========== REGLAS COMUNES (aplican a Create y Edit) ==========

            // --- Cliente ---
            RuleFor(e => e.ClienteId)
                .GreaterThan(0).WithMessage("Debe seleccionar un cliente.")
                .MustAsync(async (clienteId, ct) => await _businessRules.IsClienteActiveAsync(clienteId))
                .WithMessage("El cliente seleccionado no está activo.");

            // --- Detalles del Evento ---
            RuleFor(e => e.Tipo)
                .NotEmpty().WithMessage("Debe seleccionar un tipo de evento.");

            RuleFor(e => e.CantidadPersonas)
                .GreaterThan(0).WithMessage("La cantidad de personas debe ser mayor a cero.")
                .LessThanOrEqualTo(500).WithMessage("La cantidad de personas no puede exceder 500.")
                .Must(c => c % 1 == 0).WithMessage("La cantidad de personas debe ser un número entero.");

            // --- Fechas y Horas (presencia + coherencia básica) ---
            RuleFor(x => x.FechaContrato)
                .NotEmpty().WithMessage("La fecha del contrato es obligatoria.");

            RuleFor(x => x.Inicio)
                .NotEmpty().WithMessage("La fecha de inicio es obligatoria.");

            RuleFor(x => x.Fin)
                .NotEmpty().WithMessage("La fecha de fin es obligatoria.")
                .GreaterThanOrEqualTo(x => x.Inicio)
                .WithMessage("La fecha de fin no puede ser anterior a la fecha de inicio.");

            RuleFor(x => x.HoraInicio)
                .NotEmpty().WithMessage("La hora de inicio es obligatoria.");

            RuleFor(x => x.HoraFin)
                .NotEmpty().WithMessage("La hora de fin es obligatoria.")
                .GreaterThan(x => x.HoraInicio)
                .When(x => x.Fin.Date == x.Inicio.Date)
                .WithMessage("Si es el mismo día, la hora de fin debe ser posterior a la hora de inicio.");

            // --- Validaciones de rango y disponibilidad (negocio/BD) ---
            RuleFor(x => x)
                .MustAsync(async (evento, ct) =>
                    await _businessRules.IsValidDateRangeAsync(
                        evento.Inicio, evento.Fin, evento.HoraInicio, evento.HoraFin))
                .WithMessage("El rango de fechas y horarios no es válido.")
                .MustAsync(async (evento, ct) =>
                    await _businessRules.IsDateRangeAvailableAsync(
                        evento.Inicio, evento.Fin, evento.HoraInicio, evento.HoraFin,
                        evento.EventoId == 0 ? null : evento.EventoId))
                .WithMessage("Ya existe otro evento programado en este horario. Por favor, seleccione otra fecha u horario.");

            // --- Costos y Montos ---
            RuleFor(x => x.CostoAlquiler)
                .GreaterThan(0).WithMessage("El costo del alquiler debe ser mayor a cero.");

            RuleFor(x => x.MontoReserva)
                .GreaterThanOrEqualTo(0).WithMessage("El monto de reserva no puede ser negativo.")
                .LessThanOrEqualTo(x => x.CostoAlquiler)
                .WithMessage("El monto de reserva no puede ser mayor al costo del alquiler.");

            RuleFor(x => x.MontoAireAcondicionado)
                .GreaterThanOrEqualTo(0).WithMessage("El monto del aire acondicionado no puede ser negativo.")
                .When(x => x.MontoAireAcondicionado.HasValue);

            // --- Responsable: NOMBRE ---
            RuleFor(x => x.ResponsableNombre)
                .NotEmpty().WithMessage("El nombre del responsable es obligatorio.")
                .Length(2, 100).WithMessage("El nombre del responsable debe tener entre 2 y 100 caracteres.")
                .Must(BeOnlyLettersAndSpaces).WithMessage("El nombre del responsable solo puede contener letras y espacios.")
                .Must(NotContainConsecutiveSpaces).WithMessage("El nombre no puede contener espacios consecutivos.")
                .Must(NotStartOrEndWithSpace).WithMessage("El nombre no puede comenzar o terminar con espacios.");

            // --- Responsable: TELÉFONO ---
            RuleFor(x => x.ResponsableTelefono)
                .NotEmpty().WithMessage("El teléfono del responsable es obligatorio.")
                .Length(8, 30).WithMessage("El teléfono debe tener entre 8 y 30 caracteres.")
                .Must(BeValidPhoneNumber).WithMessage("El teléfono debe contener solo números, espacios, guiones, paréntesis o '+'.")
                .Must(HaveMinimumDigits).WithMessage("El teléfono debe contener al menos 8 dígitos.");

            // --- Responsable: CÉDULA ---
            RuleFor(x => x.ResponsableCedula)
                .NotEmpty().WithMessage("La cédula del responsable es obligatoria.")
                .Length(7, 30).WithMessage("La cédula debe tener entre 7 y 30 caracteres.")
                .Must(BeValidCedulaFormat).WithMessage("La cédula debe tener formato válido (ej: 1.234.567-8 o 12345678).");

            // ========== RULESET: CREATE (validaciones específicas de creación) ==========
            RuleSet("Create", () =>
            {
                // Fecha del contrato no futura
                RuleFor(x => x.FechaContrato)
                    .LessThanOrEqualTo(DateTime.Today)
                    .WithMessage("La fecha del contrato no puede ser futura.");

                // Inicio debe ser futuro (regla de negocio)
                RuleFor(x => x.Inicio)
                    .MustAsync(async (inicio, ct) => await _businessRules.IsEventoInFutureAsync(inicio))
                    .WithMessage("La fecha de inicio debe ser futura.");

                // Si es hoy, hora de inicio no puede ser anterior a la hora actual
                RuleFor(x => x.HoraInicio)
                    .GreaterThanOrEqualTo(_ => DateTime.Now.TimeOfDay)
                    .When(x => x.Inicio.Date == DateTime.Today)
                    .WithMessage("Si el evento es hoy, la hora de inicio no puede ser anterior a la hora actual.");

                // Monto Reserva validado contra reglas de negocio (porcentaje/mínimos, etc.)
                RuleFor(x => x)
                    .MustAsync(async (evento, ct) =>
                        await _businessRules.IsReservationAmountValidAsync(evento.MontoReserva, evento.CostoAlquiler))
                    .WithMessage("El monto de reserva no es válido en relación al costo del alquiler.");
            });

            // ========== RULESET: EDIT (validaciones específicas de edición) ==========
            RuleSet("Edit", () =>
            {
                // En edición NO forzamos que Inicio sea futuro (podrías editar otros campos),
                // pero mantenemos coherencias ya cubiertas en las reglas comunes (rango, disponibilidad, etc.)
            });
        }

        // ===== Helpers para validación del Responsable =====
        
        private bool BeOnlyLettersAndSpaces(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            // Permite letras (incluidas tildes), espacios, apóstrofes y guiones (para nombres compuestos)
            return Regex.IsMatch(value, @"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s'\-]+$");
        }

        private bool NotContainConsecutiveSpaces(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            return !value.Contains("  ");
        }

        private bool NotStartOrEndWithSpace(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            return value == value.Trim();
        }

        private bool BeValidPhoneNumber(string telefono)
        {
            if (string.IsNullOrWhiteSpace(telefono)) return false;
            // Permite: números, espacios, guiones, paréntesis, signo más
            return Regex.IsMatch(telefono, @"^[\d\s\-\(\)\+]+$");
        }

        private bool HaveMinimumDigits(string telefono)
        {
            if (string.IsNullOrWhiteSpace(telefono)) return false;
            // Extraer solo los dígitos y verificar que sean al menos 8
            var digitsOnly = Regex.Replace(telefono, @"[^\d]", "");
            return digitsOnly.Length >= 8;
        }

        private bool BeValidCedulaFormat(string cedula)
        {
            if (string.IsNullOrWhiteSpace(cedula)) return false;
            
            // Limpiar puntos y guiones
            var cleanCedula = cedula.Replace(".", "").Replace("-", "").Trim();
            
            // Validar que solo contenga dígitos después de limpiar
            if (!Regex.IsMatch(cleanCedula, @"^\d+$")) return false;
            
            // Validar longitud (7 u 8 dígitos)
            return cleanCedula.Length >= 7 && cleanCedula.Length <= 8;
        }
    }
}

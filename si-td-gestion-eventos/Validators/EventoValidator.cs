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


            // ==========================================================
            // REGLAS COMUNES (Aplican siempre: Create y Edit)
            // ==========================================================

            // --- Cliente ---
            RuleFor(e => e.ClienteId)
                .GreaterThan(0).WithMessage("Debe seleccionar un cliente.")
                .MustAsync(async (clienteId, ct) => await _businessRules.IsClienteActiveAsync(clienteId))
                .WithMessage("El cliente seleccionado no está activo.");

            // --- Detalles del Evento ---
            RuleFor(e => e.Tipo)
                .NotNull().WithMessage("Debe seleccionar un tipo de evento válido.")
                .IsInEnum().WithMessage("Debe seleccionar un tipo de evento válido."); // IsInEnum es más robusto que NotEmpty para enums

            RuleFor(e => e.CantidadPersonas)
                .GreaterThanOrEqualTo(50).WithMessage("Se necesitan al menos 50 personas para el evento.")
                .LessThanOrEqualTo(400).WithMessage("El salón no permite más de 400 personas.")
                .Must(c => c % 1 == 0).WithMessage("La cantidad de personas debe ser un número entero.");

            // --- Fechas y Horas (Presencia y Coherencia Básica) ---
            RuleFor(x => x.FechaContrato)
                .NotEmpty().WithMessage("La fecha del contrato es obligatoria.");
            // La regla LessThanOrEqualTo(Today) se movió al RuleSet "Create" porque al editar podrías ver un contrato viejo.

            RuleFor(x => x.Inicio)
                .NotEmpty().WithMessage("La fecha de inicio es obligatoria.");
            // La regla de que sea futura se movió al RuleSet "Create".

            RuleFor(x => x.Fin)
                .NotEmpty().WithMessage("La fecha de fin es obligatoria.")
                .GreaterThanOrEqualTo(x => x.Inicio) // Fin >= Inicio siempre debe cumplirse
                .WithMessage("La fecha de fin no puede ser anterior a la fecha de inicio.");

            RuleFor(x => x.HoraInicio)
                .NotEmpty().WithMessage("La hora de inicio es obligatoria.");

            RuleFor(x => x.HoraFin)
                .NotEmpty().WithMessage("La hora de fin es obligatoria.")
                // Si el evento dura solo un día, la hora de fin debe ser posterior a la de inicio
                .GreaterThan(x => x.HoraInicio)
                .When(x => x.Fin.Date == x.Inicio.Date, ApplyConditionTo.CurrentValidator) // ApplyConditionTo es más explícito
                .WithMessage("Si es el mismo día, la hora de fin debe ser posterior a la hora de inicio.");

            // --- Validaciones de Rango y Disponibilidad (Reglas de Negocio / BD) ---
            // Valida que el rango Inicio/Fin/HoraInicio/HoraFin sea lógicamente posible y esté disponible
            RuleFor(x => x) // Valida el objeto completo
                .Cascade(CascadeMode.Stop) // Si falla la primera, no sigue con la segunda
                .MustAsync(async (evento, ct) => 
                {
                    // Verificar que las horas no sean nulas antes de llamar a la regla de negocio
                    if (!evento.HoraInicio.HasValue || !evento.HoraFin.HasValue)
                        return true; // Si son nulas, otras reglas ya manejarán el error
                    
                    return await _businessRules.IsValidDateRangeAsync(
                        evento.Inicio, 
                        evento.Fin, 
                        evento.HoraInicio.Value, 
                        evento.HoraFin.Value);
                })
                .WithMessage("El rango de fechas y horarios no es válido (ej: duración negativa o excesiva).")
                .MustAsync(async (evento, ct) => 
                {
                    // Verificar que las horas no sean nulas antes de llamar a la regla de negocio
                    if (!evento.HoraInicio.HasValue || !evento.HoraFin.HasValue)
                        return true; // Si son nulas, otras reglas ya manejarán el error
                    
                    return await _businessRules.IsDateRangeAvailableAsync(
                        evento.Inicio, 
                        evento.Fin, 
                        evento.HoraInicio.Value, 
                        evento.HoraFin.Value, 
                        evento.EventoId == 0 ? null : evento.EventoId);
                })
                .WithMessage("Ya existe otro evento programado en este horario. Por favor, seleccione otra fecha u horario.");

            // --- Costos y Montos ---
            RuleFor(x => x.CostoAlquiler)
                .GreaterThan(0).WithMessage("El costo del alquiler debe ser mayor a cero.");

            RuleFor(x => x.MontoReserva)
                .GreaterThanOrEqualTo(0).WithMessage("El monto de reserva no puede ser negativo.")
                .LessThanOrEqualTo(x => x.CostoAlquiler)
                .WithMessage("El monto de reserva no puede ser mayor al costo del alquiler.");

            RuleFor(x => x.MontoAireAcondicionado)
                .GreaterThanOrEqualTo(0).WithMessage("El monto no puede ser negativo.")
                .When(x => x.MontoAireAcondicionado.HasValue);

            // --- Responsable del Salón (Validaciones detalladas) ---
            RuleFor(x => x.ResponsableNombre)
                .NotEmpty().WithMessage("El nombre del responsable es obligatorio.")
                .Length(2, 100).WithMessage("El nombre del responsable debe tener entre 2 y 100 caracteres.")
                .Must(BeOnlyLettersAndSpaces).WithMessage("El nombre del responsable solo puede contener letras y espacios.")
                .Must(NotContainConsecutiveSpaces).WithMessage("El nombre no puede contener espacios consecutivos.")
                .Must(NotStartOrEndWithSpace).WithMessage("El nombre no puede comenzar o terminar con espacios.");

            RuleFor(x => x.ResponsableTelefono)
                .NotEmpty().WithMessage("El teléfono del responsable es obligatorio.")
                .Length(8, 30).WithMessage("El teléfono debe tener entre 8 y 30 caracteres.")
                .Must(BeValidPhoneNumber).WithMessage("El teléfono debe contener solo números y caracteres válidos (+, -, (, ), espacio).")
                .Must(HaveMinimumDigits).WithMessage("El teléfono debe contener al menos 8 dígitos.");

            RuleFor(x => x.ResponsableCedula)
                .NotEmpty().WithMessage("La cédula del responsable es obligatoria.")
                // .Length(7, 30).WithMessage("La cédula debe tener entre 7 y 30 caracteres.") // El formato ya valida la longitud implícitamente
                .Must(BeValidCedulaFormat).WithMessage("La cédula debe tener formato válido (ej: 1.234.567-8 o solo números 7-8 dígitos).");


            // ==========================================================
            // RULESET: CREATE (Validaciones *adicionales* solo al crear)
            // ==========================================================
            RuleSet("Create", () =>
            {

                // Fecha del contrato no puede ser futura al crear
                RuleFor(x => x.FechaContrato)
                    .LessThanOrEqualTo(DateTime.Today)
                    .WithMessage("La fecha del contrato no puede ser futura.");

                // Fecha de inicio debe ser futura al crear (puede ser hoy)
                RuleFor(x => x.Inicio)
                    .GreaterThanOrEqualTo(DateTime.Today) // Permite crear eventos para hoy
                    .WithMessage("La fecha de inicio debe ser hoy o una fecha futura.");


                // Si es hoy, la hora de inicio no puede ser pasada
                RuleFor(x => x.HoraInicio)
                    .GreaterThanOrEqualTo(DateTime.Now.TimeOfDay)
                    .When(x => x.Inicio.Date == DateTime.Today, ApplyConditionTo.CurrentValidator)
                    .WithMessage("Si el evento es hoy, la hora de inicio no puede ser anterior a la hora actual.");

                // Validación específica del Monto de Reserva al crear y sin ser el evento en las 48hs proximas
                RuleFor(x => x)
                    .MustAsync(async (evento, ct) => await _businessRules.IsReservationAmountValidAsync(evento.MontoReserva, evento.CostoAlquiler))
                    .WithMessage("El monto de reserva no cumple con el mínimo requerido para el costo del alquiler.");

            });

            // ==========================================================
            // RULESET: EDIT (Validaciones *adicionales* solo al editar)
            // ==========================================================
            RuleSet("Edit", () =>
            {
                // Al editar, NO forzamos que Inicio sea futuro (podrías estar editando otros campos de un evento pasado).
                // Las reglas comunes ya cubren la coherencia de fechas y disponibilidad si se cambian las fechas/horas.
                // Podrías agregar reglas específicas aquí si fueran necesarias, por ejemplo:
                // RuleFor(x => x.AlgunCampoEditable).NotEmpty()...
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
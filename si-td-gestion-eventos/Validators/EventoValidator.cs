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

            // ========== REGLAS COMUNES (se aplican siempre) ==========
            
            // --- Cliente ---
            RuleFor(e => e.ClienteId)
                .GreaterThan(0).WithMessage("Debe seleccionar un cliente.")
                .MustAsync(async (clienteId, cancellation) => await _businessRules.IsClienteActiveAsync(clienteId))
                .WithMessage("El cliente seleccionado no está activo.");

            // --- Detalles del Evento ---
            RuleFor(e => e.Tipo)
                .NotEmpty().WithMessage("Debe seleccionar un tipo de evento.");

            RuleFor(e => e.CantidadPersonas)
                .GreaterThan(0).WithMessage("La cantidad de personas debe ser mayor a cero.")
                .LessThanOrEqualTo(500).WithMessage("La cantidad de personas no puede exceder 500.");

            // --- Fechas y Horas ---
            RuleFor(x => x.FechaContrato)
                .NotEmpty().WithMessage("La fecha del contrato es obligatoria.");

            RuleFor(x => x.Inicio)
                .NotEmpty().WithMessage("La fecha de inicio es obligatoria.");

            RuleFor(x => x.HoraInicio)
                .NotEmpty().WithMessage("La hora de inicio es obligatoria.");

            RuleFor(x => x.HoraFin)
                .NotEmpty().WithMessage("La hora de fin es obligatoria.")
                .GreaterThan(x => x.HoraInicio)
                .When(x => x.Fin.Date == x.Inicio.Date)
                .WithMessage("Como el evento es el mismo día, la hora de fin debe ser posterior a la de inicio.");

            // Validación de rango de fechas
            RuleFor(x => x)
                .MustAsync(async (evento, cancellation) => 
                    await _businessRules.IsValidDateRangeAsync(evento.Inicio, evento.Fin, evento.HoraInicio, evento.HoraFin))
                .WithMessage("El rango de fechas y horarios no es válido.")
                .MustAsync(async (evento, cancellation) => 
                    await _businessRules.IsDateRangeAvailableAsync(evento.Inicio, evento.Fin, evento.HoraInicio, evento.HoraFin, evento.EventoId == 0 ? null : evento.EventoId))
                .WithMessage("Ya existe otro evento programado en este horario. Por favor, seleccione otra fecha u horario.");

            // --- Aire Acondicionado ---
            RuleFor(x => x.MontoAireAcondicionado)
                .GreaterThanOrEqualTo(0).WithMessage("El monto del aire acondicionado no puede ser negativo.")
                .When(x => x.MontoAireAcondicionado.HasValue);

            // --- Responsable ---
            RuleFor(x => x.ResponsableNombre)
                .NotEmpty().WithMessage("El nombre del responsable es obligatorio.")
                .Length(2, 100).WithMessage("El nombre del responsable debe tener entre 2 y 100 caracteres.")
                .Must(BeOnlyLettersAndSpaces).WithMessage("El nombre del responsable solo puede contener letras y espacios.");

            RuleFor(x => x.ResponsableTelefono)
                .NotEmpty().WithMessage("El teléfono del responsable es obligatorio.")
                .Length(8, 30).WithMessage("El teléfono debe tener entre 8 y 30 caracteres.")
                .Must(BeValidPhoneNumber).WithMessage("El teléfono debe contener solo números, espacios, guiones o paréntesis.");

            RuleFor(x => x.ResponsableCedula)
                .NotEmpty().WithMessage("La cédula del responsable es obligatoria.")
                .Length(7, 30).WithMessage("La cédula debe tener entre 7 y 30 caracteres.")
                .Must(BeValidCedulaFormat).WithMessage("La cédula debe tener formato válido (ej: 1.234.567-8).");

            // ========== RULESET: CREATE (validaciones específicas de creación) ==========
            RuleSet("Create", () =>
            {
                RuleFor(x => x.FechaContrato)
                    .LessThanOrEqualTo(DateTime.Today)
                    .WithMessage("La fecha del contrato no puede ser futura.");

                RuleFor(x => x.Inicio)
                    .MustAsync(async (inicio, cancellation) => await _businessRules.IsEventoInFutureAsync(inicio))
                    .WithMessage("La fecha de inicio debe ser futura.");

                RuleFor(x => x.HoraInicio)
                    .GreaterThanOrEqualTo(_ => DateTime.Now.TimeOfDay)
                    .When(x => x.Inicio.Date == DateTime.Today)
                    .WithMessage("Si el evento es hoy, la hora de inicio no puede ser anterior a la hora actual.");

                RuleFor(x => x.CostoAlquiler)
                    .GreaterThan(0).WithMessage("El costo del alquiler debe ser mayor a cero.");

                RuleFor(x => x.MontoReserva)
                    .GreaterThanOrEqualTo(0).WithMessage("El monto de reserva no puede ser negativo.")
                    .LessThanOrEqualTo(x => x.CostoAlquiler).WithMessage("El monto de reserva no puede ser mayor al costo del alquiler.")
                    .MustAsync(async (evento, montoReserva, cancellation) => 
                        await _businessRules.IsReservationAmountValidAsync(montoReserva, evento.CostoAlquiler))
                    .WithMessage("El monto de reserva no es válido en relación al costo del alquiler.");
            });

            // ========== RULESET: EDIT (sin validaciones de Create) ==========
 
        }

        private bool BeOnlyLettersAndSpaces(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value.All(c => char.IsLetter(c) || char.IsWhiteSpace(c));
        }

        private bool BeValidPhoneNumber(string telefono)
        {
            if (string.IsNullOrEmpty(telefono)) return false;
            return telefono.All(c => char.IsDigit(c) || c == ' ' || c == '-' || c == '(' || c == ')' || c == '+');
        }

        private bool BeValidCedulaFormat(string cedula)
        {
            if (string.IsNullOrEmpty(cedula)) return false;
            var cleanCedula = cedula.Replace(".", "").Replace("-", "");
            return cleanCedula.Length >= 7 && cleanCedula.Length <= 8 && cleanCedula.All(char.IsDigit);
        }
    }
}
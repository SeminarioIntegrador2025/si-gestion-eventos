using FluentValidation;
using si_td_gestion_eventos.Models.ViewModels;

namespace si_td_gestion_eventos.Validators
{
    public class EventoValidator : AbstractValidator<EventoVM>
    {
        public EventoValidator()
        {
            RuleFor(e => e.ClienteId).GreaterThan(0).WithMessage("Debe seleccionar un cliente.");
            RuleFor(e => e.Fin).GreaterThanOrEqualTo(e => e.Inicio)
                .WithMessage("La fecha de fin no puede ser anterior a la fecha de inicio.");
            RuleFor(e => e.CantidadPersonas).GreaterThan(0).WithMessage("La cantidad de personas debe ser mayor a cero.");
            RuleFor(e => e.CostoAlquiler).GreaterThan(0).WithMessage("El costo del alquiler debe ser mayor a cero.");

            RuleFor(x => x.FechaContrato)
                .NotEmpty().WithMessage("La fecha del contrato es obligatoria.")
                .LessThanOrEqualTo(DateTime.Today).WithMessage("La fecha del contrato no puede ser futura.");

            RuleFor(x => x.Inicio)
                .NotEmpty().WithMessage("La fecha de inicio es obligatoria.")
                .GreaterThanOrEqualTo(DateTime.Today).WithMessage("La fecha de inicio no puede ser anterior a hoy.");

            RuleFor(x => x.Fin)
                .NotEmpty().WithMessage("La fecha de fin es obligatoria.")
                .GreaterThanOrEqualTo(x => x.Inicio).WithMessage("La fecha de fin debe ser posterior o igual a la fecha de inicio.");

            RuleFor(x => x.HoraInicio)
                .NotEmpty().WithMessage("La hora de inicio es obligatoria.");

            RuleFor(x => x.HoraFin)
                .NotEmpty().WithMessage("La hora de fin es obligatoria.")
                .GreaterThan(x => x.HoraInicio).WithMessage("La hora de fin debe ser posterior a la hora de inicio.");

            RuleFor(x => x.Tipo)
                .NotEmpty().WithMessage("Debe seleccionar un tipo de evento.");

            RuleFor(x => x.CostoAlquiler)
                .GreaterThan(0).WithMessage("El costo del alquiler debe ser mayor a cero.");

            RuleFor(x => x.MontoReserva)
                .GreaterThanOrEqualTo(0).WithMessage("El monto de reserva no puede ser negativo.")
                .LessThanOrEqualTo(x => x.CostoAlquiler).WithMessage("El monto de reserva no puede ser mayor al costo del alquiler.");

            RuleFor(x => x.CantidadPersonas)
                .GreaterThan(0).WithMessage("La cantidad de personas debe ser mayor a cero.")
                .LessThanOrEqualTo(500).WithMessage("La cantidad de personas no puede exceder 500.");

            RuleFor(x => x.MontoAireAcondicionado)
                .GreaterThanOrEqualTo(0).WithMessage("El monto del aire acondicionado no puede ser negativo.")
                .When(x => x.MontoAireAcondicionado.HasValue);

            RuleFor(x => x.ClienteId)
                .GreaterThan(0).WithMessage("Debe seleccionar un cliente.");

            RuleFor(x => x.ResponsableNombre)
                .NotEmpty().WithMessage("El nombre del responsable es obligatorio.")
                .Length(2, 100).WithMessage("El nombre del responsable debe tener entre 2 y 100 caracteres.");

            RuleFor(x => x.ResponsableTelefono)
                .NotEmpty().WithMessage("El teléfono del responsable es obligatorio.")
                .Length(8, 30).WithMessage("El teléfono debe tener entre 8 y 30 caracteres.");

            RuleFor(x => x.ResponsableCedula)
                .NotEmpty().WithMessage("La cédula del responsable es obligatoria.")
                .Length(7, 30).WithMessage("La cédula debe tener entre 7 y 30 caracteres.");

        }
    }
}
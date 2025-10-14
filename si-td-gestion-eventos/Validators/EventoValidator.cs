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
        }
    }
}
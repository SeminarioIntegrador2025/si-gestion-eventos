using FluentValidation;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;
using System;

namespace si_td_gestion_eventos.Validators
{
    public class ReprogramarEventoValidator : AbstractValidator<ReprogramarEventoVM>
    {
        private readonly IEventoBusinessRules _businessRules;

        public ReprogramarEventoValidator(IEventoBusinessRules businessRules)
        {
            _businessRules = businessRules;

            // REGLA 1: Si NO es indefinida, la fecha es obligatoria y debe ser futura
            RuleFor(x => x.NuevaFecha)
                .NotEmpty().When(x => !x.FechaIndefinida).WithMessage("La nueva fecha es obligatoria si no se marca como indefinida.")
                .GreaterThanOrEqualTo(DateTime.Today).When(x => !x.FechaIndefinida && x.NuevaFecha.HasValue)
                .WithMessage("No puedes reprogramar un evento a una fecha pasada.");

            // REGLA 2: Horarios obligatorios si hay fecha
            RuleFor(x => x.NuevaHoraInicio)
                .NotEmpty().When(x => !x.FechaIndefinida).WithMessage("La hora de inicio es obligatoria.");

            RuleFor(x => x.NuevaHoraFin)
                .NotEmpty().When(x => !x.FechaIndefinida).WithMessage("La hora de fin es obligatoria.")
                .GreaterThan(x => x.NuevaHoraInicio.Value).When(x => !x.FechaIndefinida && x.NuevaHoraInicio.HasValue && x.NuevaHoraFin.HasValue)
                .WithMessage("La hora de fin debe ser posterior a la hora de inicio.");

            // REGLA 3: Disponibilidad (Solo validamos si hay una fecha nueva concreta)
            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .MustAsync(async (model, ct) =>
                {
                    if (model.FechaIndefinida || !model.NuevaFecha.HasValue || !model.NuevaHoraInicio.HasValue || !model.NuevaHoraFin.HasValue)
                        return true; // Si es indefinida, no choca con nadie (se asume cupo liberado)

                    DateTime fechaInicio = model.NuevaFecha.Value.Date;
                    DateTime fechaFin = model.NuevaFecha.Value.Date; // Asume mismo día

                    return await _businessRules.IsDateRangeAvailableAsync(
                        fechaInicio,
                        fechaFin,
                        model.NuevaHoraInicio.Value,
                        model.NuevaHoraFin.Value,
                        model.EventoId
                    );
                })
                .When(x => !x.FechaIndefinida) // Solo ejecuta esto si se eligió fecha
                .WithMessage("El nuevo horario seleccionado entra en conflicto con otro evento.");
        }
    }
}
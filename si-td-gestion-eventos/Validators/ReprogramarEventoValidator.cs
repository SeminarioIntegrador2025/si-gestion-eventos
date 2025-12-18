using FluentValidation;
using si_td_gestion_eventos.Services.Contracts;

public class ReprogramarEventoValidator : AbstractValidator<ReprogramarEventoVM>
{
    private readonly IEventoBusinessRules _businessRules;

    public ReprogramarEventoValidator(IEventoBusinessRules businessRules)
    {
        _businessRules = businessRules;

        When(x => !x.FechaIndefinida, () =>
        {
            // 1. Obligatoriedad
            RuleFor(x => x.NuevaFechaInicio).NotNull().WithMessage("Fecha inicio obligatoria.");
            RuleFor(x => x.NuevaHoraInicio).NotNull().WithMessage("Hora inicio obligatoria.");
            RuleFor(x => x.NuevaFechaFin).NotNull().WithMessage("Fecha fin obligatoria.");
            RuleFor(x => x.NuevaHoraFin).NotNull().WithMessage("Hora fin obligatoria.");

            // 2. Coherencia Temporal (Fin > Inicio)
            RuleFor(x => x)
                .Must(m =>
                {
                    if (!m.NuevaFechaInicio.HasValue || !m.NuevaFechaFin.HasValue ||
                        !m.NuevaHoraInicio.HasValue || !m.NuevaHoraFin.HasValue) return true;

                    var inicio = m.NuevaFechaInicio.Value.Date + m.NuevaHoraInicio.Value;
                    var fin = m.NuevaFechaFin.Value.Date + m.NuevaHoraFin.Value;

                    return fin > inicio;
                })
                .WithMessage("La fecha/hora de fin debe ser posterior al inicio.");

            // 3. No al Pasado
            RuleFor(x => x)
                .Must(m =>
                {
                    if (!m.NuevaFechaInicio.HasValue || !m.NuevaHoraInicio.HasValue) return true;
                    var inicio = m.NuevaFechaInicio.Value.Date + m.NuevaHoraInicio.Value;
                    return inicio >= DateTime.Now.AddMinutes(-1);
                })
                .WithMessage("No puedes programar en el pasado.");

            // 4. Disponibilidad (Lógica Simple y Directa)
            RuleFor(x => x)
                .MustAsync(async (m, ct) =>
                {
                    if (!m.NuevaFechaInicio.HasValue || !m.NuevaFechaFin.HasValue ||
                        !m.NuevaHoraInicio.HasValue || !m.NuevaHoraFin.HasValue) return true;

                    // Pasamos las fechas tal cual las eligió el usuario. Cero magia.
                    return await _businessRules.IsDateRangeAvailableAsync(
                        m.NuevaFechaInicio.Value,
                        m.NuevaFechaFin.Value,
                        m.NuevaHoraInicio.Value,
                        m.NuevaHoraFin.Value,
                        m.EventoId
                    );
                })
                .WithMessage("El salón ya está ocupado en ese rango.");
        });
    }
}
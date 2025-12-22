using FluentValidation;
using si_td_gestion_eventos.Services.Contracts;
using si_td_gestion_eventos.Models.ViewModels;

namespace si_td_gestion_eventos.Validators
{
    public class ReprogramarEventoValidator : AbstractValidator<ReprogramarEventoVM>
    {
        private readonly IEventoBusinessRules _businessRules;

        public ReprogramarEventoValidator(IEventoBusinessRules businessRules)
        {
            _businessRules = businessRules;

            // LA CLAVE: Todo este bloque se salta si FechaIndefinida es TRUE.
            // Esto permite que el formulario pase con fechas vacías (null) cuando es "Por Definir".
            When(x => !x.FechaIndefinida, () =>
            {
                // 1. Obligatoriedad de campos
                RuleFor(x => x.NuevaFechaInicio)
                    .NotNull().WithMessage("La fecha de inicio es obligatoria.");

                RuleFor(x => x.NuevaHoraInicio)
                    .NotNull().WithMessage("La hora de inicio es obligatoria.");

                RuleFor(x => x.NuevaFechaFin)
                    .NotNull().WithMessage("La fecha de fin es obligatoria.");

                RuleFor(x => x.NuevaHoraFin)
                    .NotNull().WithMessage("La hora de fin es obligatoria.");

                // 2. Coherencia Temporal (Fin > Inicio)
                // Usamos 'DependentRules' para ejecutar esto solo si los datos básicos existen
                RuleFor(x => x)
                    .Must(m =>
                    {
                        // Si falta algún dato, devolvemos true para no duplicar mensajes de error (ya saltó el NotNull arriba)
                        if (FaltanDatos(m)) return true;

                        var inicio = m.NuevaFechaInicio!.Value.Date + m.NuevaHoraInicio!.Value;
                        var fin = m.NuevaFechaFin!.Value.Date + m.NuevaHoraFin!.Value;

                        return fin > inicio;
                    })
                    .WithMessage("La fecha de fin debe ser posterior al inicio.");

                // 3. No al Pasado
                RuleFor(x => x)
                    .Must(m =>
                    {
                        if (!m.NuevaFechaInicio.HasValue || !m.NuevaHoraInicio.HasValue) return true;

                        var inicio = m.NuevaFechaInicio.Value.Date + m.NuevaHoraInicio.Value;
                        // Damos 1 minuto de margen por latencia de red
                        return inicio >= DateTime.Now.AddMinutes(-1);
                    })
                    .WithMessage("No puedes programar el evento en el pasado.");

                // 4. Disponibilidad (Lógica de Negocio)
                RuleFor(x => x)
                    .MustAsync(async (m, ct) =>
                    {
                        if (FaltanDatos(m)) return true;

                        return await _businessRules.IsDateRangeAvailableAsync(
                            m.NuevaFechaInicio!.Value,
                            m.NuevaFechaFin!.Value,
                            m.NuevaHoraInicio!.Value,
                            m.NuevaHoraFin!.Value,
                            m.EventoId
                        );
                    })
                    .WithMessage("El salón ya se encuentra ocupado en ese rango de fechas y horas.");
            });
        }

        // Helper privado para limpiar el código y evitar repetir la chequeada de nulos
        private bool FaltanDatos(ReprogramarEventoVM m)
        {
            return !m.NuevaFechaInicio.HasValue ||
                   !m.NuevaFechaFin.HasValue ||
                   !m.NuevaHoraInicio.HasValue ||
                   !m.NuevaHoraFin.HasValue;
        }
    }
}
using FluentValidation;
using si_td_gestion_eventos.Models.ViewModels;
using System;

namespace si_td_gestion_eventos.Validators
{
    public class FianzaValidator : AbstractValidator<FianzaVM>
    {
        public FianzaValidator()
        {

            // 1. Evento Obligatorio
            RuleFor(x => x.EventoId)
                .NotEmpty().WithMessage("Debe seleccionar un evento asociado.");

            // 2. Monto Original
            RuleFor(x => x.Monto)
                .NotEmpty().WithMessage("El monto de la fianza es obligatorio.")
                .GreaterThan(0).WithMessage("El monto de la fianza debe ser mayor a 0.");

            // 3. Fecha de Registro
            RuleFor(x => x.FechaRegistro)
                .NotEmpty().WithMessage("La fecha de registro es obligatoria.")
                .LessThanOrEqualTo(DateTime.Today).WithMessage("La fecha de registro no puede ser futura.");

            // 4. Observaciones (Límite de caracteres)
            RuleFor(x => x.Observaciones)
                .MaximumLength(500).WithMessage("Las observaciones no pueden exceder los 500 caracteres.");

            // REGLAS DE DEVOLUCIÓN 

            // 5. Validación de Monto a Devolver
            When(x => x.MontoDevuelto.HasValue, () =>
            {
                RuleFor(x => x.MontoDevuelto)
                    .GreaterThanOrEqualTo(0).WithMessage("El monto devuelto no puede ser negativo.")
                    .LessThanOrEqualTo(x => x.Monto).WithMessage("El monto devuelto no puede ser mayor al monto original de la fianza.");
            });

            // 6. Validación de Fecha de Devolución
            When(x => x.FechaDevolucion.HasValue, () =>
            {
                RuleFor(x => x.FechaDevolucion)
                    .NotEmpty().WithMessage("Si hay devolución, debe indicar la fecha.")
                    .GreaterThanOrEqualTo(x => x.FechaRegistro)
                    .WithMessage("La fecha de devolución no puede ser anterior a la fecha en que se registró la fianza.")
                    .LessThanOrEqualTo(DateTime.Today).WithMessage("La fecha de devolución no puede ser futura.");
            });

            // 7. Consistencia: Si hay monto devuelto, debe haber fecha (y viceversa)
            RuleFor(x => x.FechaDevolucion)
                .NotNull().When(x => x.MontoDevuelto.HasValue && x.MontoDevuelto > 0)
                .WithMessage("Debe especificar la fecha si ingresa un monto de devolución.");
        }
    }
}
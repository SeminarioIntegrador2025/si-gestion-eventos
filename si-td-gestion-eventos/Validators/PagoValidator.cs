using FluentValidation;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts; 
using Microsoft.AspNetCore.Http; 
using System.IO; 
using System.Linq; 

namespace si_td_gestion_eventos.Validators
{
    public class PagoValidator : AbstractValidator<PagoVM>
    {

        private readonly IEventoService _eventoService;

        public PagoValidator(IEventoService eventoService)
        {
            _eventoService = eventoService;

            RuleFor(p => p.EventoId)
                .GreaterThan(0).WithMessage("El evento asociado no es válido.");

            RuleFor(p => p.Fecha)
                .NotEmpty().WithMessage("La fecha de pago es obligatoria.")
                .LessThanOrEqualTo(DateTime.Today).WithMessage("La fecha de pago no puede ser futura.");

            RuleFor(p => p.Monto)
                .GreaterThan(0).WithMessage("El monto debe ser mayor a cero.")
                .MustAsync(async (pago, monto, cancellation) =>
                    await MontoNoSuperaSaldoAsync(pago, monto, cancellation))
                .WithMessage("El monto del pago no puede superar el saldo restante del evento.");


            RuleFor(p => p.Metodo)
                .IsInEnum().WithMessage("El método de pago no es válido.");

            RuleFor(p => p.ArchivoComprobante)
                .NotNull().WithMessage("Debe adjuntar un archivo de comprobante para transferencias.")
                .When(p => p.Metodo == MetodoPago.Transferencia && p.PagoId == 0);

            RuleFor(p => p.ArchivoComprobante)
                .Must(BeValidFileType).WithMessage("El tipo de archivo no es válido (solo PDF, JPG, PNG).")
                .When(p => p.ArchivoComprobante != null);

            RuleFor(p => p.ReferenciaComprobante)
                .NotEmpty().WithMessage("Debe ingresar una referencia para la transferencia.")
                .When(p => p.Metodo == MetodoPago.Transferencia);

            RuleFor(p => p.Observaciones)
                .MaximumLength(500).WithMessage("Las observaciones no pueden exceder los 500 caracteres.");
        }

        private async Task<bool> MontoNoSuperaSaldoAsync(PagoVM pago, float monto, CancellationToken cancellation)
        {
            if (pago.EventoId <= 0)
            {
                return true;
            }

            var evento = await _eventoService.GetByIdAsync(pago.EventoId);

            if (evento == null)
            {
                return false; 
            }

            return (decimal)monto <= evento.SaldoRestante;
        }


        //Helper
        private bool BeValidFileType(IFormFile? file)
        {
            if (file == null) return true;
            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant(); // Agregado '?'
            return extension != null && allowedExtensions.Contains(extension);
        }

    }

}

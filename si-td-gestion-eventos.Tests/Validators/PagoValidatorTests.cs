using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Http;
using Moq;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;
using si_td_gestion_eventos.Validators;
using Xunit;

namespace si_td_gestion_eventos.Tests.Validators
{
    public class PagoValidatorTests
    {
        private readonly Mock<IEventoService> _mockEventoService;
        private readonly PagoValidator _validator;

        public PagoValidatorTests()
        {
            _mockEventoService = new Mock<IEventoService>();
            _validator = new PagoValidator(_mockEventoService.Object);
        }

        #region EventoId Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public async Task Validate_EventoIdInvalido_DeberiaFallar(int eventoId)
        {
            // Arrange
            var pago = new PagoVM
            {
                EventoId = eventoId,
                Fecha = DateTime.Today,
                Monto = 1000,
                Metodo = MetodoPago.Efectivo
            };

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldHaveValidationErrorFor(p => p.EventoId)
                .WithErrorMessage("El evento asociado no es válido.");
        }

        [Fact]
        public async Task Validate_EventoIdValido_DeberiaSerExitoso()
        {
            // Arrange
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 1000,
                Metodo = MetodoPago.Efectivo
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldNotHaveValidationErrorFor(p => p.EventoId);
        }

        #endregion

        #region Fecha Tests

        [Fact]
        public async Task Validate_FechaVacia_DeberiaFallar()
        {
            // Arrange
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = default,
                Monto = 1000,
                Metodo = MetodoPago.Efectivo
            };

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldHaveValidationErrorFor(p => p.Fecha)
                .WithErrorMessage("La fecha de pago es obligatoria.");
        }

        [Fact]
        public async Task Validate_FechaFutura_DeberiaFallar()
        {
            // Arrange
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today.AddDays(1),
                Monto = 1000,
                Metodo = MetodoPago.Efectivo
            };

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldHaveValidationErrorFor(p => p.Fecha)
                .WithErrorMessage("La fecha de pago no puede ser futura.");
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-10)]
        [InlineData(-30)]
        [InlineData(0)]
        public async Task Validate_FechaPasadaOHoy_DeberiaSerExitoso(int diasAtras)
        {
            // Arrange
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today.AddDays(diasAtras),
                Monto = 1000,
                Metodo = MetodoPago.Efectivo
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldNotHaveValidationErrorFor(p => p.Fecha);
        }

        #endregion

        #region Monto Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        [InlineData(-0.01)]
        public async Task Validate_MontoCeroONegativo_DeberiaFallar(float monto)
        {
            // Arrange
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = monto,
                Metodo = MetodoPago.Efectivo
            };

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldHaveValidationErrorFor(p => p.Monto)
                .WithErrorMessage("El monto debe ser mayor a cero.");
        }

        [Fact]
        public async Task Validate_MontoSuperaSaldo_DeberiaFallar()
        {
            // Arrange
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 3000,
                Metodo = MetodoPago.Efectivo
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldHaveValidationErrorFor(p => p.Monto)
                .WithErrorMessage("El monto del pago no puede superar el saldo restante del evento.");
        }

        [Theory]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(2000)]
        [InlineData(0.01)]
        public async Task Validate_MontoValidoDentroDelSaldo_DeberiaSerExitoso(float monto)
        {
            // Arrange
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = monto,
                Metodo = MetodoPago.Efectivo
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldNotHaveValidationErrorFor(p => p.Monto);
        }

        [Fact]
        public async Task Validate_EventoNoExiste_MontoDeberiaFallar()
        {
            // Arrange
            var pago = new PagoVM
            {
                EventoId = 999,
                Fecha = DateTime.Today,
                Monto = 1000,
                Metodo = MetodoPago.Efectivo
            };

            _mockEventoService.Setup(e => e.GetByIdAsync(999)).ReturnsAsync((EventoVM?)null);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldHaveValidationErrorFor(p => p.Monto);
        }

        [Fact]
        public async Task Validate_MontoExactoAlSaldo_DeberiaSerExitoso()
        {
            // Arrange
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 2000,
                Metodo = MetodoPago.Efectivo
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldNotHaveValidationErrorFor(p => p.Monto);
        }

        #endregion

        #region Método de Pago Tests

        [Fact]
        public async Task Validate_MetodoEfectivo_DeberiaSerExitoso()
        {
            // Arrange
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 1000,
                Metodo = MetodoPago.Efectivo
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldNotHaveValidationErrorFor(p => p.Metodo);
        }

        [Fact]
        public async Task Validate_MetodoTransferencia_DeberiaSerExitoso()
        {
            // Arrange
            var mockFile = CreateMockPdfFile();
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 1000,
                Metodo = MetodoPago.Transferencia,
                ArchivoComprobante = mockFile.Object
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldNotHaveValidationErrorFor(p => p.Metodo);
        }

        #endregion

        #region ArchivoComprobante Tests

        [Fact]
        public async Task Validate_TransferenciaSinComprobante_DeberiaFallar()
        {
            // Arrange
            var pago = new PagoVM
            {
                PagoId = 0,
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 1000,
                Metodo = MetodoPago.Transferencia,
                ArchivoComprobante = null
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldHaveValidationErrorFor(p => p.ArchivoComprobante)
                .WithErrorMessage("Debe adjuntar un archivo de comprobante para transferencias.");
        }

        [Fact]
        public async Task Validate_TransferenciaConComprobante_DeberiaSerExitoso()
        {
            // Arrange
            var mockFile = CreateMockPdfFile();
            var pago = new PagoVM
            {
                PagoId = 0,
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 1000,
                Metodo = MetodoPago.Transferencia,
                ArchivoComprobante = mockFile.Object
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldNotHaveValidationErrorFor(p => p.ArchivoComprobante);
        }

        [Fact]
        public async Task Validate_EfectivoSinComprobante_DeberiaSerExitoso()
        {
            // Arrange
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 1000,
                Metodo = MetodoPago.Efectivo,
                ArchivoComprobante = null
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldNotHaveValidationErrorFor(p => p.ArchivoComprobante);
        }

        [Fact]
        public async Task Validate_EdicionTransferenciaSinComprobante_DeberiaSerExitoso()
        {
            // Arrange - PagoId > 0 indica edición
            var pago = new PagoVM
            {
                PagoId = 5,
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 1000,
                Metodo = MetodoPago.Transferencia,
                ArchivoComprobante = null
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldNotHaveValidationErrorFor(p => p.ArchivoComprobante);
        }

        [Theory]
        [InlineData(".pdf")]
        [InlineData(".jpg")]
        [InlineData(".jpeg")]
        [InlineData(".png")]
        public async Task Validate_ArchivoTipoValido_DeberiaSerExitoso(string extension)
        {
            // Arrange
            var mockFile = CreateMockFile($"comprobante{extension}", extension);
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 1000,
                Metodo = MetodoPago.Transferencia,
                ArchivoComprobante = mockFile.Object
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldNotHaveValidationErrorFor(p => p.ArchivoComprobante);
        }

        [Theory]
        [InlineData(".PDF")]
        [InlineData(".JPG")]
        [InlineData(".JPEG")]
        [InlineData(".PNG")]
        [InlineData(".Pdf")]
        [InlineData(".Jpg")]
        public async Task Validate_ArchivoExtensionMayusculas_DeberiaSerExitoso(string extension)
        {
            // Arrange
            var mockFile = CreateMockFile($"comprobante{extension}", extension);
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 1000,
                Metodo = MetodoPago.Transferencia,
                ArchivoComprobante = mockFile.Object
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldNotHaveValidationErrorFor(p => p.ArchivoComprobante);
        }

        [Theory]
        [InlineData(".txt")]
        [InlineData(".doc")]
        [InlineData(".docx")]
        [InlineData(".exe")]
        [InlineData(".zip")]
        [InlineData(".rar")]
        public async Task Validate_ArchivoTipoInvalido_DeberiaFallar(string extension)
        {
            // Arrange
            var mockFile = CreateMockFile($"archivo{extension}", extension);
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 1000,
                Metodo = MetodoPago.Transferencia,
                ArchivoComprobante = mockFile.Object
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldHaveValidationErrorFor(p => p.ArchivoComprobante)
                .WithErrorMessage("El tipo de archivo no es válido (solo PDF, JPG, PNG).");
        }

        [Fact]
        public async Task Validate_ArchivoSinExtension_DeberiaFallar()
        {
            // Arrange
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns("archivosinextension");
            mockFile.Setup(f => f.Length).Returns(1024);

            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 1000,
                Metodo = MetodoPago.Transferencia,
                ArchivoComprobante = mockFile.Object
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldHaveValidationErrorFor(p => p.ArchivoComprobante);
        }

        #endregion

        #region Observaciones Tests

        [Fact]
        public async Task Validate_ObservacionesVacias_DeberiaSerExitoso()
        {
            // Arrange
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 1000,
                Metodo = MetodoPago.Efectivo,
                Observaciones = null
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldNotHaveValidationErrorFor(p => p.Observaciones);
        }

        [Fact]
        public async Task Validate_ObservacionesCortas_DeberiaSerExitoso()
        {
            // Arrange
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 1000,
                Metodo = MetodoPago.Efectivo,
                Observaciones = "Pago completo del evento"
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldNotHaveValidationErrorFor(p => p.Observaciones);
        }

        [Fact]
        public async Task Validate_ObservacionesMaximo500_DeberiaSerExitoso()
        {
            // Arrange
            var observaciones = new string('A', 500);
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 1000,
                Metodo = MetodoPago.Efectivo,
                Observaciones = observaciones
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldNotHaveValidationErrorFor(p => p.Observaciones);
        }

        [Fact]
        public async Task Validate_ObservacionesMayor500_DeberiaFallar()
        {
            // Arrange
            var observaciones = new string('A', 501);
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 1000,
                Metodo = MetodoPago.Efectivo,
                Observaciones = observaciones
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldHaveValidationErrorFor(p => p.Observaciones)
                .WithErrorMessage("Las observaciones no pueden exceder los 500 caracteres.");
        }

        #endregion

        #region Escenarios Completos

        [Fact]
        public async Task Validate_PagoCompletoValido_DeberiaSerExitoso()
        {
            // Arrange
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 1500,
                Metodo = MetodoPago.Efectivo,
                Observaciones = "Pago total del evento de cumpleaños"
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public async Task Validate_PagoTransferenciaCompletoValido_DeberiaSerExitoso()
        {
            // Arrange
            var mockFile = CreateMockPdfFile();
            var pago = new PagoVM
            {
                PagoId = 0,
                EventoId = 1,
                Fecha = DateTime.Today.AddDays(-5),
                Monto = 1500,
                Metodo = MetodoPago.Transferencia,
                ArchivoComprobante = mockFile.Object,
                Observaciones = "Transferencia realizada el 15/01/2024"
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public async Task Validate_PagoParcialValido_DeberiaSerExitoso()
        {
            // Arrange
            var pago = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 500,
                Metodo = MetodoPago.Efectivo,
                Observaciones = "Pago parcial - Primera cuota"
            };

            var evento = CreateValidEventoVM(saldoRestante: 2000);
            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(evento);

            // Act
            var result = await _validator.TestValidateAsync(pago);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        #endregion

        #region Helper Methods

        private EventoVM CreateValidEventoVM(decimal saldoRestante = 2000)
        {
            return new EventoVM
            {
                EventoId = 1,
                ClienteId = 1,
                Tipo = TipoEvento.Cumpleaños,
                FechaContrato = DateTime.Today,
                Inicio = DateTime.Today.AddDays(10),
                Fin = DateTime.Today.AddDays(10),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                CantidadPersonas = 50,
                CostoAlquiler = 3000,
                MontoReserva = 500,
                MontoAireAcondicionado = 0,
                ResponsableNombre = "Juan Pérez",
                ResponsableTelefono = "099123456",
                ResponsableCedula = "12345678",
                SaldoRestante = saldoRestante,
                TotalPagado = 3000 - saldoRestante
            };
        }

        private Mock<IFormFile> CreateMockPdfFile()
        {
            return CreateMockFile("comprobante.pdf", ".pdf");
        }

        private Mock<IFormFile> CreateMockFile(string fileName, string extension)
        {
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.Length).Returns(1024);
            mockFile.Setup(f => f.ContentType).Returns(GetContentType(extension));

            return mockFile;
        }

        private string GetContentType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "application/octet-stream"
            };
        }

        #endregion
    }
}

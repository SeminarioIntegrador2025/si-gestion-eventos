using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Moq;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Repositories;
using si_td_gestion_eventos.Services.Common;
using si_td_gestion_eventos.Services.Contracts;
using si_td_gestion_eventos.Services.Implementation;
using System.Linq.Expressions;
using Xunit;

namespace si_td_gestion_eventos.Tests.Services
{
    public class PagoServiceTests
    {
        // --- Mocks: Simuladores de dependencias ---
        private readonly Mock<IGenericRepository<Pago>> _mockPagoRepository;
        private readonly Mock<IGenericRepository<ComprobanteExterno>> _mockComprobanteRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IValidator<PagoVM>> _mockValidator;
        private readonly Mock<IWebHostEnvironment> _mockWebHostEnvironment;
        private readonly Mock<IEventoService> _mockEventoService;
        // NUEVO: Mock para el servicio de almacenamiento de archivos
        private readonly Mock<IFileStorageService> _mockFileStorageService;

        // System Under Test (SUT): La clase que estamos probando
        private readonly PagoService _sut;

        public PagoServiceTests()
        {
            // Configuración de licencia para QuestPDF (necesario para tests de generación de PDF)
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            // Inicialización de todos los Mocks
            _mockPagoRepository = new Mock<IGenericRepository<Pago>>();
            _mockComprobanteRepository = new Mock<IGenericRepository<ComprobanteExterno>>();
            _mockMapper = new Mock<IMapper>();
            _mockValidator = new Mock<IValidator<PagoVM>>();
            _mockWebHostEnvironment = new Mock<IWebHostEnvironment>();
            _mockEventoService = new Mock<IEventoService>();
            _mockFileStorageService = new Mock<IFileStorageService>();

            // Configuración básica del entorno
            _mockWebHostEnvironment.Setup(w => w.WebRootPath).Returns("C:\\wwwroot");

            // --- INSTANCIACIÓN DEL SERVICIO ---
            // Se agregan todos los objetos mockeados al constructor
            _sut = new PagoService(
                _mockPagoRepository.Object,
                _mockComprobanteRepository.Object,
                _mockMapper.Object,
                _mockValidator.Object,
                _mockWebHostEnvironment.Object,
                _mockEventoService.Object,
                _mockFileStorageService.Object 
            );
        }

        #region GetPagosByEventoIdAsync Tests

        [Fact]
        public async Task GetPagosByEventoIdAsync_EventoConPagos_DeberiaRetornarListaPagos()
        {
            // Arrange
            int eventoId = 1;
            var pagos = new List<Pago>
            {
                new Pago { PagoId = 1, EventoId = eventoId, Monto = 1000, Fecha = DateTime.Now, Metodo = MetodoPago.Efectivo, Valido = true },
                new Pago { PagoId = 2, EventoId = eventoId, Monto = 500, Fecha = DateTime.Now, Metodo = MetodoPago.Transferencia, Valido = true }
            };

            var pagosVM = new List<PagoVM>
            {
                new PagoVM { PagoId = 1, EventoId = eventoId, Monto = 1000, Metodo = MetodoPago.Efectivo },
                new PagoVM { PagoId = 2, EventoId = eventoId, Monto = 500, Metodo = MetodoPago.Transferencia }
            };

            var eventoVM = new EventoVM
            {
                EventoId = eventoId,
                Tipo = TipoEvento.Cumpleaños,
                Inicio = DateTime.Now,
                ResponsableNombre = "Juan",
                CostoAlquiler = 2000,
                MontoReserva = 500,
                CantidadPersonas = 50,
                ClienteId = 1,
                Fin = DateTime.Now.AddHours(5),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                FechaContrato = DateTime.Now,
                ClienteNombreCompleto = "Juan Pérez"
            };

            _mockPagoRepository.Setup(r => r.FindWithIncludesAsync(
                It.IsAny<Expression<Func<Pago, bool>>>(),
                It.IsAny<Expression<Func<Pago, object>>[]>()
            )).ReturnsAsync(pagos);

            _mockMapper.Setup(m => m.Map<List<PagoVM>>(pagos)).Returns(pagosVM);
            _mockEventoService.Setup(e => e.GetByIdAsync(eventoId)).ReturnsAsync(eventoVM);

            // Act
            var result = await _sut.GetPagosByEventoIdAsync(eventoId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.All(result, p => Assert.Equal(eventoId, p.EventoId));
        }

        [Fact]
        public async Task GetPagosByEventoIdAsync_EventoSinPagos_DeberiaRetornarListaVacia()
        {
            // Arrange
            int eventoId = 1;
            _mockPagoRepository.Setup(r => r.FindWithIncludesAsync(
                It.IsAny<Expression<Func<Pago, bool>>>(),
                It.IsAny<Expression<Func<Pago, object>>[]>()
            )).ReturnsAsync(new List<Pago>());

            _mockMapper.Setup(m => m.Map<List<PagoVM>>(It.IsAny<List<Pago>>())).Returns(new List<PagoVM>());

            // Act
            var result = await _sut.GetPagosByEventoIdAsync(eventoId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        #endregion

        #region GetByIdAsync Tests

        [Fact]
        public async Task GetByIdAsync_PagoExisteConPersonaFisica_DeberiaRetornarPagoConNombreCompleto()
        {
            // Arrange
            var pago = new Pago
            {
                PagoId = 1,
                EventoId = 1,
                Monto = 1000,
                Fecha = DateTime.Now,
                Metodo = MetodoPago.Efectivo,
                Evento = new Evento
                {
                    EventoId = 1,
                    Tipo = TipoEvento.Cumpleaños,
                    Inicio = DateTime.Now,
                    Cliente = new Cliente
                    {
                        ClienteId = 1,
                        Nombre = "Juan",
                        Apellido = "Pérez",
                        Tipo = TipoCliente.PersonaFisica
                    }
                }
            };

            var pagoVM = new PagoVM { PagoId = 1, EventoId = 1, Monto = 1000, Metodo = MetodoPago.Efectivo };

            _mockPagoRepository.Setup(r => r.GetByIdWithIncludesAsync(
                1,
                It.IsAny<Expression<Func<Pago, object>>[]>()
            )).ReturnsAsync(pago);

            _mockMapper.Setup(m => m.Map<PagoVM>(pago)).Returns(pagoVM);

            // Act
            var result = await _sut.GetByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Juan Pérez", result.ClienteNombre);
        }

        #endregion

        #region CreateAsync Tests

        [Fact]
        public async Task CreateAsync_ValidacionExitosa_DeberiaCrearPago()
        {
            // Arrange
            var pagoVM = new PagoVM
            {
                EventoId = 1,
                Fecha = DateTime.Today,
                Monto = 1000,
                Metodo = MetodoPago.Efectivo
            };

            var pagoEntity = new Pago
            {
                PagoId = 1,
                EventoId = 1,
                Monto = 1000,
                Fecha = DateTime.Today,
                Metodo = MetodoPago.Efectivo,
                Valido = true
            };

            var eventoVM = new EventoVM
            {
                EventoId = 1,
                CostoAlquiler = 2000,
                SaldoRestante = 1000, // Después del pago quedará 0
                Estado = EventoEstado.PendienteAdeudado
            };

            _mockValidator.Setup(v => v.ValidateAsync(It.IsAny<PagoVM>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FluentValidation.Results.ValidationResult());

            _mockMapper.Setup(m => m.Map<Pago>(pagoVM)).Returns(pagoEntity);
            
            _mockPagoRepository.Setup(r => r.AddAsync(It.IsAny<Pago>())).Returns(Task.CompletedTask);
            _mockPagoRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            _mockEventoService.Setup(s => s.GetByIdAsync(1)).ReturnsAsync(eventoVM);

            _mockEventoService.Setup(s => s.UpdateAsync(It.IsAny<EventoVM>()))
                .ReturnsAsync(ServiceResult<EventoVM>.SuccessResult(eventoVM, "Actualizado"));

            _mockPagoRepository.Setup(r => r.GetByIdWithIncludesAsync(
                1, 
                It.IsAny<Expression<Func<Pago, object>>[]>()
            )).ReturnsAsync(pagoEntity);

            _mockMapper.Setup(m => m.Map<PagoVM>(pagoEntity)).Returns(pagoVM);

            // Act
            var result = await _sut.CreateAsync(pagoVM);

            // Assert
            Assert.True(result.Success); 
            Assert.NotNull(result.Data);
            Assert.Equal("Pago registrado exitosamente.", result.Message);
        }

        #endregion

        #region GenerarReciboPdfAsync Tests

        [Fact]
        public async Task GenerarReciboPdfAsync_PagoExiste_DeberiaGenerarPdf()
        {
            // Arrange
            var pago = new Pago { PagoId = 1, Monto = 1000 };
            var pagoVM = new PagoVM { PagoId = 1, Monto = 1000, Fecha = DateTime.Now };

            _mockPagoRepository.Setup(r => r.GetByIdWithIncludesAsync(1, It.IsAny<Expression<Func<Pago, object>>[]>()))
                               .ReturnsAsync(pago);
            _mockMapper.Setup(m => m.Map<PagoVM>(pago)).Returns(pagoVM);

            // Act
            var result = await _sut.GenerarReciboPdfAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<byte[]>(result);
        }

        #endregion
    }
}
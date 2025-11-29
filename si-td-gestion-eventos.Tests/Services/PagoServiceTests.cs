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
        private readonly Mock<IGenericRepository<Pago>> _mockPagoRepository;
        private readonly Mock<IGenericRepository<ComprobanteExterno>> _mockComprobanteRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IValidator<PagoVM>> _mockValidator;
        private readonly Mock<IWebHostEnvironment> _mockWebHostEnvironment;
        private readonly Mock<IEventoService> _mockEventoService;
        private readonly PagoService _sut;


        public PagoServiceTests()
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            _mockPagoRepository = new Mock<IGenericRepository<Pago>>();
            _mockComprobanteRepository = new Mock<IGenericRepository<ComprobanteExterno>>();
            _mockMapper = new Mock<IMapper>();
            _mockValidator = new Mock<IValidator<PagoVM>>();
            _mockWebHostEnvironment = new Mock<IWebHostEnvironment>();
            _mockEventoService = new Mock<IEventoService>();

            _mockWebHostEnvironment.Setup(w => w.WebRootPath).Returns("C:\\wwwroot");

            _sut = new PagoService(
                _mockPagoRepository.Object,
                _mockComprobanteRepository.Object,
                _mockMapper.Object,
                _mockValidator.Object,
                _mockWebHostEnvironment.Object,
                _mockEventoService.Object
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
                new Pago { PagoId = 1, EventoId = eventoId, Monto = 1000, Fecha = DateTime.Now, Metodo = MetodoPago.Efectivo },
                new Pago { PagoId = 2, EventoId = eventoId, Monto = 500, Fecha = DateTime.Now, Metodo = MetodoPago.Transferencia }
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
            Assert.All(result, p => Assert.NotNull(p.EventoDescripcion));
            Assert.All(result, p => Assert.NotNull(p.ClienteNombre));
        }

        [Fact]
        public async Task GetPagosByEventoIdAsync_EventoSinPagos_DeberiaRetornarListaVacia()
        {
            // Arrange
            int eventoId = 1;
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
                FechaContrato = DateTime.Now
            };

            _mockPagoRepository.Setup(r => r.FindWithIncludesAsync(
                It.IsAny<Expression<Func<Pago, bool>>>(),
                It.IsAny<Expression<Func<Pago, object>>[]>()
            )).ReturnsAsync(new List<Pago>());

            _mockMapper.Setup(m => m.Map<List<PagoVM>>(It.IsAny<List<Pago>>())).Returns(new List<PagoVM>());
            _mockEventoService.Setup(e => e.GetByIdAsync(eventoId)).ReturnsAsync(eventoVM);

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
            Assert.Equal(1, result.PagoId);
            Assert.Contains("Juan Pérez", result.ClienteNombre);
        }

        [Fact]
        public async Task GetByIdAsync_PagoExisteConPersonaJuridica_DeberiaRetornarPagoConRazonSocial()
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
                    Tipo = TipoEvento.Corporativo, 
                    Inicio = DateTime.Now,
                    Cliente = new Cliente 
                    { 
                        ClienteId = 1, 
                        Nombre = "Empresa ABC S.A.", 
                        Tipo = TipoCliente.PersonaJuridica 
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
            Assert.Equal("Empresa ABC S.A.", result.ClienteNombre);
        }

        [Fact]
        public async Task GetByIdAsync_PagoNoExiste_DeberiaRetornarNull()
        {
            // Arrange
            _mockPagoRepository.Setup(r => r.GetByIdWithIncludesAsync(
                999, 
                It.IsAny<Expression<Func<Pago, object>>[]>()
            )).ReturnsAsync((Pago?)null);

            // Act
            var result = await _sut.GetByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetByIdAsync_PagoConComprobante_DeberiaIncluirRutaArchivo()
        {
            // Arrange
            var pago = new Pago 
            { 
                PagoId = 1, 
                EventoId = 1, 
                Monto = 1000, 
                Fecha = DateTime.Now, 
                Metodo = MetodoPago.Transferencia,
                ComprobanteExterno = new ComprobanteExterno 
                { 
                    ComprobanteExternoId = 1,
                    PagoId = 1,
                    RutaArchivo = "/uploads/comprobantes/test.pdf",
                    NombreArchivo = "test.pdf",
                    FechaComprobante = DateTime.Now,
                    Pago = new Pago()
                },
                Evento = new Evento 
                { 
                    EventoId = 1, 
                    Tipo = TipoEvento.Cumpleaños, 
                    Inicio = DateTime.Now,
                    Cliente = new Cliente { ClienteId = 1, Nombre = "Juan", Apellido = "Pérez", Tipo = TipoCliente.PersonaFisica }
                }
            };

            var pagoVM = new PagoVM { PagoId = 1, EventoId = 1, Monto = 1000, Metodo = MetodoPago.Transferencia };

            _mockPagoRepository.Setup(r => r.GetByIdWithIncludesAsync(
                1, 
                It.IsAny<Expression<Func<Pago, object>>[]>()
            )).ReturnsAsync(pago);

            _mockMapper.Setup(m => m.Map<PagoVM>(pago)).Returns(pagoVM);

            // Act
            var result = await _sut.GetByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("/uploads/comprobantes/test.pdf", result.RutaArchivoExistente);
        }

        #endregion

        #region GetAllAsync Tests

        [Fact]
        public async Task GetAllAsync_DeberiaRetornarTodosLosPagosConDatosCalculados()
        {
            // Arrange
            var pagos = new List<Pago>
            {
                new Pago 
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
                        Cliente = new Cliente { Nombre = "Juan", Apellido = "Pérez", Tipo = TipoCliente.PersonaFisica }
                    }
                },
                new Pago 
                { 
                    PagoId = 2, 
                    EventoId = 2, 
                    Monto = 2000, 
                    Fecha = DateTime.Now, 
                    Metodo = MetodoPago.Transferencia,
                    Evento = new Evento 
                    { 
                        EventoId = 2,
                        Tipo = TipoEvento.Casamiento, 
                        Inicio = DateTime.Now.AddDays(10),
                        Cliente = new Cliente { Nombre = "María", Apellido = "González", Tipo = TipoCliente.PersonaFisica }
                    }
                }
            };

            var pagosVM = new List<PagoVM>
            {
                new PagoVM { PagoId = 1, EventoId = 1, Monto = 1000, Metodo = MetodoPago.Efectivo },
                new PagoVM { PagoId = 2, EventoId = 2, Monto = 2000, Metodo = MetodoPago.Transferencia }
            };

            _mockPagoRepository.Setup(r => r.FindWithIncludesAsync(
                It.IsAny<Expression<Func<Pago, bool>>>(),
                It.IsAny<Expression<Func<Pago, object>>[]>()
            )).ReturnsAsync(pagos);

            _mockMapper.Setup(m => m.Map<List<PagoVM>>(pagos)).Returns(pagosVM);

            // Act
            var result = await _sut.GetAllAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.All(result, p => Assert.NotNull(p.EventoDescripcion));
            Assert.All(result, p => Assert.NotNull(p.ClienteNombre));
        }

        #endregion

        #region CreateAsync Tests

        [Fact]
        public async Task CreateAsync_ValidacionExitosa_SinComprobante_DeberiaCrearPago()
        {
            // Arrange
            var pagoVM = new PagoVM
            {
                EventoId = 1,
                Monto = 500,
                Fecha = DateTime.Now,
                Metodo = MetodoPago.Efectivo
            };

            var pagoEntity = new Pago
            {
                PagoId = 1,
                EventoId = 1,
                Monto = 500,
                Fecha = DateTime.Now,
                Metodo = MetodoPago.Efectivo
            };

            _mockValidator.Setup(v => v.ValidateAsync(pagoVM, default))
                .ReturnsAsync(new ValidationResult());

            _mockMapper.Setup(m => m.Map<Pago>(pagoVM)).Returns(pagoEntity);
            _mockPagoRepository.Setup(r => r.AddAsync(It.IsAny<Pago>())).Returns(Task.CompletedTask);
            _mockPagoRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            var eventoVM = new EventoVM 
            { 
                EventoId = 1, 
                Estado = EventoEstado.PendienteAdeudado,
                CostoAlquiler = 2000,
                MontoAireAcondicionado = 0,
                TotalPagado = 0,
                ResponsableNombre = "Juan",
                MontoReserva = 500,
                CantidadPersonas = 50,
                ClienteId = 1,
                Inicio = DateTime.Now,
                Fin = DateTime.Now.AddHours(5),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                FechaContrato = DateTime.Now
            };

            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(eventoVM);

            var pagoConEvento = new Pago
            {
                PagoId = 1,
                EventoId = 1,
                Monto = 500,
                Fecha = DateTime.Now,
                Metodo = MetodoPago.Efectivo,
                Evento = new Evento
                {
                    EventoId = 1,
                    Tipo = TipoEvento.Cumpleaños,
                    Inicio = DateTime.Now,
                    Cliente = new Cliente { Nombre = "Juan", Apellido = "Pérez", Tipo = TipoCliente.PersonaFisica }
                }
            };

            var pagoVMRetornado = new PagoVM { PagoId = 1, EventoId = 1, Monto = 500, Metodo = MetodoPago.Efectivo };
            
            _mockPagoRepository.Setup(r => r.GetByIdWithIncludesAsync(
                1, 
                It.IsAny<Expression<Func<Pago, object>>[]>()
            )).ReturnsAsync(pagoConEvento);
            
            _mockMapper.Setup(m => m.Map<PagoVM>(pagoConEvento)).Returns(pagoVMRetornado);
            _mockEventoService.Setup(e => e.UpdateAsync(It.IsAny<EventoVM>())).ReturnsAsync(new ServiceResult<EventoVM>());

            // Act
            var result = await _sut.CreateAsync(pagoVM);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Pago registrado exitosamente.", result.Message);
            Assert.NotNull(result.Data);
            _mockPagoRepository.Verify(r => r.AddAsync(It.IsAny<Pago>()), Times.Once);
            _mockPagoRepository.Verify(r => r.SaveChangesAsync(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task CreateAsync_PagoCompletaEvento_DeberiaActualizarEstadoAPendientePagado()
        {
            // Arrange
            var pagoVM = new PagoVM
            {
                EventoId = 1,
                Monto = 1500,
                Fecha = DateTime.Now,
                Metodo = MetodoPago.Efectivo
            };

            var pagoEntity = new Pago
            {
                PagoId = 1,
                EventoId = 1,
                Monto = 1500,
                Fecha = DateTime.Now,
                Metodo = MetodoPago.Efectivo
            };

            _mockValidator.Setup(v => v.ValidateAsync(pagoVM, default))
                .ReturnsAsync(new ValidationResult());

            _mockMapper.Setup(m => m.Map<Pago>(pagoVM)).Returns(pagoEntity);
            _mockPagoRepository.Setup(r => r.AddAsync(It.IsAny<Pago>())).Returns(Task.CompletedTask);
            _mockPagoRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            var eventoVM = new EventoVM 
            { 
                EventoId = 1, 
                Estado = EventoEstado.PendienteAdeudado,
                CostoAlquiler = 2000,
                MontoAireAcondicionado = 0,
                TotalPagado = 500,
                ResponsableNombre = "Juan",
                MontoReserva = 500,
                CantidadPersonas = 50,
                ClienteId = 1,
                Inicio = DateTime.Now,
                Fin = DateTime.Now.AddHours(5),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                FechaContrato = DateTime.Now
            };

            _mockEventoService.Setup(e => e.GetByIdAsync(1)).ReturnsAsync(eventoVM);

            var pagoConEvento = new Pago
            {
                PagoId = 1,
                EventoId = 1,
                Monto = 1500,
                Fecha = DateTime.Now,
                Metodo = MetodoPago.Efectivo,
                Evento = new Evento
                {
                    EventoId = 1,
                    Tipo = TipoEvento.Cumpleaños,
                    Inicio = DateTime.Now,
                    Cliente = new Cliente { Nombre = "Juan", Apellido = "Pérez", Tipo = TipoCliente.PersonaFisica }
                }
            };

            var pagoVMRetornado = new PagoVM { PagoId = 1, EventoId = 1, Monto = 1500, Metodo = MetodoPago.Efectivo };
            
            _mockPagoRepository.Setup(r => r.GetByIdWithIncludesAsync(
                1, 
                It.IsAny<Expression<Func<Pago, object>>[]>()
            )).ReturnsAsync(pagoConEvento);
            
            _mockMapper.Setup(m => m.Map<PagoVM>(pagoConEvento)).Returns(pagoVMRetornado);
            _mockEventoService.Setup(e => e.UpdateAsync(It.IsAny<EventoVM>())).ReturnsAsync(new ServiceResult<EventoVM>());

            // Act
            var result = await _sut.CreateAsync(pagoVM);

            // Assert
            Assert.True(result.Success);
            _mockEventoService.Verify(e => e.UpdateAsync(It.Is<EventoVM>(ev => ev.Estado == EventoEstado.PendientePagado)), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ValidacionFallida_DeberiaRetornarErrores()
        {
            // Arrange
            var pagoVM = new PagoVM
            {
                EventoId = 1,
                Monto = -100,
                Fecha = DateTime.Now,
                Metodo = MetodoPago.Efectivo
            };

            var validationFailures = new List<ValidationFailure>
            {
                new ValidationFailure("Monto", "El monto debe ser mayor a cero.")
            };

            _mockValidator.Setup(v => v.ValidateAsync(pagoVM, default))
                .ReturnsAsync(new ValidationResult(validationFailures));

            // Act
            var result = await _sut.CreateAsync(pagoVM);

            // Assert
            Assert.False(result.Success);
            Assert.Single(result.Errors);
            Assert.Contains("El monto debe ser mayor a cero.", result.Errors);
            _mockPagoRepository.Verify(r => r.AddAsync(It.IsAny<Pago>()), Times.Never);
        }

        #endregion

        #region GenerarReciboPdfAsync Tests

        [Fact]
        public async Task GenerarReciboPdfAsync_PagoExiste_DeberiaGenerarPdf()
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
                    Cliente = new Cliente { ClienteId = 1, Nombre = "Juan", Apellido = "Pérez", Tipo = TipoCliente.PersonaFisica }
                }
            };

            var pagoVM = new PagoVM 
            { 
                PagoId = 1, 
                EventoId = 1, 
                Monto = 1000, 
                Metodo = MetodoPago.Efectivo,
                Fecha = DateTime.Now
            };

            _mockPagoRepository.Setup(r => r.GetByIdWithIncludesAsync(
                1, 
                It.IsAny<Expression<Func<Pago, object>>[]>()
            )).ReturnsAsync(pago);

            _mockMapper.Setup(m => m.Map<PagoVM>(pago)).Returns(pagoVM);

            // Act
            var result = await _sut.GenerarReciboPdfAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<byte[]>(result);
        }

        [Fact]
        public async Task GenerarReciboPdfAsync_PagoNoExiste_DeberiaRetornarNull()
        {
            // Arrange
            _mockPagoRepository.Setup(r => r.GetByIdWithIncludesAsync(
                999, 
                It.IsAny<Expression<Func<Pago, object>>[]>()
            )).ReturnsAsync((Pago?)null);

            // Act
            var result = await _sut.GenerarReciboPdfAsync(999);

            // Assert
            Assert.Null(result);
        }

        #endregion
    }
}
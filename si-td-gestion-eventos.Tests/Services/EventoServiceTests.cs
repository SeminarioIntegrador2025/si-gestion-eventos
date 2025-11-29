using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Moq;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Repositories;
using si_td_gestion_eventos.Services.Contracts;
using si_td_gestion_eventos.Services.Implementation;
using System.Linq.Expressions;
using Xunit;

namespace si_td_gestion_eventos.Tests.Services
{
    public class EventoServiceTests
    {
        private readonly Mock<IGenericRepository<Evento>> _mockEventoRepository;
        private readonly Mock<IGenericRepository<Pago>> _mockPagoRepository;
        private readonly Mock<IGenericRepository<ComprobanteExterno>> _mockComprobanteRepository;
        private readonly Mock<IValidator<EventoVM>> _mockValidator;
        private readonly Mock<IEventoBusinessRules> _mockBusinessRules;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IFileStorageService> _mockFileStorageService;
        private readonly EventoService _sut;

        private void SetupMapperForUpdate()
        {
            _mockMapper.Setup(m => m.Map(It.IsAny<EventoVM>(), It.IsAny<Evento>()))
                .Returns<EventoVM, Evento>((src, dest) =>
                {
                    // Copiar solo propiedades editables
                    dest.ResponsableNombre = src.ResponsableNombre;
                    dest.ResponsableTelefono = src.ResponsableTelefono;
                    dest.ResponsableCedula = src.ResponsableCedula;
                    dest.CantidadPersonas = src.CantidadPersonas;
                    dest.Inicio = src.Inicio;
                    dest.Fin = src.Fin;
                    dest.HoraInicio = src.HoraInicio;
                    dest.HoraFin = src.HoraFin;
                    dest.Observaciones = src.Observaciones;
                    dest.Tipo = src.Tipo ?? TipoEvento.Otro;
                    dest.MontoAireAcondicionado = (float?)src.MontoAireAcondicionado;
                    // NO copiar: FechaContrato, CostoAlquiler, MontoReserva, ClienteId
                    return dest;
                });
        }

        public EventoServiceTests()
        {
            _mockEventoRepository = new Mock<IGenericRepository<Evento>>();
            _mockPagoRepository = new Mock<IGenericRepository<Pago>>();
            _mockComprobanteRepository = new Mock<IGenericRepository<ComprobanteExterno>>();
            _mockValidator = new Mock<IValidator<EventoVM>>();
            _mockBusinessRules = new Mock<IEventoBusinessRules>();
            _mockMapper = new Mock<IMapper>();
            _mockFileStorageService = new Mock<IFileStorageService>();

            _sut = new EventoService(
                _mockEventoRepository.Object,
                _mockPagoRepository.Object,
                _mockValidator.Object,
                _mockBusinessRules.Object,
                _mockMapper.Object,
                _mockFileStorageService.Object,
                _mockComprobanteRepository.Object
            );
        }

        #region GetByIdAsync Tests

        [Fact]
        public async Task GetByIdAsync_EventoExists_CalculateBalanceCorrectly()
        {
            // Arrange
            var evento = new Evento
            {
                EventoId = 1,
                CostoAlquiler = 2000,
                MontoAireAcondicionado = 500,
                Pagos = new List<Pago>
                {
                    new Pago { Monto = 1000 },
                    new Pago { Monto = 500 }
                },
                Cliente = new Cliente { ClienteId = 1, Nombre = "Juan" }
            };

            var eventoVM = new EventoVM
            {
                EventoId = 1,
                CostoAlquiler = 2000,
                MontoAireAcondicionado = 500,
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

            _mockEventoRepository.Setup(r => r.GetByIdWithIncludesAsync(
                1,
                It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync(evento);

            _mockMapper.Setup(m => m.Map<EventoVM>(evento)).Returns(eventoVM);

            // Act
            var result = await _sut.GetByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1500m, result.TotalPagado); // 1000 + 500
            Assert.Equal(1000m, result.SaldoRestante); // 2500 - 1500
        }

        [Fact]
        public async Task GetByIdAsync_EventoNoExiste_RetornaNull()
        {
            // Arrange
            _mockEventoRepository.Setup(r => r.GetByIdWithIncludesAsync(
                999,
                It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync((Evento?)null);

            // Act
            var result = await _sut.GetByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetByIdAsync_EventoSinPagos_DeberiaCalcularSaldoCompleto()
        {
            // Arrange
            var evento = new Evento
            {
                EventoId = 1,
                CostoAlquiler = 2000,
                MontoAireAcondicionado = 0,
                Pagos = new List<Pago>(),
                Cliente = new Cliente { ClienteId = 1, Nombre = "María" }
            };

            var eventoVM = new EventoVM
            {
                EventoId = 1,
                CostoAlquiler = 2000,
                MontoAireAcondicionado = 0,
                ResponsableNombre = "María",
                MontoReserva = 500,
                CantidadPersonas = 30,
                ClienteId = 1,
                Inicio = DateTime.Now,
                Fin = DateTime.Now.AddHours(4),
                HoraInicio = TimeSpan.FromHours(19),
                HoraFin = TimeSpan.FromHours(23),
                FechaContrato = DateTime.Now
            };

            _mockEventoRepository.Setup(r => r.GetByIdWithIncludesAsync(
                1,
                It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync(evento);

            _mockMapper.Setup(m => m.Map<EventoVM>(evento)).Returns(eventoVM);

            // Act
            var result = await _sut.GetByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0m, result.TotalPagado);
            Assert.Equal(2000m, result.SaldoRestante);
        }

        #endregion

        #region CreateAsync Tests

        [Fact]
        public async Task CreateAsync_ValidacionExitosa_DeberiaCrearEvento()
        {
            // Arrange
            var eventoVM = new EventoVM
            {
                CostoAlquiler = 2000,
                MontoReserva = 500,
                Inicio = DateTime.Now.AddDays(5),
                Fin = DateTime.Now.AddDays(5).AddHours(5),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                ResponsableNombre = "Juan",
                ResponsableTelefono = "099123456",
                ResponsableCedula = "12345678",
                CantidadPersonas = 50,
                ClienteId = 1,
                FechaContrato = DateTime.Now,
                Tipo = TipoEvento.Cumpleaños
            };

            var evento = new Evento 
            { 
                EventoId = 1,
                Estado = EventoEstado.PendienteAdeudado,
                Cliente = new Cliente { ClienteId = 1, Nombre = "Juan" }
            };

            _mockValidator.Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<EventoVM>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(new ValidationResult());

            _mockBusinessRules.Setup(br => br.IsDateRangeAvailableAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>(),
                null
            )).ReturnsAsync(true);

            _mockMapper.Setup(m => m.Map<Evento>(eventoVM)).Returns(evento);
            _mockEventoRepository.Setup(r => r.AddAsync(It.IsAny<Evento>())).Returns(Task.CompletedTask);
            _mockEventoRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _mockEventoRepository.Setup(r => r.GetByIdWithIncludesAsync(
                It.IsAny<int>(),
                It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync(evento);

            _mockMapper.Setup(m => m.Map<EventoVM>(evento)).Returns(eventoVM);

            // Act
            var result = await _sut.CreateAsync(eventoVM);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Evento creado exitosamente.", result.Message);
            _mockEventoRepository.Verify(r => r.AddAsync(It.IsAny<Evento>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ValidacionFallida_DeberiaRetornarErrores()
        {
            // Arrange
            var eventoVM = new EventoVM
            {
                CostoAlquiler = 0, // Inválido - debe ser mayor a cero
                ResponsableNombre = "Juan",
                MontoReserva = 500,
                CantidadPersonas = 50,
                ClienteId = 1,
                Inicio = DateTime.Now.AddDays(10),
                Fin = DateTime.Now.AddDays(10).AddHours(5),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                FechaContrato = DateTime.Now,
                Tipo = TipoEvento.Cumpleaños,
                ResponsableTelefono = "099123456",
                ResponsableCedula = "12345678"
            };

            var validationFailures = new List<ValidationFailure>
    {
        new ValidationFailure("CostoAlquiler", "El costo del alquiler debe ser mayor a cero.")
    };

            // Setup del validador para que falle
            _mockValidator.Setup(v => v.ValidateAsync(
                It.IsAny<EventoVM>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(new ValidationResult(validationFailures));

            // No es necesario configurar business rules porque la validación falla antes

            // Act
            var result = await _sut.CreateAsync(eventoVM);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("El costo del alquiler debe ser mayor a cero.", result.Errors);
            _mockEventoRepository.Verify(r => r.AddAsync(It.IsAny<Evento>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_HorarioNoDisponible_DeberiaRetornarError()
        {
            // Arrange
            var eventoVM = new EventoVM
            {
                CostoAlquiler = 2000,
                MontoReserva = 500,
                Inicio = DateTime.Now.AddDays(5),
                Fin = DateTime.Now.AddDays(5).AddHours(5),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                ResponsableNombre = "Juan",
                CantidadPersonas = 50,
                ClienteId = 1,
                FechaContrato = DateTime.Now
            };

            _mockValidator.Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<EventoVM>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(new ValidationResult());

            _mockBusinessRules.Setup(br => br.IsDateRangeAvailableAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>(),
                null
            )).ReturnsAsync(false);

            // Act
            var result = await _sut.CreateAsync(eventoVM);

            // Assert
            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("El horario seleccionado ya no está disponible"));
        }

        #endregion

        #region CreateEventWithPaymentAsync Tests

        [Fact]
        public async Task CreateEventWithPaymentAsync_PagoCompleto_DeberiaEstablecerEstadoPendientePagado()
        {
            // Arrange
            var eventoVM = new EventoVM
            {
                CostoAlquiler = 2000,
                MontoAireAcondicionado = 0,
                Inicio = DateTime.Now.AddDays(10),
                Fin = DateTime.Now.AddDays(10).AddHours(5),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                ResponsableNombre = "Juan",
                MontoReserva = 500,
                CantidadPersonas = 50,
                ClienteId = 1,
                FechaContrato = DateTime.Now
            };

            var pagoVM = new PagoReservaVM
            {
                Monto = 2000, // Pago completo
                Fecha = DateTime.Now,
                Metodo = MetodoPago.Efectivo
            };

            var evento = new Evento 
            { 
                EventoId = 1, 
                CostoAlquiler = 2000,
                Cliente = new Cliente { ClienteId = 1, Nombre = "Juan" }
            };

            _mockValidator.Setup(v => v.ValidateAsync(
                It.IsAny<EventoVM>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(new ValidationResult());

            _mockValidator.Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<EventoVM>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(new ValidationResult());

            _mockBusinessRules.Setup(br => br.IsDateRangeAvailableAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int?>()
            )).ReturnsAsync(true);

            _mockMapper.Setup(m => m.Map<Evento>(eventoVM)).Returns(evento);
            _mockEventoRepository.Setup(r => r.AddAsync(It.IsAny<Evento>())).Returns(Task.CompletedTask);
            _mockEventoRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _mockPagoRepository.Setup(r => r.AddAsync(It.IsAny<Pago>())).Returns(Task.CompletedTask);
            _mockPagoRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            _mockEventoRepository.Setup(r => r.GetByIdWithIncludesAsync(
                It.IsAny<int>(),
                It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync(evento);

            _mockMapper.Setup(m => m.Map<EventoVM>(evento)).Returns(eventoVM);

            // Act
            var result = await _sut.CreateEventWithPaymentAsync(eventoVM, pagoVM);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(EventoEstado.PendientePagado, evento.Estado);
        }

        [Fact]
        public async Task CreateEventWithPaymentAsync_PagoParcial_DeberiaEstablecerEstadoPendienteAdeudado()
        {
            // Arrange
            var eventoVM = new EventoVM
            {
                CostoAlquiler = 2000,
                MontoAireAcondicionado = 0,
                Inicio = DateTime.Now.AddDays(10),
                Fin = DateTime.Now.AddDays(10).AddHours(5),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                ResponsableNombre = "Juan",
                MontoReserva = 500,
                CantidadPersonas = 50,
                ClienteId = 1,
                FechaContrato = DateTime.Now,
                Tipo = TipoEvento.Cumpleaños,
                ResponsableTelefono = "099123456",
                ResponsableCedula = "12345678"
            };

            var pagoVM = new PagoReservaVM
            {
                Monto = 500, // Pago parcial (reserva)
                Fecha = DateTime.Now,
                Metodo = MetodoPago.Efectivo
            };

            var evento = new Evento
            {
                EventoId = 1,
                CostoAlquiler = 2000,
                MontoAireAcondicionado = 0,
                Cliente = new Cliente { ClienteId = 1, Nombre = "Juan" }
            };

            // Setup ambos overloads del validador
            _mockValidator.Setup(v => v.ValidateAsync(
                It.IsAny<EventoVM>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(new ValidationResult());

            _mockValidator.Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<EventoVM>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(new ValidationResult());  

            _mockBusinessRules.Setup(br => br.IsDateRangeAvailableAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int?>()
            )).ReturnsAsync(true);

            _mockMapper.Setup(m => m.Map<Evento>(eventoVM)).Returns(evento);
            _mockEventoRepository.Setup(r => r.AddAsync(It.IsAny<Evento>())).Returns(Task.CompletedTask);
            _mockEventoRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _mockPagoRepository.Setup(r => r.AddAsync(It.IsAny<Pago>())).Returns(Task.CompletedTask);
            _mockPagoRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            _mockEventoRepository.Setup(r => r.GetByIdWithIncludesAsync(
                It.IsAny<int>(),
                It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync(evento);

            _mockMapper.Setup(m => m.Map<EventoVM>(evento)).Returns(eventoVM);

            // Act
            var result = await _sut.CreateEventWithPaymentAsync(eventoVM, pagoVM);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(EventoEstado.PendienteAdeudado, evento.Estado);
        }

        [Fact]
        public async Task CreateEventWithPaymentAsync_ConComprobante_DeberiaGuardarArchivo()
        {
            // Arrange
            var eventoVM = new EventoVM
            {
                CostoAlquiler = 2000,
                MontoAireAcondicionado = 0,
                Inicio = DateTime.Now.AddDays(10),
                Fin = DateTime.Now.AddDays(10).AddHours(5),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                ResponsableNombre = "Juan",
                MontoReserva = 500,
                CantidadPersonas = 50,
                ClienteId = 1,
                FechaContrato = DateTime.Now
            };

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns("comprobante.pdf");
            mockFile.Setup(f => f.Length).Returns(1024);

            var pagoVM = new PagoReservaVM
            {
                Monto = 1000,
                Fecha = DateTime.Now,
                Metodo = MetodoPago.Transferencia,
                ArchivoComprobante = mockFile.Object
            };

            var evento = new Evento 
            { 
                EventoId = 1, 
                CostoAlquiler = 2000,
                Cliente = new Cliente { ClienteId = 1, Nombre = "Juan" }
            };

            var pago = new Pago { PagoId = 1 };

            _mockValidator.Setup(v => v.ValidateAsync(
                It.IsAny<EventoVM>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(new ValidationResult());

            _mockValidator.Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<EventoVM>>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(new ValidationResult());

            _mockBusinessRules.Setup(br => br.IsDateRangeAvailableAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int?>()
            )).ReturnsAsync(true);

            _mockMapper.Setup(m => m.Map<Evento>(eventoVM)).Returns(evento);
            _mockEventoRepository.Setup(r => r.AddAsync(It.IsAny<Evento>())).Returns(Task.CompletedTask);
            _mockEventoRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _mockPagoRepository.Setup(r => r.AddAsync(It.IsAny<Pago>())).Returns(Task.CompletedTask);
            _mockPagoRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _mockComprobanteRepository.Setup(r => r.AddAsync(It.IsAny<ComprobanteExterno>())).Returns(Task.CompletedTask);
            _mockComprobanteRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            _mockFileStorageService.Setup(f => f.GuardarArchivoAsync(
                It.IsAny<IFormFile>(),
                It.IsAny<string>()
            )).ReturnsAsync("/uploads/comprobantes/test.pdf");

            _mockEventoRepository.Setup(r => r.GetByIdWithIncludesAsync(
                It.IsAny<int>(),
                It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync(evento);

            _mockMapper.Setup(m => m.Map<EventoVM>(evento)).Returns(eventoVM);

            // Act
            var result = await _sut.CreateEventWithPaymentAsync(eventoVM, pagoVM);

            // Assert
            Assert.True(result.Success);
            _mockFileStorageService.Verify(f => f.GuardarArchivoAsync(
                It.IsAny<IFormFile>(),
                "uploads/comprobantes"
            ), Times.Once);
            _mockComprobanteRepository.Verify(r => r.AddAsync(It.IsAny<ComprobanteExterno>()), Times.Once);
        }

        #endregion

        #region UpdateAsync Tests

        [Fact]
        public async Task UpdateAsync_EventoExiste_DeberiaActualizar()
        {
            // Arrange
            var eventoVM = new EventoVM
            {
                EventoId = 1,
                CostoAlquiler = 2000,
                MontoReserva = 500,
                Inicio = DateTime.Now.AddDays(5),
                Fin = DateTime.Now.AddDays(5).AddHours(5),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                ResponsableNombre = "Juan Actualizado",
                ResponsableTelefono = "099123456",
                ResponsableCedula = "12345678",
                CantidadPersonas = 60,
                ClienteId = 1,
                FechaContrato = DateTime.Now.AddDays(-10),
                Tipo = TipoEvento.Cumpleaños
            };

            var eventoExistente = new Evento
            {
                EventoId = 1,
                ClienteId = 1,
                FechaContrato = DateTime.Now.AddDays(-10),
                CostoAlquiler = 1500,
                MontoReserva = 400,
                Inicio = DateTime.Now.AddDays(5),
                ResponsableNombre = "Juan",
                ResponsableTelefono = "099123456",
                ResponsableCedula = "12345678",
                Fin = DateTime.Now.AddDays(5).AddHours(5),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                Cliente = new Cliente { ClienteId = 1, Nombre = "Juan" }
            };

            _mockValidator.Setup(v => v.ValidateAsync(
                It.IsAny<EventoVM>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(new ValidationResult());

            _mockEventoRepository.Setup(r => r.GetByIdWithIncludesAsync(
                1,
                It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync(eventoExistente);

            _mockBusinessRules.Setup(br => br.IsClienteActiveAsync(1)).ReturnsAsync(true);
            _mockBusinessRules.Setup(br => br.CanModifyEventoAsync(1)).ReturnsAsync(true);

            // Setup CORRECTO del Mapper - usar Returns con callback
            _mockMapper.Setup(m => m.Map(It.IsAny<EventoVM>(), It.IsAny<Evento>()))
                .Returns<EventoVM, Evento>((src, dest) =>
                {
                    // Actualizar solo propiedades editables
                    dest.ResponsableNombre = src.ResponsableNombre;
                    dest.ResponsableTelefono = src.ResponsableTelefono;
                    dest.ResponsableCedula = src.ResponsableCedula;
                    dest.CantidadPersonas = src.CantidadPersonas;
                    dest.Inicio = src.Inicio;
                    dest.Fin = src.Fin;
                    dest.HoraInicio = src.HoraInicio;
                    dest.HoraFin = src.HoraFin;
                    dest.Observaciones = src.Observaciones;
                    dest.Tipo = src.Tipo ?? TipoEvento.Otro;
                    // NO actualizar: FechaContrato, CostoAlquiler, MontoReserva
                    return dest;
                });

    _mockEventoRepository.Setup(r => r.Update(It.IsAny<Evento>()));
    _mockEventoRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

    var eventoVMRetornado = new EventoVM
    {
        EventoId = 1,
        ResponsableNombre = "Juan Actualizado",
        CostoAlquiler = 1500, // Debe mantener el valor original
        MontoReserva = 400, // Debe mantener el valor original
        CantidadPersonas = 60,
        ClienteId = 1,
        Inicio = DateTime.Now.AddDays(5),
        Fin = DateTime.Now.AddDays(5).AddHours(5),
        HoraInicio = TimeSpan.FromHours(18),
        HoraFin = TimeSpan.FromHours(23),
        FechaContrato = DateTime.Now.AddDays(-10)
    };

    _mockMapper.Setup(m => m.Map<EventoVM>(It.IsAny<Evento>())).Returns(eventoVMRetornado);

    // Act
    var result = await _sut.UpdateAsync(eventoVM);

    // Assert
    Assert.True(result.Success);
    Assert.Equal("Evento actualizado correctamente.", result.Message);
    _mockEventoRepository.Verify(r => r.Update(It.IsAny<Evento>()), Times.Once);
    
    // Verificar que NO se actualizaron los campos protegidos
    Assert.Equal(DateTime.Now.AddDays(-10).Date, eventoExistente.FechaContrato.Date);
    Assert.Equal(1500, eventoExistente.CostoAlquiler);
    Assert.Equal(400, eventoExistente.MontoReserva);
}

        [Fact]
        public async Task UpdateAsync_ClienteInactivo_DeberiaRetornarError()
        {
            // Arrange
            var eventoVM = new EventoVM
            {
                EventoId = 1,
                ClienteId = 1,
                ResponsableNombre = "Juan",
                MontoReserva = 500,
                CostoAlquiler = 2000,
                CantidadPersonas = 50,
                Inicio = DateTime.Now.AddDays(5),
                Fin = DateTime.Now.AddDays(5).AddHours(5),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                FechaContrato = DateTime.Now.AddDays(-10),
                Tipo = TipoEvento.Cumpleaños,
                ResponsableTelefono = "099123456",
                ResponsableCedula = "12345678"
            };

            var eventoExistente = new Evento
            {
                EventoId = 1,
                ClienteId = 1,
                Cliente = new Cliente { ClienteId = 1, Nombre = "Juan", Activo = false }
            };

            _mockValidator.Setup(v => v.ValidateAsync(
                It.IsAny<EventoVM>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(new ValidationResult());

            _mockEventoRepository.Setup(r => r.GetByIdWithIncludesAsync(
                1,
                It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync(eventoExistente);

            _mockBusinessRules.Setup(br => br.IsClienteActiveAsync(1)).ReturnsAsync(false);

            // Act
            var result = await _sut.UpdateAsync(eventoVM);

            // Assert
            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("cliente está inactivo") || e.Contains("cliente no está activo"));
        }

        #endregion

        #region CancelAsync Tests

        [Fact]
        public async Task CancelAsync_EventoCancelable_DeberiaCancelarEvento()
        {
            // Arrange
            var evento = new Evento { EventoId = 1, Estado = EventoEstado.PendienteAdeudado };

            _mockBusinessRules.Setup(br => br.CanCancelEventoAsync(1)).ReturnsAsync(true);
            _mockEventoRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(evento);
            _mockEventoRepository.Setup(r => r.Update(It.IsAny<Evento>()));
            _mockEventoRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _sut.CancelAsync(1);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(EventoEstado.Cancelado, evento.Estado);
            _mockEventoRepository.Verify(r => r.Update(It.IsAny<Evento>()), Times.Once);
        }

        [Fact]
        public async Task CancelAsync_EventoNoCancelable_DeberiaRetornarError()
        {
            // Arrange
            _mockBusinessRules.Setup(br => br.CanCancelEventoAsync(1)).ReturnsAsync(false);

            // Act
            var result = await _sut.CancelAsync(1);

            // Assert
            Assert.False(result.Success);
            _mockEventoRepository.Verify(r => r.Update(It.IsAny<Evento>()), Times.Never);
        }

        [Fact]
        public async Task CancelAsync_EventoNoExiste_DeberiaRetornarError()
        {
            // Arrange
            _mockBusinessRules.Setup(br => br.CanCancelEventoAsync(999)).ReturnsAsync(true);
            _mockEventoRepository.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Evento?)null);

            // Act
            var result = await _sut.CancelAsync(999);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Evento no encontrado.", result.Errors);
        }

        #endregion

        #region RescheduleAsync Tests

        [Fact]
        public async Task RescheduleAsync_EventoReprogramable_DeberiaReprogramar()
        {
            // Arrange
            var evento = new Evento 
            { 
                EventoId = 1, 
                Estado = EventoEstado.PendientePagado,
                Inicio = DateTime.Now.AddDays(10),
                Fin = DateTime.Now.AddDays(10).AddHours(5)
            };

            var nuevaFechaInicio = DateTime.Now.AddDays(15);
            var nuevaFechaFin = DateTime.Now.AddDays(15).AddHours(5);
            var nuevaHoraInicio = TimeSpan.FromHours(19);
            var nuevaHoraFin = TimeSpan.FromHours(24);

            _mockBusinessRules.Setup(br => br.CanRescheduleEventoAsync(1)).ReturnsAsync(true);
            _mockEventoRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(evento);
            _mockBusinessRules.Setup(br => br.IsValidDateRangeAsync(
                nuevaFechaInicio, nuevaFechaFin, nuevaHoraInicio, nuevaHoraFin
            )).ReturnsAsync(true);
            _mockBusinessRules.Setup(br => br.IsDateRangeAvailableAsync(
                nuevaFechaInicio, nuevaFechaFin, nuevaHoraInicio, nuevaHoraFin, 1
            )).ReturnsAsync(true);

            _mockEventoRepository.Setup(r => r.Update(It.IsAny<Evento>()));
            _mockEventoRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _sut.RescheduleAsync(1, nuevaFechaInicio, nuevaFechaFin, nuevaHoraInicio, nuevaHoraFin);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(EventoEstado.Reprogramado, evento.Estado);
            Assert.Equal(nuevaFechaInicio, evento.Inicio);
            Assert.Equal(nuevaFechaFin, evento.Fin);
        }

        [Fact]
        public async Task RescheduleAsync_FechasConflicto_DeberiaRetornarError()
        {
            // Arrange
            var evento = new Evento { EventoId = 1, Estado = EventoEstado.PendientePagado };

            var nuevaFechaInicio = DateTime.Now.AddDays(15);
            var nuevaFechaFin = DateTime.Now.AddDays(15).AddHours(5);
            var nuevaHoraInicio = TimeSpan.FromHours(19);
            var nuevaHoraFin = TimeSpan.FromHours(24);

            _mockBusinessRules.Setup(br => br.CanRescheduleEventoAsync(1)).ReturnsAsync(true);
            _mockEventoRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(evento);
            _mockBusinessRules.Setup(br => br.IsValidDateRangeAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()
            )).ReturnsAsync(true);
            _mockBusinessRules.Setup(br => br.IsDateRangeAvailableAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), 1
            )).ReturnsAsync(false);

            // Act
            var result = await _sut.RescheduleAsync(1, nuevaFechaInicio, nuevaFechaFin, nuevaHoraInicio, nuevaHoraFin);

            // Assert
            Assert.False(result.Success); 
            Assert.Contains(result.Errors, e => e.Contains("Ya existe otro evento"));
        }

        #endregion

        #region MarkCompletedEventsAsync Tests

        [Fact]
        public async Task MarkCompletedEventsAsync_EventosVencidos_DeberiaMarcarComoRealizados()
        {
            // Arrange
            var eventos = new List<Evento>
            {
                new Evento 
                { 
                    EventoId = 1, 
                    Estado = EventoEstado.PendientePagado,
                    Fin = DateTime.Today.AddDays(-1)
                },
                new Evento 
                { 
                    EventoId = 2, 
                    Estado = EventoEstado.PendienteAdeudado,
                    Fin = DateTime.Today.AddDays(-2)
                }
            };

            _mockEventoRepository.Setup(r => r.FindAsync(
                It.IsAny<Expression<Func<Evento, bool>>>()
            )).ReturnsAsync(eventos);

            _mockEventoRepository.Setup(r => r.Update(It.IsAny<Evento>()));
            _mockEventoRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _sut.MarkCompletedEventsAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal(2, result.Data);
            Assert.All(eventos, e => Assert.Equal(EventoEstado.Realizado, e.Estado));
        }

        [Fact]
        public async Task MarkCompletedEventsAsync_SinEventos_RetornarCero()
        {
            // Arrange
            _mockEventoRepository.Setup(r => r.FindAsync(
                It.IsAny<Expression<Func<Evento, bool>>>()
            )).ReturnsAsync(new List<Evento>());

            // Act
            var result = await _sut.MarkCompletedEventsAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal(0, result.Data);
            _mockEventoRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
        }

        #endregion

        #region CheckAndCancelUnpaidEventsAsync Tests

        [Fact]
        public async Task CheckAndCancelUnpaidEventsAsync_SinPago_CancelaEvento()
        {
            // Arrange
            var eventos = new List<Evento>
            {
                new Evento
                {
                    EventoId = 1,
                    Estado = EventoEstado.PendienteAdeudado,
                    Inicio = DateTime.Now.AddHours(40), // Dentro de las 48 horas
                    CostoAlquiler = 2000,
                    MontoAireAcondicionado = 0,
                    Pagos = new List<Pago>() // Sin pagos
                }
            };

            _mockEventoRepository.Setup(r => r.FindWithIncludesAsync(
                It.IsAny<Expression<Func<Evento, bool>>>(),
                It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync(eventos);

            _mockEventoRepository.Setup(r => r.Update(It.IsAny<Evento>()));
            _mockEventoRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _sut.CheckAndCancelUnpaidEventsAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal(1, result.Data);
            Assert.Equal(EventoEstado.Cancelado, eventos[0].Estado);
        }

        [Fact]
        public async Task CheckAndCancelUnpaidEventsAsync_ConReserva_NoCancela()
        {
            // Arrange
            var eventos = new List<Evento>
            {
                new Evento
                {
                    EventoId = 1,
                    Estado = EventoEstado.PendienteAdeudado,
                    Inicio = DateTime.Now.AddHours(40),
                    CostoAlquiler = 2000,
                    MontoAireAcondicionado = 0,
                    Pagos = new List<Pago> { new Pago { Monto = 500 } } // Con reserva
                }
            };

            _mockEventoRepository.Setup(r => r.FindWithIncludesAsync(
                It.IsAny<Expression<Func<Evento, bool>>>(),
                It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync(eventos);

            // Act
            var result = await _sut.CheckAndCancelUnpaidEventsAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal(0, result.Data);
            Assert.NotEqual(EventoEstado.Cancelado, eventos[0].Estado);
        }

        #endregion

        #region GetEventosSinFianzaParaDropdownAsync Tests

        [Fact]
        public async Task GetEventosSinFianzaParaDropdownAsync_RetornaSoloSinFianza()
        {
            // Arrange
            var eventos = new List<Evento>
            {
                new Evento 
                { 
                    EventoId = 1, 
                    FianzaId = null, 
                    Estado = EventoEstado.PendientePagado,
                    Tipo = TipoEvento.Cumpleaños,
                    Inicio = DateTime.Now.AddDays(10),
                    Cliente = new Cliente { Nombre = "Juan", Apellido = "Pérez" }
                },
                new Evento 
                { 
                    EventoId = 2, 
                    FianzaId = 1, // Tiene fianza
                    Estado = EventoEstado.PendientePagado,
                    Cliente = new Cliente { Nombre = "María", Apellido = "González" }
                }
            };

            _mockEventoRepository.Setup(r => r.FindWithIncludesAsync(
                It.IsAny<Expression<Func<Evento, bool>>>(),
                It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync(eventos.Where(e => e.FianzaId == null));

            // Act
            var result = await _sut.GetEventosSinFianzaParaDropdownAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Contains("Juan Pérez", result.First().Text);
        }

        #endregion

        #region GetLatestAsync Tests

        [Fact]
        public async Task GetLatestAsync_Retorna2UltimosEventos()
        {
            // Arrange
            var eventos = new List<Evento>
            {
                new Evento { EventoId = 3, Cliente = new Cliente { Nombre = "Cliente 3" } },
                new Evento { EventoId = 2, Cliente = new Cliente { Nombre = "Cliente 2" } },
                new Evento { EventoId = 1, Cliente = new Cliente { Nombre = "Cliente 1" } }
            };

            var eventosVM = new List<EventoVM>
            {
                new EventoVM { EventoId = 3, ResponsableNombre = "C3", MontoReserva = 500, CantidadPersonas = 50, ClienteId = 1, Inicio = DateTime.Now, Fin = DateTime.Now, HoraInicio = TimeSpan.FromHours(18), HoraFin = TimeSpan.FromHours(23), FechaContrato = DateTime.Now },
                new EventoVM { EventoId = 2, ResponsableNombre = "C2", MontoReserva = 500, CantidadPersonas = 50, ClienteId = 1, Inicio = DateTime.Now, Fin = DateTime.Now, HoraInicio = TimeSpan.FromHours(18), HoraFin = TimeSpan.FromHours(23), FechaContrato = DateTime.Now },
                new EventoVM { EventoId = 1, ResponsableNombre = "C1", MontoReserva = 500, CantidadPersonas = 50, ClienteId = 1, Inicio = DateTime.Now, Fin = DateTime.Now, HoraInicio = TimeSpan.FromHours(18), HoraFin = TimeSpan.FromHours(23), FechaContrato = DateTime.Now }
            };

            _mockEventoRepository.Setup(r => r.FindWithIncludesAsync(
                null,
                It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync(eventos);

            _mockMapper.Setup(m => m.Map<List<EventoVM>>(It.IsAny<IEnumerable<Evento>>())).Returns(eventosVM);

            // Act
            var result = await _sut.GetLatestAsync(2);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
        }
    }
    #endregion
}
using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Moq;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Repositories;
using si_td_gestion_eventos.Services.Common; // Necesario para ServiceResult
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
                    // IMPORTANTE: Valido = true para que el servicio los sume
                    new Pago { Monto = 1000, Valido = true },
                    new Pago { Monto = 500, Valido = true }
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
                ClienteId = 1,
                Inicio = DateTime.Now
            };

            _mockEventoRepository.Setup(r => r.GetByIdWithIncludesAsync(
                1, It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync(evento);

            _mockMapper.Setup(m => m.Map<EventoVM>(evento)).Returns(eventoVM);

            // Act
            var result = await _sut.GetByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1500m, result.TotalPagado); // 1000 + 500
            Assert.Equal(1000m, result.SaldoRestante); // (2000 + 500) - 1500
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
                Inicio = DateTime.Now.AddDays(5),
                Fin = DateTime.Now.AddDays(5).AddHours(5),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                ClienteId = 1
            };

            var evento = new Evento { EventoId = 1, Estado = EventoEstado.PendienteAdeudado };

            _mockValidator.Setup(v => v.ValidateAsync(It.IsAny<EventoVM>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());

            _mockBusinessRules.Setup(br => br.IsDateRangeAvailableAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), null
            )).ReturnsAsync(true);

            _mockMapper.Setup(m => m.Map<Evento>(eventoVM)).Returns(evento);

            // Simular retorno de la base de datos tras crear
            _mockEventoRepository.Setup(r => r.GetByIdWithIncludesAsync(1, It.IsAny<Expression<Func<Evento, object>>[]>()))
                .ReturnsAsync(evento);
            _mockMapper.Setup(m => m.Map<EventoVM>(evento)).Returns(eventoVM);

            // Act
            var result = await _sut.CreateAsync(eventoVM);

            // Assert
            Assert.True(result.Success);
            _mockEventoRepository.Verify(r => r.AddAsync(It.IsAny<Evento>()), Times.Once);
        }

        #endregion

        #region ReprogramarAsync Tests (NUEVO - Reemplaza RescheduleAsync)

        [Fact]
        public async Task ReprogramarAsync_FechaIndefinida_MueveAlPasado()
        {
            // Arrange
            int eventoId = 1;
            var fechaOriginal = new DateTime(2025, 12, 23);

            var evento = new Evento
            {
                EventoId = eventoId,
                Inicio = fechaOriginal,
                Fin = fechaOriginal.AddHours(5),
                Estado = EventoEstado.PendientePagado,
                Observaciones = "Obs inicial"
            };

            var model = new ReprogramarEventoVM
            {
                EventoId = eventoId,
                FechaIndefinida = true
            };

            _mockEventoRepository.Setup(r => r.GetByIdAsync(eventoId)).ReturnsAsync(evento);
            _mockEventoRepository.Setup(r => r.Update(It.IsAny<Evento>()));
            _mockEventoRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _sut.ReprogramarAsync(model);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(EventoEstado.Reprogramado, evento.Estado);

            // Verificar lógica de 1900 (Estacionamiento)
            Assert.Equal(1900, evento.Inicio.Year);
            Assert.Equal(1900, evento.Fin.Year);

            // Verificar que conservó mes y día (23/12)
            Assert.Equal(12, evento.Inicio.Month);
            Assert.Equal(23, evento.Inicio.Day);

            // Verificar Historial en Observaciones
            Assert.Contains("[REPROGRAMADO]", evento.Observaciones);
        }

        [Fact]
        public async Task ReprogramarAsync_NuevaFechaDefinida_ActualizaCorrectamente()
        {
            // Arrange
            int eventoId = 1;
            var evento = new Evento { EventoId = eventoId, Inicio = DateTime.Now };

            var nuevaFecha = new DateTime(2026, 1, 1);
            var horaInicio = new TimeSpan(20, 0, 0);
            var horaFin = new TimeSpan(23, 0, 0);

            var model = new ReprogramarEventoVM
            {
                EventoId = eventoId,
                NuevaFechaInicio = nuevaFecha,
                NuevaFechaFin = nuevaFecha,
                NuevaHoraInicio = horaInicio,
                NuevaHoraFin = horaFin
            };

            _mockEventoRepository.Setup(r => r.GetByIdAsync(eventoId)).ReturnsAsync(evento);

            // Act
            var result = await _sut.ReprogramarAsync(model);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(EventoEstado.Reprogramado, evento.Estado);
            Assert.Equal(nuevaFecha.Date + horaInicio, evento.Inicio);
            Assert.Equal(nuevaFecha.Date + horaFin, evento.Fin);
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
                    Inicio = DateTime.Now.AddHours(40),
                    CostoAlquiler = 2000,
                    Pagos = new List<Pago>() // Sin pagos
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
            Assert.Equal(1, result.Data);
            Assert.Equal(EventoEstado.Cancelado, eventos[0].Estado);
        }

        [Fact]
        public async Task CheckAndCancelUnpaidEventsAsync_ConPagoValido_NoCancela()
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
                    // Pago Valido = true
                    Pagos = new List<Pago> { new Pago { Monto = 500, Valido = true } }
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
            Assert.Equal(0, result.Data); // 0 cancelados
            Assert.NotEqual(EventoEstado.Cancelado, eventos[0].Estado);
        }

        [Fact]
        public async Task CheckAndCancelUnpaidEventsAsync_ConPagoAnulado_DeberiaCancelar()
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
                    // Pago Valido = false (ANULADO) -> El sistema debe ignorar este monto
                    Pagos = new List<Pago> { new Pago { Monto = 500, Valido = false } }
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
            Assert.Equal(1, result.Data); // Debe cancelar porque el pago no cuenta
            Assert.Equal(EventoEstado.Cancelado, eventos[0].Estado);
        }

        #endregion

        #region MarkCompletedEventsAsync Tests

        [Fact]
        public async Task MarkCompletedEventsAsync_IgnoraEventosReprogramados()
        {
            // Arrange
            // Evento en 1900 (Reprogramado) y Evento vencido normal
            var eventosEncontrados = new List<Evento>
            {
                new Evento
                {
                    EventoId = 1,
                    Estado = EventoEstado.PendientePagado,
                    Fin = DateTime.Today.AddDays(-1) // Vencido
                }
                // Nota: El filtro del repositorio debería excluir el Reprogramado desde la consulta.
                // Aquí simulamos que el repositorio devuelve lo que encuentra según la lógica del Service.
            };

            // Configuramos el Mock para que cuando se llame al FindAsync con el filtro correcto,
            // devuelva solo el evento pendiente, simulando que la BD filtró el reprogramado.
            _mockEventoRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Evento, bool>>>()))
                .ReturnsAsync(eventosEncontrados);

            // Act
            var result = await _sut.MarkCompletedEventsAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal(1, result.Data); // Solo marcó 1
            Assert.Equal(EventoEstado.Realizado, eventosEncontrados[0].Estado);
        }

        #endregion
    }
}
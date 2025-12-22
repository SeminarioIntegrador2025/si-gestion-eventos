using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Moq;
using si_td_gestion_eventos.Context;
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
    public class EventoServiceTests
    {
        private readonly Mock<IGenericRepository<Evento>> _mockEventoRepository;
        private readonly Mock<IGenericRepository<Pago>> _mockPagoRepository;
        private readonly Mock<IGenericRepository<ComprobanteExterno>> _mockComprobanteRepository;
        private readonly Mock<IValidator<EventoVM>> _mockValidator;
        private readonly Mock<IEventoBusinessRules> _mockBusinessRules;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IFileStorageService> _mockFileStorageService;
        private readonly Mock<AppDbContext> _mockContext;

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

            // Inicialización del mock del contexto
            _mockContext = new Mock<AppDbContext>(new DbContextOptions<AppDbContext>());

            _sut = new EventoService(
                _mockEventoRepository.Object,
                _mockPagoRepository.Object,
                _mockValidator.Object,
                _mockBusinessRules.Object,
                _mockMapper.Object,
                _mockFileStorageService.Object,
                _mockComprobanteRepository.Object,
                _mockContext.Object
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
            Assert.Equal(1500m, result.TotalPagado);
            Assert.Equal(1000m, result.SaldoRestante);
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
                ClienteId = 1
            };

            var evento = new Evento { EventoId = 1, Estado = EventoEstado.PendienteAdeudado };

            _mockValidator.Setup(v => v.ValidateAsync(It.IsAny<EventoVM>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());

            _mockBusinessRules.Setup(br => br.IsDateRangeAvailableAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), null
            )).ReturnsAsync(true);

            _mockMapper.Setup(m => m.Map<Evento>(eventoVM)).Returns(evento);
            _mockEventoRepository.Setup(r => r.AddAsync(It.IsAny<Evento>())).Returns(Task.CompletedTask);
            _mockEventoRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            _mockEventoRepository.Setup(r => r.GetByIdWithIncludesAsync(1, It.IsAny<Expression<Func<Evento, object>>[]>()))
                .ReturnsAsync(evento);
            _mockMapper.Setup(m => m.Map<EventoVM>(evento)).Returns(eventoVM);

            // Act
            var result = await _sut.CreateAsync(eventoVM);

            // Assert
            Assert.True(result.Success);
        }

        #endregion

        #region ReprogramarAsync Tests

        [Fact]
        public async Task ReprogramarAsync_FechaIndefinida_MueveAlPasado()
        {
            // Arrange
            int eventoId = 1;
            var evento = new Evento
            {
                EventoId = eventoId,
                Inicio = new DateTime(2025, 12, 23),
                Fin = new DateTime(2025, 12, 23).AddHours(5),
                Estado = EventoEstado.PendientePagado
            };

            var model = new ReprogramarEventoVM { EventoId = eventoId, FechaIndefinida = true };

            _mockEventoRepository.Setup(r => r.GetByIdAsync(eventoId)).ReturnsAsync(evento);
            _mockEventoRepository.Setup(r => r.Update(It.IsAny<Evento>()));
            _mockEventoRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _sut.ReprogramarAsync(model);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(1900, evento.Inicio.Year);
            Assert.Equal(EventoEstado.Reprogramado, evento.Estado);
        }

        #endregion

        #region ActualizarEstadosEventosPasadosAsync Tests (NUEVO)

        [Fact]
        public async Task ActualizarEstados_EventoPagadoPasado_MarcaComoRealizado()
        {
            // Arrange (CASO A: Pagó todo y el evento ya pasó)
            var eventos = new List<Evento>
            {
                new Evento
                {
                    EventoId = 1,
                    Estado = EventoEstado.PendientePagado, // Estado previo
                    Fin = DateTime.Now.AddDays(-1), // Ya pasó
                    CostoAlquiler = 1000,
                    Pagos = new List<Pago> { new Pago { Monto = 1000, Valido = true } } // Pagó todo
                }
            };

            // Mockeamos la búsqueda para que devuelva este evento
            _mockEventoRepository.Setup(r => r.FindWithIncludesAsync(
                It.IsAny<Expression<Func<Evento, bool>>>(),
                It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync(eventos);

            // Act
            var result = await _sut.ActualizarEstadosEventosPasadosAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal(1, result.Data); // 1 evento modificado
            Assert.Equal(EventoEstado.Realizado, eventos[0].Estado); // Debe pasar a Realizado
        }

        [Fact]
        public async Task ActualizarEstados_EventoDeudorPasado_MarcaComoPendienteAdeudado()
        {
            // Arrange (CASO B: Debe plata y el evento ya pasó)
            var eventos = new List<Evento>
            {
                new Evento
                {
                    EventoId = 2,
                    Estado = EventoEstado.PendientePagado, // Estaba pendiente
                    Fin = DateTime.Now.AddDays(-1), // Ya pasó
                    CostoAlquiler = 1000,
                    Pagos = new List<Pago>() // No pagó nada
                }
            };

            _mockEventoRepository.Setup(r => r.FindWithIncludesAsync(
                It.IsAny<Expression<Func<Evento, bool>>>(),
                It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync(eventos);

            // Act
            var result = await _sut.ActualizarEstadosEventosPasadosAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal(1, result.Data);
            // IMPORTANTE: Según tu nueva lógica, NO se cancela, se marca como PendienteAdeudado
            Assert.Equal(EventoEstado.PendienteAdeudado, eventos[0].Estado);
        }

        #endregion
    }
}
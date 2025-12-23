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
                ClienteId = 1,
                Fin = DateTime.Now.AddDays(5).AddHours(5),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                CantidadPersonas = 50,
                ResponsableNombre = "Juan Pérez"
            };

            var cliente = new Cliente 
            { 
                ClienteId = 1, 
                Nombre = "Juan",
                Apellido = "Pérez",
                CedulaIdentidad = "12345678"
            };

            var evento = new Evento 
            { 
                EventoId = 0, // Inicialmente sin ID (simula antes de guardar)
                Estado = EventoEstado.PendienteAdeudado,
                CostoAlquiler = 2000,
                ClienteId = 1,
                Cliente = cliente,
                Inicio = DateTime.Now.AddDays(5),
                Fin = DateTime.Now.AddDays(5).AddHours(5),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23)
            };

            // CORRECCIÓN CRÍTICA: Mock correcto para ValidateAsync con opciones
            _mockValidator.Setup(v => v.ValidateAsync(
                It.IsAny<IValidationContext>(), 
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(new ValidationResult());

            // Configuración de reglas de negocio
            _mockBusinessRules.Setup(br => br.IsDateRangeAvailableAsync(
                It.IsAny<DateTime>(), 
                It.IsAny<DateTime>(), 
                It.IsAny<TimeSpan>(), 
                It.IsAny<TimeSpan>(), 
                null
            )).ReturnsAsync(true);

            // Configuración del mapper: EventoVM -> Evento
            _mockMapper.Setup(m => m.Map<Evento>(It.IsAny<EventoVM>()))
                .Returns(evento);

            // Simular que AddAsync asigna el ID
            _mockEventoRepository.Setup(r => r.AddAsync(It.IsAny<Evento>()))
                .Callback<Evento>(e => e.EventoId = 1) // Simula la asignación del ID por la BD
                .Returns(Task.CompletedTask);

            _mockEventoRepository.Setup(r => r.SaveChangesAsync())
                .Returns(Task.CompletedTask);

            // GetByIdWithIncludesAsync devolverá el evento con ID=1
            _mockEventoRepository.Setup(r => r.GetByIdWithIncludesAsync(
                1, // ID específico que se asignó en el Callback
                It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync((int id, Expression<Func<Evento, object>>[] includes) => 
    {
        // Actualizamos el evento para reflejar el estado después de guardar
        evento.EventoId = id;
        return evento;
    });

            // Configuración del mapper: Evento -> EventoVM
            _mockMapper.Setup(m => m.Map<EventoVM>(It.IsAny<Evento>()))
                .Returns((Evento e) => new EventoVM 
                { 
                    EventoId = e.EventoId, 
                    CostoAlquiler = (decimal)e.CostoAlquiler,
                    ClienteId = e.Cliente?.ClienteId ?? 0,
                    ResponsableNombre = e.Cliente?.Nombre ?? "N/A",
                    Inicio = e.Inicio,
                    Fin = e.Fin,
                    HoraInicio = e.HoraInicio,
                    HoraFin = e.HoraFin,
                    Estado = e.Estado
                });

            // Act
            var result = await _sut.CreateAsync(eventoVM);

            // Assert
            Assert.True(result.Success, $"Expected success but got: {string.Join(", ", result.Errors)}");
            Assert.NotNull(result.Data);
            Assert.Equal(1, result.Data.EventoId);
            Assert.Equal(EventoEstado.PendienteAdeudado, result.Data.Estado);
            Assert.Equal("Evento creado exitosamente.", result.Message);
            
            // Verificaciones adicionales
            _mockEventoRepository.Verify(r => r.AddAsync(It.IsAny<Evento>()), Times.Once);
            _mockEventoRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
            _mockEventoRepository.Verify(r => r.GetByIdWithIncludesAsync(1, It.IsAny<Expression<Func<Evento, object>>[]>()), Times.Once);
            _mockMapper.Verify(m => m.Map<Evento>(It.IsAny<EventoVM>()), Times.Once);
            _mockMapper.Verify(m => m.Map<EventoVM>(It.IsAny<Evento>()), Times.Once);
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
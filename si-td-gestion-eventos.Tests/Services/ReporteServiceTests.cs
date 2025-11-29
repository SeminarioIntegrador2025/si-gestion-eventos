using Moq;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Repositories;
using si_td_gestion_eventos.Services.Implementation;
using System.Linq.Expressions;
using Xunit;

namespace si_td_gestion_eventos.Tests.Services
{
    public class ReporteServiceTests
    {
        private readonly Mock<IGenericRepository<Evento>> _mockEventoRepository;
        private readonly Mock<IGenericRepository<Pago>> _mockPagoRepository;
        private readonly Mock<IGenericRepository<Fianza>> _mockFianzaRepository;
        private readonly Mock<IGenericRepository<Cliente>> _mockClienteRepository;
        private readonly ReporteService _sut;

        public ReporteServiceTests()
        {
            _mockEventoRepository = new Mock<IGenericRepository<Evento>>();
            _mockPagoRepository = new Mock<IGenericRepository<Pago>>();
            _mockFianzaRepository = new Mock<IGenericRepository<Fianza>>();
            _mockClienteRepository = new Mock<IGenericRepository<Cliente>>();

            _sut = new ReporteService(
                _mockEventoRepository.Object,
                _mockPagoRepository.Object,
                _mockFianzaRepository.Object,
                _mockClienteRepository.Object
            );
        }

        #region GetReportesConsolidadosAsync Tests

        [Fact]
        public async Task GetReportesConsolidadosAsync_DeberiaRetornarTodosLosReportes()
        {
            // Arrange
            SetupMocksConDatosBase();

            // Act
            var result = await _sut.GetReportesConsolidadosAsync();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Eventos);
            Assert.NotNull(result.Ingresos);
            Assert.NotNull(result.Pagos);
            Assert.NotNull(result.Fianzas);
            Assert.NotNull(result.Clientes);
            Assert.NotNull(result.TopClientes);
            Assert.NotNull(result.EventosPorTipo);
        }

        #endregion

        #region GetReporteEventosAsync Tests

        [Fact]
        public async Task GetReporteEventosAsync_ConEventosEsteMes_DeberiaCalcularEstadisticasCorrectamente()
        {
            // Arrange
            var hoy = DateTime.Today;
            var inicioMesActual = new DateTime(hoy.Year, hoy.Month, 1);
            var finMesActual = inicioMesActual.AddMonths(1).AddDays(-1);

            var eventosEsteMes = new List<Evento>
            {
                new Evento { EventoId = 1, Inicio = inicioMesActual.AddDays(5), Fin = inicioMesActual.AddDays(5), Estado = EventoEstado.Realizado },
                new Evento { EventoId = 2, Inicio = inicioMesActual.AddDays(10), Fin = inicioMesActual.AddDays(10), Estado = EventoEstado.PendientePagado },
                new Evento { EventoId = 3, Inicio = inicioMesActual.AddDays(15), Fin = inicioMesActual.AddDays(15), Estado = EventoEstado.Cancelado }
            };

            _mockEventoRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Evento, bool>>>()))
                .ReturnsAsync((Expression<Func<Evento, bool>> predicate) =>
                {
                    var compiledPredicate = predicate.Compile();
                    return eventosEsteMes.Where(compiledPredicate).ToList();
                });

            // Act
            var result = await _sut.GetReporteEventosAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.EventosEsteMes);
            Assert.Equal(1, result.EventosRealizadosEsteMes);
            Assert.Equal(1, result.EventosPendientesEsteMes);
            Assert.Equal(1, result.EventosCanceladosEsteMes);
        }

        [Fact]
        public async Task GetReporteEventosAsync_SinEventos_DeberiaRetornarCeros()
        {
            // Arrange
            _mockEventoRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Evento, bool>>>()))
                .ReturnsAsync(new List<Evento>());

            // Act
            var result = await _sut.GetReporteEventosAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.EventosEsteMes);
            Assert.Equal(0, result.EventosRealizadosEsteMes);
            Assert.Equal(0, result.EventosPendientesEsteMes);
            Assert.Equal(0, result.EventosCanceladosEsteMes);
        }

        [Fact]
        public async Task GetReporteEventosAsync_ConCrecimiento_DeberiaTenerPorcentajePositivo()
        {
            // Arrange
            var hoy = DateTime.Today;
            var inicioMesActual = new DateTime(hoy.Year, hoy.Month, 1);
            var inicioMesAnterior = inicioMesActual.AddMonths(-1);
            var finMesAnterior = inicioMesActual.AddDays(-1);

            var eventosEsteMes = new List<Evento>
            {
                new Evento { EventoId = 1, Inicio = inicioMesActual.AddDays(5), Fin = inicioMesActual.AddDays(5), Estado = EventoEstado.PendientePagado },
                new Evento { EventoId = 2, Inicio = inicioMesActual.AddDays(10), Fin = inicioMesActual.AddDays(10), Estado = EventoEstado.PendientePagado }
            };

            var eventosMesAnterior = new List<Evento>
            {
                new Evento { EventoId = 3, Inicio = inicioMesAnterior.AddDays(5), Fin = inicioMesAnterior.AddDays(5), Estado = EventoEstado.Realizado }
            };

            _mockEventoRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Evento, bool>>>()))
                .ReturnsAsync((Expression<Func<Evento, bool>> predicate) =>
                {
                    var compiledPredicate = predicate.Compile();
                    var allEventos = eventosEsteMes.Concat(eventosMesAnterior);
                    return allEventos.Where(compiledPredicate).ToList();
                });

            // Act
            var result = await _sut.GetReporteEventosAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.CrecimientoPositivo);
            Assert.True(result.PorcentajeCambioEventos > 0);
        }

        [Fact]
        public async Task GetReporteEventosAsync_DeberiaCalcularTasaOcupacionCorrectamente()
        {
            // Arrange
            var hoy = DateTime.Today;
            var inicioMesActual = new DateTime(hoy.Year, hoy.Month, 1);

            var eventos = new List<Evento>
            {
                new Evento 
                { 
                    EventoId = 1, 
                    Inicio = inicioMesActual.AddDays(5), 
                    Fin = inicioMesActual.AddDays(7), 
                    Estado = EventoEstado.PendientePagado 
                }
            };

            _mockEventoRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Evento, bool>>>()))
                .ReturnsAsync((Expression<Func<Evento, bool>> predicate) =>
                {
                    var compiledPredicate = predicate.Compile();
                    return eventos.Where(compiledPredicate).ToList();
                });

            // Act
            var result = await _sut.GetReporteEventosAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.TasaOcupacionEsteMes >= 0);
            Assert.True(result.DiasDisponiblesEsteMes >= 0);
        }

        #endregion

        #region GetReporteIngresosAsync Tests

        [Fact]
        public async Task GetReporteIngresosAsync_ConPagos_DeberiaCalcularIngresosCorrectamente()
        {
            // Arrange
            var hoy = DateTime.Today;
            var inicioMesActual = new DateTime(hoy.Year, hoy.Month, 1);

            var pagos = new List<Pago>
            {
                new Pago 
                { 
                    PagoId = 1, 
                    Monto = 1000, 
                    Fecha = inicioMesActual.AddDays(5),
                    Evento = new Evento 
                    { 
                        EventoId = 1, 
                        CostoAlquiler = 2000, 
                        MontoReserva = 500,
                        MontoAireAcondicionado = 500,
                        Estado = EventoEstado.PendientePagado
                    }
                },
                new Pago 
                { 
                    PagoId = 2, 
                    Monto = 1500, 
                    Fecha = inicioMesActual.AddDays(10),
                    Evento = new Evento 
                    { 
                        EventoId = 2, 
                        CostoAlquiler = 3000, 
                        MontoReserva = 1000,
                        MontoAireAcondicionado = 0,
                        Estado = EventoEstado.PendientePagado
                    }
                }
            };

            _mockPagoRepository.Setup(r => r.FindWithIncludesAsync(
                It.IsAny<Expression<Func<Pago, bool>>>(),
                It.IsAny<Expression<Func<Pago, object>>[]>()
            )).ReturnsAsync((Expression<Func<Pago, bool>> predicate, Expression<Func<Pago, object>>[] includes) =>
            {
                if (predicate != null)
                {
                    var compiledPredicate = predicate.Compile();
                    return pagos.Where(compiledPredicate).ToList();
                }
                return pagos;
            });

            // Act
            var result = await _sut.GetReporteIngresosAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.TotalIngresosEsteMes > 0);
            Assert.True(result.TotalReservasEsteMes >= 0);
            Assert.True(result.TotalAlquileresEsteMes >= 0);
        }

        [Fact]
        public async Task GetReporteIngresosAsync_SinPagos_DeberiaRetornarCeros()
        {
            // Arrange
            _mockPagoRepository.Setup(r => r.FindWithIncludesAsync(
                It.IsAny<Expression<Func<Pago, bool>>>(),
                It.IsAny<Expression<Func<Pago, object>>[]>()
            )).ReturnsAsync(new List<Pago>());

            // Act
            var result = await _sut.GetReporteIngresosAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.TotalIngresosEsteMes);
            Assert.Equal(0, result.TotalReservasEsteMes);
            Assert.Equal(0, result.TotalAlquileresEsteMes);
        }

        #endregion

        #region GetReportePagosAsync Tests

        [Fact]
        public async Task GetReportePagosAsync_ConPagosPorMetodo_DeberiaContabilizarCorrectamente()
        {
            // Arrange
            var hoy = DateTime.Today;
            var inicioMesActual = new DateTime(hoy.Year, hoy.Month, 1);

            var pagosEsteMes = new List<Pago>
            {
                new Pago { PagoId = 1, Monto = 1000, Fecha = inicioMesActual.AddDays(5), Metodo = MetodoPago.Efectivo },
                new Pago { PagoId = 2, Monto = 1500, Fecha = inicioMesActual.AddDays(10), Metodo = MetodoPago.Transferencia },
                new Pago { PagoId = 3, Monto = 500, Fecha = inicioMesActual.AddDays(15), Metodo = MetodoPago.Efectivo }
            };

            var eventos = new List<Evento>
            {
                new Evento 
                { 
                    EventoId = 1, 
                    CostoAlquiler = 2000, 
                    MontoReserva = 500,
                    Estado = EventoEstado.PendienteAdeudado,
                    Pagos = new List<Pago> { pagosEsteMes[0] }
                }
            };

            _mockPagoRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Pago, bool>>>()))
                .ReturnsAsync((Expression<Func<Pago, bool>> predicate) =>
                {
                    var compiledPredicate = predicate.Compile();
                    return pagosEsteMes.Where(compiledPredicate).ToList();
                });

            _mockEventoRepository.Setup(r => r.FindWithIncludesAsync(
                It.IsAny<Expression<Func<Evento, bool>>>(),
                It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync(eventos);

            // Act
            var result = await _sut.GetReportePagosAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.TotalPagosEsteMes > 0);
            Assert.True(result.PagosEfectivoEsteMes > 0);
            Assert.True(result.PagosTransferenciaEsteMes > 0);
        }

        [Fact]
        public async Task GetReportePagosAsync_ConAdeudos_DeberiaCalcularPorcentajeRecuperacion()
        {
            // Arrange
            var eventos = new List<Evento>
            {
                new Evento 
                { 
                    EventoId = 1, 
                    CostoAlquiler = 2000, 
                    MontoReserva = 500,
                    MontoAireAcondicionado = 0,
                    Estado = EventoEstado.PendienteAdeudado,
                    Pagos = new List<Pago> 
                    { 
                        new Pago { PagoId = 1, Monto = 1000, Fecha = DateTime.Now } 
                    }
                }
            };

            _mockPagoRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Pago, bool>>>()))
                .ReturnsAsync(new List<Pago>());

            _mockEventoRepository.Setup(r => r.FindWithIncludesAsync(
                It.IsAny<Expression<Func<Evento, bool>>>(),
                It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync(eventos);

            // Act
            var result = await _sut.GetReportePagosAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.TotalAdeudado > 0);
            Assert.True(result.EventosConAdeudo > 0);
            Assert.True(result.PorcentajeRecuperacion >= 0 && result.PorcentajeRecuperacion <= 100);
        }

        #endregion

        #region GetReporteFianzasAsync Tests

        [Fact]
        public async Task GetReporteFianzasAsync_ConFianzas_DeberiaCalcularTotales()
        {
            // Arrange
            var fianzas = new List<Fianza>
            {
                new Fianza 
                { 
                    FianzaId = 1, 
                    Monto = 1000, 
                    MontoDevuelto = 0,
                    Estado = EstadoFianza.Registrada,
                    FechaRegistro = DateTime.Now.AddDays(-10),
                    FechaDevolucion = DateTime.Now.AddDays(30)
                },
                new Fianza 
                { 
                    FianzaId = 2, 
                    Monto = 1500, 
                    MontoDevuelto = 1500,
                    Estado = EstadoFianza.DevueltaTotalmente,
                    FechaRegistro = DateTime.Now.AddDays(-20),
                    FechaDevolucion = DateTime.Now.AddDays(-1)
                }
            };

            _mockFianzaRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(fianzas);

            // Act
            var result = await _sut.GetReporteFianzasAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2500, result.TotalFianzasRegistradas);
            Assert.Equal(1500, result.TotalFianzasDevueltas);
            Assert.Equal(1000, result.TotalFianzasPendientesDevolucion);
        }

        [Fact]
        public async Task GetReporteFianzasAsync_ConFianzasVencidas_DeberiaContabilizarlas()
        {
            // Arrange
            var fianzas = new List<Fianza>
            {
                new Fianza 
                { 
                    FianzaId = 1, 
                    Monto = 1000, 
                    MontoDevuelto = 0,
                    Estado = EstadoFianza.Registrada,
                    FechaRegistro = DateTime.Now.AddDays(-60),
                    FechaDevolucion = DateTime.Now.AddDays(-10)
                }
            };

            _mockFianzaRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(fianzas);

            // Act
            var result = await _sut.GetReporteFianzasAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.FianzasVencidas);
        }

        #endregion

        #region GetReporteClientesAsync Tests

        [Fact]
        public async Task GetReporteClientesAsync_ConClientes_DeberiaCalcularEstadisticas()
        {
            // Arrange
            var hoy = DateTime.Today;
            var inicioMesActual = new DateTime(hoy.Year, hoy.Month, 1);

            var clientes = new List<Cliente>
            {
                new Cliente 
                { 
                    ClienteId = 1, 
                    Nombre = "Juan", 
                    Apellido = "Pérez",
                    Activo = true,
                    Eventos = new List<Evento>
                    {
                        new Evento { EventoId = 1, FechaContrato = inicioMesActual.AddDays(5) }
                    }
                },
                new Cliente 
                { 
                    ClienteId = 2, 
                    Nombre = "María", 
                    Apellido = "González",
                    Activo = false,
                    Eventos = new List<Evento>()
                }
            };

            _mockClienteRepository.Setup(r => r.FindWithIncludesAsync(
                null,
                It.IsAny<Expression<Func<Cliente, object>>[]>()
            )).ReturnsAsync(clientes);

            // Act
            var result = await _sut.GetReporteClientesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.TotalClientes);
            Assert.Equal(1, result.ClientesActivos);
            Assert.Equal(1, result.ClientesInactivos);
            Assert.True(result.PromedioEventosPorCliente >= 0);
        }

        [Fact]
        public async Task GetReporteClientesAsync_ConClientesRecurrentes_DeberiaCalcularPorcentaje()
        {
            // Arrange
            var hoy = DateTime.Today;
            var inicioMesActual = new DateTime(hoy.Year, hoy.Month, 1);

            var clientes = new List<Cliente>
            {
                new Cliente 
                { 
                    ClienteId = 1, 
                    Nombre = "Juan", 
                    Activo = true,
                    Eventos = new List<Evento>
                    {
                        new Evento { EventoId = 1, FechaContrato = inicioMesActual.AddMonths(-2) },
                        new Evento { EventoId = 2, FechaContrato = inicioMesActual.AddDays(5) }
                    }
                }
            };

            _mockClienteRepository.Setup(r => r.FindWithIncludesAsync(
                null,
                It.IsAny<Expression<Func<Cliente, object>>[]>()
            )).ReturnsAsync(clientes);

            // Act
            var result = await _sut.GetReporteClientesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.ClientesRecurrentesEsteMes > 0);
            Assert.True(result.PorcentajeRecurrencia >= 0);
        }

        #endregion

        #region Helper Methods

        private void SetupMocksConDatosBase()
        {
            var eventos = new List<Evento>
            {
                new Evento 
                { 
                    EventoId = 1, 
                    Inicio = DateTime.Today, 
                    Fin = DateTime.Today,
                    Estado = EventoEstado.PendientePagado,
                    Tipo = TipoEvento.Cumpleaños
                }
            };

            var pagos = new List<Pago>
            {
                new Pago 
                { 
                    PagoId = 1, 
                    Monto = 1000, 
                    Fecha = DateTime.Now,
                    EventoId = 1,
                    Evento = eventos[0]
                }
            };

            var fianzas = new List<Fianza>
            {
                new Fianza 
                { 
                    FianzaId = 1, 
                    Monto = 500, 
                    MontoDevuelto = 0,
                    Estado = EstadoFianza.Registrada,
                    FechaRegistro = DateTime.Now,
                    FechaDevolucion = DateTime.Now.AddDays(30)
                }
            };

            var clientes = new List<Cliente>
            {
                new Cliente 
                { 
                    ClienteId = 1, 
                    Nombre = "Juan", 
                    Activo = true,
                    Eventos = eventos
                }
            };

            eventos[0].Cliente = clientes[0];
            eventos[0].Pagos = pagos;

            _mockEventoRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Evento, bool>>>()))
                .ReturnsAsync((Expression<Func<Evento, bool>> predicate) =>
                {
                    if (predicate != null)
                    {
                        var compiledPredicate = predicate.Compile();
                        return eventos.Where(compiledPredicate).ToList();
                    }
                    return eventos;
                });

            _mockEventoRepository.Setup(r => r.FindWithIncludesAsync(
                It.IsAny<Expression<Func<Evento, bool>>>(),
                It.IsAny<Expression<Func<Evento, object>>[]>()
            )).ReturnsAsync((Expression<Func<Evento, bool>> predicate, Expression<Func<Evento, object>>[] includes) =>
            {
                if (predicate != null)
                {
                    var compiledPredicate = predicate.Compile();
                    return eventos.Where(compiledPredicate).ToList();
                }
                return eventos;
            });

            _mockPagoRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Pago, bool>>>()))
                .ReturnsAsync((Expression<Func<Pago, bool>> predicate) =>
                {
                    if (predicate != null)
                    {
                        var compiledPredicate = predicate.Compile();
                        return pagos.Where(compiledPredicate).ToList();
                    }
                    return pagos;
                });

            _mockPagoRepository.Setup(r => r.FindWithIncludesAsync(
                It.IsAny<Expression<Func<Pago, bool>>>(),
                It.IsAny<Expression<Func<Pago, object>>[]>()
            )).ReturnsAsync((Expression<Func<Pago, bool>> predicate, Expression<Func<Pago, object>>[] includes) =>
            {
                if (predicate != null)
                {
                    var compiledPredicate = predicate.Compile();
                    return pagos.Where(compiledPredicate).ToList();
                }
                return pagos;
            });

            _mockFianzaRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(fianzas);

            _mockClienteRepository.Setup(r => r.FindWithIncludesAsync(
                null,
                It.IsAny<Expression<Func<Cliente, object>>[]>()
            )).ReturnsAsync(clientes);
        }

        #endregion
    }
}
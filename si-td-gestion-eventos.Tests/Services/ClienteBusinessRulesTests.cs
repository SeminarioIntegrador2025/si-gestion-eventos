using Microsoft.EntityFrameworkCore;
using Moq;
using Moq.EntityFrameworkCore;
using si_td_gestion_eventos.Context;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Services.Implementation;
using System; // Agregado para DateTime y TimeSpan
using System.Collections.Generic; // Agregado para List<>
using System.Threading.Tasks; // Agregado para Task
using Xunit; // Asegúrate de tener el using para Xunit

namespace si_td_gestion_eventos.Tests
{
    public class ClienteBusinessRulesTests
    {
        private readonly Mock<AppDbContext> _mockContext;
        private readonly ClienteBusinessRules _businessRules;

        public ClienteBusinessRulesTests()
        {
            // --- CORRECCIÓN 1: INICIALIZAR EL MOCK ANTES DE USARLO ---
            _mockContext = new Mock<AppDbContext>(new DbContextOptions<AppDbContext>()); // Necesita DbContextOptions
            _businessRules = new ClienteBusinessRules(_mockContext.Object);
        }

        #region IsCedulaUniqueAsync Tests

        [Fact]
        public async Task IsCedulaUniqueAsync_ShouldReturnFalse_WhenCedulaAlreadyExists()
        {
            // Arrange
            var existingCedula = "12345678";
            var clientes = new List<Cliente>
            {
                new Cliente // El Cliente está bien, no usa Responsable
                {
                    ClienteId = 1, Nombre = "Juan", Apellido = "Pérez", CedulaIdentidad = "12345678",
                    Domicilio = "Calle 123", Telefono = "098765432", Activo = true, Tipo = TipoCliente.PersonaFisica
                }
            };
            _mockContext.Setup(c => c.Cliente).ReturnsDbSet(clientes);

            // Act
            var result = await _businessRules.IsCedulaUniqueAsync(existingCedula);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsCedulaUniqueAsync_ShouldReturnTrue_WhenCedulaDoesNotExist()
        {
            // Arrange
            var newCedula = "99999999";
            var clientes = new List<Cliente>
            {
                new Cliente
                {
                    ClienteId = 1, Nombre = "Juan", Apellido = "Pérez", CedulaIdentidad = "12345678",
                    Domicilio = "Calle 123", Telefono = "098765432", Activo = true, Tipo = TipoCliente.PersonaFisica
                }
            };
            _mockContext.Setup(c => c.Cliente).ReturnsDbSet(clientes);

            // Act
            var result = await _businessRules.IsCedulaUniqueAsync(newCedula);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsCedulaUniqueAsync_ShouldReturnTrue_WhenCedulaExistsButIsExcluded()
        {
            // Arrange
            var existingCedula = "12345678";
            var excludeClienteId = 1;
            var clientes = new List<Cliente>
            {
                new Cliente
                {
                    ClienteId = 1, Nombre = "Juan", Apellido = "Pérez", CedulaIdentidad = "12345678",
                    Domicilio = "Calle 123", Telefono = "098765432", Activo = true, Tipo = TipoCliente.PersonaFisica
                }
            };
            _mockContext.Setup(c => c.Cliente).ReturnsDbSet(clientes);

            // Act
            var result = await _businessRules.IsCedulaUniqueAsync(existingCedula, excludeClienteId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsCedulaUniqueAsync_ShouldReturnFalse_WhenCedulaExistsAndExcludedClienteIsDifferent()
        {
            // Arrange
            var existingCedula = "12345678";
            var excludeClienteId = 2;
            var clientes = new List<Cliente>
            {
                new Cliente
                {
                    ClienteId = 1, Nombre = "Juan", Apellido = "Pérez", CedulaIdentidad = "12345678",
                    Domicilio = "Calle 123", Telefono = "098765432", Activo = true, Tipo = TipoCliente.PersonaFisica
                }
            };
            _mockContext.Setup(c => c.Cliente).ReturnsDbSet(clientes);

            // Act
            var result = await _businessRules.IsCedulaUniqueAsync(existingCedula, excludeClienteId);

            // Assert
            Assert.False(result);
        }

        #endregion

        // --- ARREGLO 2: ACTUALIZAR LA CREACIÓN DE EVENTOS EN LAS PRUEBAS ---
        #region CanDeactivateClienteAsync Tests (y HasActiveEventsAsync)

        // Método helper para crear eventos con la nueva estructura
        private Evento CrearEventoDePrueba(int eventoId, int clienteId, DateTime inicio, EventoEstado estado = EventoEstado.Confirmado)
        {
            return new Evento
            {
                EventoId = eventoId,
                ClienteId = clienteId,
                FechaContrato = inicio.AddDays(-5), // Fecha contrato inventada
                Inicio = inicio,
                Fin = inicio, // Fin inventado
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                Tipo = TipoEvento.Cumpleaños,
                CostoAlquiler = 5000f, // Usar float si cambiaste en la entidad
                MontoReserva = 1000f,
                CantidadPersonas = 50,
                Estado = estado,

                // --- ¡AQUÍ ESTÁ EL CAMBIO IMPORTANTE! ---
                // Inicializamos el objeto anidado
                ResponsableSalon = new ResponsableSalon
                {
                    Nombre = $"Responsable {eventoId}",
                    Telefono = $"0987654{eventoId}",
                    CI = $"1234567{eventoId}" // Usamos CI según la entidad ResponsableSalon
                },
                ServiciosEsenciales = new ServiciosEsenciales // También inicializar este
                {
                    CertificadoAGADU = new CertificadoAGADU() // Y sus objetos internos
                }
                // Las colecciones (Pagos, Fianzas, Reportes) se inicializan vacías por defecto
            };
        }

        [Fact]
        public async Task CanDeactivateClienteAsync_ShouldReturnFalse_WhenClienteHasActiveEvents()
        {
            // Arrange
            var clienteId = 1;
            var eventos = new List<Evento>
            {
                CrearEventoDePrueba(1, clienteId, DateTime.Now.AddDays(10)) // Evento futuro
            };
            _mockContext.Setup(c => c.Evento).ReturnsDbSet(eventos);

            // Act
            var result = await _businessRules.CanDeactivateClienteAsync(clienteId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CanDeactivateClienteAsync_ShouldReturnTrue_WhenClienteHasOnlyPastEvents()
        {
            // Arrange
            var clienteId = 2;
            var eventos = new List<Evento>
            {
                CrearEventoDePrueba(2, clienteId, DateTime.Now.AddDays(-10)) // Evento pasado
            };
            _mockContext.Setup(c => c.Evento).ReturnsDbSet(eventos);

            // Act
            var result = await _businessRules.CanDeactivateClienteAsync(clienteId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CanDeactivateClienteAsync_ShouldReturnTrue_WhenClienteHasNoEvents()
        {
            // Arrange
            var clienteId = 3;
            var eventos = new List<Evento>();
            _mockContext.Setup(c => c.Evento).ReturnsDbSet(eventos);

            // Act
            var result = await _businessRules.CanDeactivateClienteAsync(clienteId);

            // Assert
            Assert.True(result);
        }

        #endregion

        #region HasActiveEventsAsync Tests (Usa el mismo helper 'CrearEventoDePrueba')

        [Fact]
        public async Task HasActiveEventsAsync_ShouldReturnTrue_WhenClienteHasFutureEvents()
        {
            // Arrange
            var clienteId = 1;
            var eventos = new List<Evento>
            {
                CrearEventoDePrueba(1, clienteId, DateTime.Now.AddDays(10)) // Evento futuro
            };
            _mockContext.Setup(c => c.Evento).ReturnsDbSet(eventos);

            // Act
            var result = await _businessRules.HasActiveEventsAsync(clienteId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task HasActiveEventsAsync_ShouldReturnFalse_WhenClienteHasOnlyPastEvents()
        {
            // Arrange
            var clienteId = 2;
            var eventos = new List<Evento>
            {
                CrearEventoDePrueba(2, clienteId, DateTime.Now.AddDays(-10)) // Evento pasado
            };
            _mockContext.Setup(c => c.Evento).ReturnsDbSet(eventos);

            // Act
            var result = await _businessRules.HasActiveEventsAsync(clienteId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task HasActiveEventsAsync_ShouldReturnFalse_WhenClienteHasNoEvents()
        {
            // Arrange
            var clienteId = 3;
            var eventos = new List<Evento>();
            _mockContext.Setup(c => c.Evento).ReturnsDbSet(eventos);

            // Act
            var result = await _businessRules.HasActiveEventsAsync(clienteId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task HasActiveEventsAsync_ShouldReturnFalse_WhenClienteDoesNotExist()
        {
            // Arrange
            var clienteId = 999;
            var eventos = new List<Evento>
            {
                CrearEventoDePrueba(1, 1, DateTime.Now.AddDays(10)) // Evento de otro cliente
            };
            _mockContext.Setup(c => c.Evento).ReturnsDbSet(eventos);

            // Act
            var result = await _businessRules.HasActiveEventsAsync(clienteId);

            // Assert
            Assert.False(result);
        }

        #endregion
    }
}
using Moq;
using Moq.EntityFrameworkCore;
using si_td_gestion_eventos.Context;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Services.Implementation;

namespace si_td_gestion_eventos.Tests;

public class ClienteBusinessRulesTests
{
    private readonly Mock<AppDbContext> _mockContext;
    private readonly ClienteBusinessRules _businessRules;

    public ClienteBusinessRulesTests()
    {
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
            new Cliente
            {
                ClienteId = 1,
                Nombre = "Juan",
                Apellido = "Pérez",
                CedulaIdentidad = "12345678",
                Domicilio = "Calle 123",
                Telefono = "098765432",
                Activo = true
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
                ClienteId = 1,
                Nombre = "Juan",
                Apellido = "Pérez",
                CedulaIdentidad = "12345678",
                Domicilio = "Calle 123",
                Telefono = "098765432",
                Activo = true
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
        var excludeClienteId = 1; // El cliente que ya tiene esa cédula
        var clientes = new List<Cliente>
        {
            new Cliente
            {
                ClienteId = 1,
                Nombre = "Juan",
                Apellido = "Pérez",
                CedulaIdentidad = "12345678",
                Domicilio = "Calle 123",
                Telefono = "098765432",
                Activo = true
            }
        };

        _mockContext.Setup(c => c.Cliente).ReturnsDbSet(clientes);

        // Act
        var result = await _businessRules.IsCedulaUniqueAsync(existingCedula, excludeClienteId);

        // Assert
        Assert.True(result); // Debe ser true porque excluimos al cliente que ya tiene esa cédula
    }

    [Fact]
    public async Task IsCedulaUniqueAsync_ShouldReturnFalse_WhenCedulaExistsAndExcludedClienteIsDifferent()
    {
        // Arrange
        var existingCedula = "12345678";
        var excludeClienteId = 2; // Un cliente diferente al que tiene esa cédula
        var clientes = new List<Cliente>
        {
            new Cliente
            {
                ClienteId = 1,
                Nombre = "Juan",
                Apellido = "Pérez",
                CedulaIdentidad = "12345678",
                Domicilio = "Calle 123",
                Telefono = "098765432",
                Activo = true
            }
        };

        _mockContext.Setup(c => c.Cliente).ReturnsDbSet(clientes);

        // Act
        var result = await _businessRules.IsCedulaUniqueAsync(existingCedula, excludeClienteId);

        // Assert
        Assert.False(result); // Debe ser false porque otro cliente tiene esa cédula
    }

    #endregion

    #region CanDeactivateClienteAsync Tests

    [Fact]
    public async Task CanDeactivateClienteAsync_ShouldReturnFalse_WhenClienteHasActiveEvents()
    {
        // Arrange
        var clienteId = 1; // Tiene un evento futuro
        var eventos = new List<Evento>
        {
            new Evento
            {
                EventoId = 1,
                FechaContrato = DateTime.Now,
                Inicio = DateTime.Now.AddDays(10), // Evento futuro
                Fin = DateTime.Now.AddDays(10),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                Tipo = TipoEvento.Cumpleaños,
                CostoAlquiler = 5000,
                MontoReserva = 1000,
                CantidadPersonas = 50,
                ResponsableNombre = "Responsable 1",
                ResponsableTelefono = "098765432",
                ResponsableCedula = "12345678",
                Estado = EventoEstado.Confirmado,
                ClienteId = 1
            }
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
        var clienteId = 2; // Solo tiene eventos pasados
        var eventos = new List<Evento>
        {
            new Evento
            {
                EventoId = 2,
                FechaContrato = DateTime.Now.AddDays(-20),
                Inicio = DateTime.Now.AddDays(-10), // Evento pasado
                Fin = DateTime.Now.AddDays(-10),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                Tipo = TipoEvento.Casamiento,
                CostoAlquiler = 8000,
                MontoReserva = 2000,
                CantidadPersonas = 100,
                ResponsableNombre = "Responsable 2",
                ResponsableTelefono = "091234567",
                ResponsableCedula = "87654321",
                Estado = EventoEstado.Confirmado,
                ClienteId = 2
            }
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
        var clienteId = 3; // No tiene eventos
        var eventos = new List<Evento>(); // Lista vacía

        _mockContext.Setup(c => c.Evento).ReturnsDbSet(eventos);

        // Act
        var result = await _businessRules.CanDeactivateClienteAsync(clienteId);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region HasActiveEventsAsync Tests

    [Fact]
    public async Task HasActiveEventsAsync_ShouldReturnTrue_WhenClienteHasFutureEvents()
    {
        // Arrange
        var clienteId = 1; // Tiene un evento futuro
        var eventos = new List<Evento>
        {
            new Evento
            {
                EventoId = 1,
                FechaContrato = DateTime.Now,
                Inicio = DateTime.Now.AddDays(10), // Evento futuro
                Fin = DateTime.Now.AddDays(10),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                Tipo = TipoEvento.Cumpleaños,
                CostoAlquiler = 5000,
                MontoReserva = 1000,
                CantidadPersonas = 50,
                ResponsableNombre = "Responsable 1",
                ResponsableTelefono = "098765432",
                ResponsableCedula = "12345678",
                Estado = EventoEstado.Confirmado,
                ClienteId = 1
            }
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
        var clienteId = 2; // Solo tiene eventos pasados
        var eventos = new List<Evento>
        {
            new Evento
            {
                EventoId = 2,
                FechaContrato = DateTime.Now.AddDays(-20),
                Inicio = DateTime.Now.AddDays(-10), // Evento pasado
                Fin = DateTime.Now.AddDays(-10),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                Tipo = TipoEvento.Casamiento,
                CostoAlquiler = 8000,
                MontoReserva = 2000,
                CantidadPersonas = 100,
                ResponsableNombre = "Responsable 2",
                ResponsableTelefono = "091234567",
                ResponsableCedula = "87654321",
                Estado = EventoEstado.Confirmado,
                ClienteId = 2
            }
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
        var clienteId = 3; // No tiene eventos
        var eventos = new List<Evento>(); // Lista vacía

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
        var clienteId = 999; // No existe
        var eventos = new List<Evento>
        {
            new Evento
            {
                EventoId = 1,
                FechaContrato = DateTime.Now,
                Inicio = DateTime.Now.AddDays(10),
                Fin = DateTime.Now.AddDays(10),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                Tipo = TipoEvento.Cumpleaños,
                CostoAlquiler = 5000,
                MontoReserva = 1000,
                CantidadPersonas = 50,
                ResponsableNombre = "Responsable 1",
                ResponsableTelefono = "098765432",
                ResponsableCedula = "12345678",
                Estado = EventoEstado.Confirmado,
                ClienteId = 1 // Diferente al clienteId buscado
            }
        };

        _mockContext.Setup(c => c.Evento).ReturnsDbSet(eventos);

        // Act
        var result = await _businessRules.HasActiveEventsAsync(clienteId);

        // Assert
        Assert.False(result);
    }

    #endregion
}
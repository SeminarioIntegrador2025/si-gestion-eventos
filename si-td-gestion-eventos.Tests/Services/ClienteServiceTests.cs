using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Repositories;
using si_td_gestion_eventos.Services;
using si_td_gestion_eventos.Services.Contracts;


namespace si_td_gestion_eventos.Tests;

public class ClienteServiceTests
{   
    private readonly Mock<IGenericRepository<Cliente>> _mockRepository;
    private readonly Mock<IValidator<ClienteVM>> _mockValidator;
    private readonly Mock<IClienteBusinessRules> _mockBusinessRules;
    private readonly Mock<IMapper> _mockMapper;
    private readonly ClienteService _clienteService;


    public ClienteServiceTests()
    {
        _mockRepository = new Mock<IGenericRepository<Cliente>>();
        _mockValidator = new Mock<IValidator<ClienteVM>>();
        _mockBusinessRules = new Mock<IClienteBusinessRules>();
        _mockMapper = new Mock<IMapper>();

        _clienteService = new ClienteService(
            _mockRepository.Object,
            _mockValidator.Object,
            _mockBusinessRules.Object,
            _mockMapper.Object
        );

    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ShouldReturnMappedClientes_WhenClientesExist()
    {
        // Arrange
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
            },
            new Cliente
            {
                ClienteId = 2,
                Nombre = "María",
                Apellido = "González",
                CedulaIdentidad = "87654321",
                Domicilio = "Avenida 456",
                Telefono = "091234567",
                Activo = false
            }
        };

        var clientesVM = new List<ClienteVM>
        {
            new ClienteVM
            {
                ClienteId = 1,
                Nombre = "Juan",
                Apellido = "Pérez",
                CedulaIdentidad = "12345678",
                Domicilio = "Calle 123",
                Telefono = "098765432",
                Activo = true
            },
            new ClienteVM
            {
                ClienteId = 2,
                Nombre = "María",
                Apellido = "González",
                CedulaIdentidad = "87654321",
                Domicilio = "Avenida 456",
                Telefono = "091234567",
                Activo = false
            }
        };

        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(clientes);
        _mockMapper.Setup(m => m.Map<IEnumerable<ClienteVM>>(clientes)).Returns(clientesVM);

        // Act
        var result = await _clienteService.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        Assert.Equal("Juan", result.First().Nombre);
        Assert.Equal("María", result.Last().Nombre);

        _mockRepository.Verify(r => r.GetAllAsync(), Times.Once);
        _mockMapper.Verify(m => m.Map<IEnumerable<ClienteVM>>(clientes), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyCollection_WhenNoClientesExist()
    {
        // Arrange
        var emptyClientes = new List<Cliente>();
        var emptyClientesVM = new List<ClienteVM>();

        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(emptyClientes);
        _mockMapper.Setup(m => m.Map<IEnumerable<ClienteVM>>(emptyClientes)).Returns(emptyClientesVM);

        // Act
        var result = await _clienteService.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);

        _mockRepository.Verify(r => r.GetAllAsync(), Times.Once);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ShouldReturnClienteVM_WhenClienteExists()
    {
        // Arrange
        var clienteId = 1;
        var cliente = new Cliente
        {
            ClienteId = clienteId,
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        var clienteVM = new ClienteVM
        {
            ClienteId = clienteId,
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        _mockRepository.Setup(r => r.GetByIdAsync(clienteId)).ReturnsAsync(cliente);
        _mockMapper.Setup(m => m.Map<ClienteVM>(cliente)).Returns(clienteVM);

        // Act
        var result = await _clienteService.GetByIdAsync(clienteId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(clienteId, result.ClienteId);
        Assert.Equal("Juan", result.Nombre);
        Assert.Equal("Pérez", result.Apellido);

        _mockRepository.Verify(r => r.GetByIdAsync(clienteId), Times.Once);
        _mockMapper.Verify(m => m.Map<ClienteVM>(cliente), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenClienteDoesNotExist()
    {
        // Arrange
        var clienteId = 999;
        Cliente? cliente = null;

        _mockRepository.Setup(r => r.GetByIdAsync(clienteId)).ReturnsAsync(cliente);

        // Act
        var result = await _clienteService.GetByIdAsync(clienteId);

        // Assert
        Assert.Null(result);

        _mockRepository.Verify(r => r.GetByIdAsync(clienteId), Times.Once);
        _mockMapper.Verify(m => m.Map<ClienteVM>(It.IsAny<Cliente>()), Times.Never);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ShouldReturnSuccess_WhenValidationPasses()
    {
        // Arrange
        var clienteVM = new ClienteVM
        {
            ClienteId = 0,
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        var cliente = new Cliente
        {
            ClienteId = 1,
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        var validationResult = new ValidationResult();
        _mockValidator.Setup(v => v.ValidateAsync(clienteVM, default)).ReturnsAsync(validationResult);
        _mockMapper.Setup(m => m.Map<Cliente>(clienteVM)).Returns(cliente);

        // Act
        var result = await _clienteService.CreateAsync(clienteVM);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.ClienteId);
        Assert.Equal("Cliente creado con éxito.", result.Message);

        _mockValidator.Verify(v => v.ValidateAsync(clienteVM, default), Times.Once);
        _mockMapper.Verify(m => m.Map<Cliente>(clienteVM), Times.Once);
        _mockRepository.Verify(r => r.AddAsync(cliente), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnFailure_WhenValidationFails()
    {
        // Arrange
        var clienteVM = new ClienteVM
        {
            ClienteId = 0,
            Nombre = "",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        var validationFailures = new List<ValidationFailure>
        {
            new ValidationFailure("Nombre", "El nombre es obligatorio.")
        };
        var validationResult = new ValidationResult(validationFailures);

        _mockValidator.Setup(v => v.ValidateAsync(clienteVM, default)).ReturnsAsync(validationResult);

        // Act
        var result = await _clienteService.CreateAsync(clienteVM);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("El nombre es obligatorio.", result.Errors);

        _mockValidator.Verify(v => v.ValidateAsync(clienteVM, default), Times.Once);
        _mockMapper.Verify(m => m.Map<Cliente>(It.IsAny<ClienteVM>()), Times.Never);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Cliente>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnFailure_WhenExceptionOccurs()
    {
        // Arrange
        var clienteVM = new ClienteVM
        {
            ClienteId = 0,
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        var cliente = new Cliente
        {
            ClienteId = 0,
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        var validationResult = new ValidationResult();
        _mockValidator.Setup(v => v.ValidateAsync(clienteVM, default)).ReturnsAsync(validationResult);
        _mockMapper.Setup(m => m.Map<Cliente>(clienteVM)).Returns(cliente);
        _mockRepository.Setup(r => r.AddAsync(cliente)).ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _clienteService.CreateAsync(clienteVM);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Error al crear cliente: Database error", result.Errors);

        _mockValidator.Verify(v => v.ValidateAsync(clienteVM, default), Times.Once);
        _mockMapper.Verify(m => m.Map<Cliente>(clienteVM), Times.Once);
        _mockRepository.Verify(r => r.AddAsync(cliente), Times.Once);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ShouldReturnSuccess_WhenValidationPassesAndClienteExists()
    {
        // Arrange
        var clienteVM = new ClienteVM
        {
            ClienteId = 1,
            Nombre = "Juan Actualizado",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123 Actualizada",
            Telefono = "098765432",
            Activo = true
        };

        var cliente = new Cliente
        {
            ClienteId = 1,
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        var validationResult = new ValidationResult();
        _mockValidator.Setup(v => v.ValidateAsync(clienteVM, default)).ReturnsAsync(validationResult);
        _mockRepository.Setup(r => r.GetByIdAsync(clienteVM.ClienteId)).ReturnsAsync(cliente);
        _mockMapper.Setup(m => m.Map(clienteVM, cliente)).Returns(cliente);

        // Act
        var result = await _clienteService.UpdateAsync(clienteVM);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Cliente actualizado con éxito.", result.Message);

        _mockValidator.Verify(v => v.ValidateAsync(clienteVM, default), Times.Once);
        _mockRepository.Verify(r => r.GetByIdAsync(clienteVM.ClienteId), Times.Once);
        _mockMapper.Verify(m => m.Map(clienteVM, cliente), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnFailure_WhenValidationFails()
    {
        // Arrange
        var clienteVM = new ClienteVM
        {
            ClienteId = 1,
            Nombre = "",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        var validationFailures = new List<ValidationFailure>
        {
            new ValidationFailure("Nombre", "El nombre es obligatorio.")
        };
        var validationResult = new ValidationResult(validationFailures);

        _mockValidator.Setup(v => v.ValidateAsync(clienteVM, default)).ReturnsAsync(validationResult);

        // Act
        var result = await _clienteService.UpdateAsync(clienteVM);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("El nombre es obligatorio.", result.Errors);

        _mockValidator.Verify(v => v.ValidateAsync(clienteVM, default), Times.Once);
        _mockRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnFailure_WhenClienteDoesNotExist()
    {
        // Arrange
        var clienteVM = new ClienteVM
        {
            ClienteId = 999,
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        var validationResult = new ValidationResult();
        _mockValidator.Setup(v => v.ValidateAsync(clienteVM, default)).ReturnsAsync(validationResult);
        _mockRepository.Setup(r => r.GetByIdAsync(clienteVM.ClienteId)).ReturnsAsync((Cliente?)null);

        // Act
        var result = await _clienteService.UpdateAsync(clienteVM);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Cliente no encontrado.", result.Errors);

        _mockValidator.Verify(v => v.ValidateAsync(clienteVM, default), Times.Once);
        _mockRepository.Verify(r => r.GetByIdAsync(clienteVM.ClienteId), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnFailure_WhenExceptionOccurs()
    {
        // Arrange
        var clienteVM = new ClienteVM
        {
            ClienteId = 1,
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        var cliente = new Cliente
        {
            ClienteId = 1,
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        var validationResult = new ValidationResult();
        _mockValidator.Setup(v => v.ValidateAsync(clienteVM, default)).ReturnsAsync(validationResult);
        _mockRepository.Setup(r => r.GetByIdAsync(clienteVM.ClienteId)).ReturnsAsync(cliente);
        _mockMapper.Setup(m => m.Map(clienteVM, cliente)).Returns(cliente);
        _mockRepository.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _clienteService.UpdateAsync(clienteVM);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Error al actualizar cliente: Database error", result.Errors);

        _mockValidator.Verify(v => v.ValidateAsync(clienteVM, default), Times.Once);
        _mockRepository.Verify(r => r.GetByIdAsync(clienteVM.ClienteId), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    #endregion

    #region DeactivateAsync Tests

    [Fact]
    public async Task DeactivateAsync_ShouldReturnSuccess_WhenClienteCanBeDeactivated()
    {
        // Arrange
        var clienteId = 1;
        var cliente = new Cliente
        {
            ClienteId = clienteId,
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        _mockBusinessRules.Setup(br => br.CanDeactivateClienteAsync(clienteId)).ReturnsAsync(true);
        _mockRepository.Setup(r => r.GetByIdAsync(clienteId)).ReturnsAsync(cliente);

        // Act
        var result = await _clienteService.DeactivateAsync(clienteId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Cliente dado de baja correctamente.", result.Message);
        Assert.False(cliente.Activo);

        _mockBusinessRules.Verify(br => br.CanDeactivateClienteAsync(clienteId), Times.Once);
        _mockRepository.Verify(r => r.GetByIdAsync(clienteId), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_ShouldReturnFailure_WhenClienteCannotBeDeactivated()
    {
        // Arrange
        var clienteId = 1;

        _mockBusinessRules.Setup(br => br.CanDeactivateClienteAsync(clienteId)).ReturnsAsync(false);

        // Act
        var result = await _clienteService.DeactivateAsync(clienteId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No se puede dar de baja al cliente porque tiene eventos activos.", result.Errors);

        _mockBusinessRules.Verify(br => br.CanDeactivateClienteAsync(clienteId), Times.Once);
        _mockRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateAsync_ShouldReturnFailure_WhenClienteDoesNotExist()
    {
        // Arrange
        var clienteId = 999;

        _mockBusinessRules.Setup(br => br.CanDeactivateClienteAsync(clienteId)).ReturnsAsync(true);
        _mockRepository.Setup(r => r.GetByIdAsync(clienteId)).ReturnsAsync((Cliente?)null);

        // Act
        var result = await _clienteService.DeactivateAsync(clienteId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Cliente no encontrado.", result.Errors);

        _mockBusinessRules.Verify(br => br.CanDeactivateClienteAsync(clienteId), Times.Once);
        _mockRepository.Verify(r => r.GetByIdAsync(clienteId), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeactivateAsync_ShouldReturnFailure_WhenExceptionOccurs()
    {
        // Arrange
        var clienteId = 1;
        var cliente = new Cliente
        {
            ClienteId = clienteId,
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        _mockBusinessRules.Setup(br => br.CanDeactivateClienteAsync(clienteId)).ReturnsAsync(true);
        _mockRepository.Setup(r => r.GetByIdAsync(clienteId)).ReturnsAsync(cliente);
        _mockRepository.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _clienteService.DeactivateAsync(clienteId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Error al dar de baja cliente: Database error", result.Errors);

        _mockBusinessRules.Verify(br => br.CanDeactivateClienteAsync(clienteId), Times.Once);
        _mockRepository.Verify(r => r.GetByIdAsync(clienteId), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    #endregion

    #region ActivateAsync Tests

    [Fact]
    public async Task ActivateAsync_ShouldReturnSuccess_WhenClienteExists()
    {
        // Arrange
        var clienteId = 1;
        var cliente = new Cliente
        {
            ClienteId = clienteId,
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = false
        };

        _mockRepository.Setup(r => r.GetByIdAsync(clienteId)).ReturnsAsync(cliente);

        // Act
        var result = await _clienteService.ActivateAsync(clienteId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Cliente activado correctamente.", result.Message);
        Assert.True(cliente.Activo);

        _mockRepository.Verify(r => r.GetByIdAsync(clienteId), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ActivateAsync_ShouldReturnFailure_WhenClienteDoesNotExist()
    {
        // Arrange
        var clienteId = 999;

        _mockRepository.Setup(r => r.GetByIdAsync(clienteId)).ReturnsAsync((Cliente?)null);

        // Act
        var result = await _clienteService.ActivateAsync(clienteId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Cliente no encontrado.", result.Errors);

        _mockRepository.Verify(r => r.GetByIdAsync(clienteId), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ActivateAsync_ShouldReturnFailure_WhenExceptionOccurs()
    {
        // Arrange
        var clienteId = 1;
        var cliente = new Cliente
        {
            ClienteId = clienteId,
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = false
        };

        _mockRepository.Setup(r => r.GetByIdAsync(clienteId)).ReturnsAsync(cliente);
        _mockRepository.Setup(r => r.SaveChangesAsync()).ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _clienteService.ActivateAsync(clienteId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Error al activar cliente: Database error", result.Errors);

        _mockRepository.Verify(r => r.GetByIdAsync(clienteId), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    #endregion

    #region GetClientesActivosParaDropdown Tests

    [Fact]
    public async Task GetClientesActivosParaDropdown_ShouldReturnActiveClientsAsSelectList()
    {
        // Arrange
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
            },
            new Cliente
            {
                ClienteId = 2,
                Nombre = "María",
                Apellido = "González",
                CedulaIdentidad = "87654321",
                Domicilio = "Avenida 456",
                Telefono = "091234567",
                Activo = false // Este no debería aparecer
            },
            new Cliente
            {
                ClienteId = 3,
                Nombre = "Pedro",
                Apellido = "López",
                CedulaIdentidad = "11111111",
                Domicilio = "Boulevard 789",
                Telefono = "099876543",
                Activo = true
            }
        };

        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(clientes);

        // Act
        var result = _clienteService.GetClientesActivosParaDropdown();

        // Assert
        Assert.NotNull(result);
        var selectList = result.ToList();
        Assert.Equal(2, selectList.Count); // Solo los activos
            
        Assert.Equal("1", selectList[0].Value);
        Assert.Equal("Juan Pérez", selectList[0].Text);
            
        Assert.Equal("3", selectList[1].Value);
        Assert.Equal("Pedro López", selectList[1].Text);

        _mockRepository.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public void GetClientesActivosParaDropdown_ShouldReturnEmptyList_WhenNoActiveClientsExist()
    {
        // Arrange
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
                Activo = false
            }
        };

        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(clientes);

        // Act
        var result = _clienteService.GetClientesActivosParaDropdown();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);

        _mockRepository.Verify(r => r.GetAllAsync(), Times.Once);
    }

    #endregion
}

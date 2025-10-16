using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.Rendering;
using Moq;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Repositories;
using si_td_gestion_eventos.Services.Contracts;
using si_td_gestion_eventos.Services.Implementation;
using System.Linq.Expressions;

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

    // --- SECCIÓN CORREGIDA ---
    // Todas las instancias de 'new Cliente' ahora incluyen los campos requeridos.
    #region GetClientesActivosParaDropdownAsync Tests

    [Fact]
    public async Task GetClientesActivosParaDropdownAsync_ShouldReturnActiveClientsAsSelectList()
    {
        // Arrange
        var clientes = new List<Cliente>
        {
            new Cliente
            {
                ClienteId = 1, Nombre = "Juan", Apellido = "Pérez", Activo = true,
                CedulaIdentidad = "123", Domicilio = "Calle 1", Telefono = "111"
            },
            new Cliente
            {
                ClienteId = 2, Nombre = "María", Apellido = "González", Activo = false, // No activo
                CedulaIdentidad = "456", Domicilio = "Calle 2", Telefono = "222"
            },
            new Cliente
            {
                ClienteId = 3, Nombre = "Pedro", Apellido = "López", Activo = true,
                CedulaIdentidad = "789", Domicilio = "Calle 3", Telefono = "333"
            }
        };

        var clientesActivos = new List<Cliente> { clientes[0], clientes[2] };

        // Se mockea FindAsync, que es el método que realmente usa el servicio.
        _mockRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Cliente, bool>>>()))
                       .ReturnsAsync(clientesActivos);

        // Act
        var result = await _clienteService.GetClientesActivosParaDropdownAsync();

        // Assert
        Assert.NotNull(result);
        var selectList = result.ToList();
        Assert.Equal(2, selectList.Count); // Solo los activos

        Assert.Equal("1", selectList[0].Value);
        Assert.Equal("Pérez, Juan", selectList[0].Text);

        Assert.Equal("3", selectList[1].Value);
        Assert.Equal("López, Pedro", selectList[1].Text);

        _mockRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<Cliente, bool>>>()), Times.Once);
    }

    [Fact]
    public async Task GetClientesActivosParaDropdownAsync_ShouldReturnEmptyList_WhenNoActiveClientsExist()
    {
        // Arrange
        _mockRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Cliente, bool>>>()))
                       .ReturnsAsync(new List<Cliente>());

        // Act
        var result = await _clienteService.GetClientesActivosParaDropdownAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    // --- Otras pruebas también se actualizan para incluir todos los campos requeridos ---

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ShouldReturnSuccess_WhenValidationPasses()
    {
        // Arrange
        var clienteVM = new ClienteVM
        {
            Nombre = "Nuevo",
            Apellido = "Cliente",
            CedulaIdentidad = "111",
            Domicilio = "Domicilio",
            Telefono = "Tel",
            Activo = true
        };

        var cliente = new Cliente
        {
            ClienteId = 1, // El ID se asigna después de guardar
            Nombre = "Nuevo",
            Apellido = "Cliente",
            CedulaIdentidad = "111",
            Domicilio = "Domicilio",
            Telefono = "Tel",
            Activo = true
        };

        _mockValidator.Setup(v => v.ValidateAsync(clienteVM, default)).ReturnsAsync(new ValidationResult());
        _mockMapper.Setup(m => m.Map<Cliente>(clienteVM)).Returns(cliente);

        // Act
        var result = await _clienteService.CreateAsync(clienteVM);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.ClienteId);
        _mockRepository.Verify(r => r.AddAsync(cliente), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    #endregion

    // (Asegúrate de que el resto de tus pruebas también completen los datos de los clientes de prueba si es necesario)
}
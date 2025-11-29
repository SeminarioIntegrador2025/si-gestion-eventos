using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Repositories;
using si_td_gestion_eventos.Services.Contracts;
using si_td_gestion_eventos.Services.Implementation;
using System.Linq.Expressions;
using Xunit;

namespace si_td_gestion_eventos.Tests.Services
{
    public class ClienteServiceTests
    {
        private readonly Mock<IGenericRepository<Cliente>> _mockClienteRepository;
        private readonly Mock<IValidator<ClienteVM>> _mockValidator;
        private readonly Mock<IClienteBusinessRules> _mockBusinessRules;
        private readonly Mock<IMapper> _mockMapper;
        private readonly ClienteService _sut;

        public ClienteServiceTests()
        {
            _mockClienteRepository = new Mock<IGenericRepository<Cliente>>();
            _mockValidator = new Mock<IValidator<ClienteVM>>();
            _mockBusinessRules = new Mock<IClienteBusinessRules>();
            _mockMapper = new Mock<IMapper>();

            _sut = new ClienteService(
                _mockClienteRepository.Object,
                _mockValidator.Object,
                _mockBusinessRules.Object,
                _mockMapper.Object
            );
        }

        [Fact]
        public async Task GetAllAsync_DeberiaRetornarListaDeClientesVM()
        {
            // Arrange
            var clientes = new List<Cliente>
            {
                new Cliente { ClienteId = 1, Nombre = "Juan", Apellido = "Pérez" },
                new Cliente { ClienteId = 2, Nombre = "María", Apellido = "González" }
            };

            var clientesVM = new List<ClienteVM>
            {
                new ClienteVM { ClienteId = 1, Nombre = "Juan", Apellido = "Pérez", Telefono = "123", Domicilio = "Dir1", Activo = true },
                new ClienteVM { ClienteId = 2, Nombre = "María", Apellido = "González", Telefono = "456", Domicilio = "Dir2", Activo = true }
            };

            _mockClienteRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(clientes);
            _mockMapper.Setup(m => m.Map<IEnumerable<ClienteVM>>(clientes)).Returns(clientesVM);

            // Act
            var result = await _sut.GetAllAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            _mockClienteRepository.Verify(r => r.GetAllAsync(), Times.Once);
        }

        [Fact]
        public async Task GetByIdAsync_ClienteExiste_DeberiaRetornarClienteVM()
        {
            // Arrange
            var cliente = new Cliente { ClienteId = 1, Nombre = "Juan", Apellido = "Pérez" };
            var clienteVM = new ClienteVM { ClienteId = 1, Nombre = "Juan", Apellido = "Pérez", Telefono = "123", Domicilio = "Dir1", Activo = true };

            _mockClienteRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(cliente);
            _mockMapper.Setup(m => m.Map<ClienteVM>(cliente)).Returns(clienteVM);

            // Act
            var result = await _sut.GetByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.ClienteId);
            Assert.Equal("Juan", result.Nombre);
        }

        [Fact]
        public async Task GetByIdAsync_ClienteNoExiste_DeberiaRetornarNull()
        {
            // Arrange
            _mockClienteRepository.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Cliente?)null);

            // Act
            var result = await _sut.GetByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task CreateAsync_ValidacionExitosa_DeberiaCrearCliente()
        {
            // Arrange
            var clienteVM = new ClienteVM 
            { 
                Nombre = "Juan", 
                Apellido = "Pérez", 
                Telefono = "123456789", 
                Domicilio = "Calle 123", 
                Activo = true 
            };

            var cliente = new Cliente { ClienteId = 1, Nombre = "Juan", Apellido = "Pérez" };

            _mockValidator.Setup(v => v.ValidateAsync(clienteVM, default))
                .ReturnsAsync(new ValidationResult());

            _mockMapper.Setup(m => m.Map<Cliente>(clienteVM)).Returns(cliente);

            _mockClienteRepository.Setup(r => r.AddAsync(It.IsAny<Cliente>())).Returns(Task.CompletedTask);
            _mockClienteRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _sut.CreateAsync(clienteVM);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Cliente creado con éxito.", result.Message);
            _mockClienteRepository.Verify(r => r.AddAsync(It.IsAny<Cliente>()), Times.Once);
            _mockClienteRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ValidacionFallida_DeberiaRetornarErrores()
        {
            // Arrange
            var clienteVM = new ClienteVM 
            { 
                Nombre = "", 
                Apellido = "Pérez", 
                Telefono = "123", 
                Domicilio = "Dir", 
                Activo = true 
            };

            var validationFailures = new List<ValidationFailure>
            {
                new ValidationFailure("Nombre", "El nombre es obligatorio.")
            };

            _mockValidator.Setup(v => v.ValidateAsync(clienteVM, default))
                .ReturnsAsync(new ValidationResult(validationFailures));

            // Act
            var result = await _sut.CreateAsync(clienteVM);

            // Assert
            Assert.False(result.Success);
            Assert.Single(result.Errors);
            Assert.Contains("El nombre es obligatorio.", result.Errors);
            _mockClienteRepository.Verify(r => r.AddAsync(It.IsAny<Cliente>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_ClienteExiste_DeberiaActualizarCliente()
        {
            // Arrange
            var clienteVM = new ClienteVM 
            { 
                ClienteId = 1, 
                Nombre = "Juan Actualizado", 
                Apellido = "Pérez", 
                Telefono = "123", 
                Domicilio = "Dir", 
                Activo = true 
            };

            var clienteExistente = new Cliente { ClienteId = 1, Nombre = "Juan", Apellido = "Pérez" };

            _mockValidator.Setup(v => v.ValidateAsync(clienteVM, default))
                .ReturnsAsync(new ValidationResult());

            _mockClienteRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(clienteExistente);
            _mockMapper.Setup(m => m.Map(clienteVM, clienteExistente)).Returns(clienteExistente);
            _mockClienteRepository.Setup(r => r.Update(It.IsAny<Cliente>()));
            _mockClienteRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _sut.UpdateAsync(clienteVM);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Cliente actualizado con éxito.", result.Message);
            _mockClienteRepository.Verify(r => r.Update(It.IsAny<Cliente>()), Times.Once);
            _mockClienteRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ClienteNoExiste_DeberiaRetornarError()
        {
            // Arrange
            var clienteVM = new ClienteVM 
            { 
                ClienteId = 999, 
                Nombre = "Juan", 
                Apellido = "Pérez", 
                Telefono = "123", 
                Domicilio = "Dir", 
                Activo = true 
            };

            _mockValidator.Setup(v => v.ValidateAsync(clienteVM, default))
                .ReturnsAsync(new ValidationResult());

            _mockClienteRepository.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Cliente?)null);

            // Act
            var result = await _sut.UpdateAsync(clienteVM);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Cliente no encontrado.", result.Errors);
        }

        [Fact]
        public async Task DeactivateAsync_ClienteSinEventosActivos_DeberiaDesactivar()
        {
            // Arrange
            var cliente = new Cliente { ClienteId = 1, Nombre = "Juan", Activo = true };

            _mockBusinessRules.Setup(br => br.CanDeactivateClienteAsync(1)).ReturnsAsync(true);
            _mockClienteRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(cliente);
            _mockClienteRepository.Setup(r => r.Update(It.IsAny<Cliente>()));
            _mockClienteRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _sut.DeactivateAsync(1);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Cliente dado de baja correctamente.", result.Message);
            Assert.False(cliente.Activo);
            _mockClienteRepository.Verify(r => r.Update(It.IsAny<Cliente>()), Times.Once);
        }

        [Fact]
        public async Task DeactivateAsync_ClienteConEventosActivos_DeberiaRetornarError()
        {
            // Arrange
            _mockBusinessRules.Setup(br => br.CanDeactivateClienteAsync(1)).ReturnsAsync(false);

            // Act
            var result = await _sut.DeactivateAsync(1);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("No se puede dar de baja al cliente porque tiene eventos activos.", result.Errors);
            _mockClienteRepository.Verify(r => r.Update(It.IsAny<Cliente>()), Times.Never);
        }

        [Fact]
        public async Task ActivateAsync_ClienteExiste_DeberiaActivar()
        {
            // Arrange
            var cliente = new Cliente { ClienteId = 1, Nombre = "Juan", Activo = false };

            _mockClienteRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(cliente);
            _mockClienteRepository.Setup(r => r.Update(It.IsAny<Cliente>()));
            _mockClienteRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _sut.ActivateAsync(1);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Cliente activado correctamente.", result.Message);
            Assert.True(cliente.Activo);
        }

        [Fact]
        public async Task GetClientesActivosParaDropdownAsync_DeberiaRetornarListaOrdenada()
        {
            // Arrange
            var clientesActivos = new List<Cliente>
            {
                new Cliente { ClienteId = 1, Tipo = Models.Enums.TipoCliente.PersonaFisica, Nombre = "Juan", Apellido = "Pérez", Activo = true },
                new Cliente { ClienteId = 2, Tipo = Models.Enums.TipoCliente.PersonaJuridica, Nombre = "Empresa ABC", Activo = true }
            };

            _mockClienteRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Cliente, bool>>>()))
                .ReturnsAsync(clientesActivos);

            // Act
            var result = await _sut.GetClientesActivosParaDropdownAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }
    }
}
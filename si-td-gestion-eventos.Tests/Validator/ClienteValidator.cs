using FluentValidation.TestHelper;
using Moq;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;
using si_td_gestion_eventos.Validators;

namespace si_td_gestion_eventos.Tests;

public class ClienteValidatorTests
{
    private readonly Mock<IClienteBusinessRules> _mockBusinessRules;
    private readonly ClienteValidator _validator;

    public ClienteValidatorTests()
    {
        _mockBusinessRules = new Mock<IClienteBusinessRules>();
        _validator = new ClienteValidator(_mockBusinessRules.Object);
    }

    #region Nombre Tests

    [Fact]
    public void Nombre_ShouldHaveError_WhenEmpty()
    {
        // Arrange
        var cliente = new ClienteVM
        {
            Nombre = "",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        // Act & Assert
        var result = _validator.TestValidate(cliente);
        result.ShouldHaveValidationErrorFor(c => c.Nombre)
                .WithErrorMessage("El nombre es obligatorio.");
    }

    [Fact]
    public void Nombre_ShouldHaveError_WhenTooShort()
    {
        // Arrange
        var cliente = new ClienteVM
        {
            Nombre = "A",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        // Act & Assert
        var result = _validator.TestValidate(cliente);
        result.ShouldHaveValidationErrorFor(c => c.Nombre)
                .WithErrorMessage("El nombre debe tener entre 2 y 60 caracteres.");
    }

    [Fact]
    public void Nombre_ShouldHaveError_WhenTooLong()
    {
        // Arrange
        var cliente = new ClienteVM
        {
            Nombre = new string('A', 61),
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        // Act & Assert
        var result = _validator.TestValidate(cliente);
        result.ShouldHaveValidationErrorFor(c => c.Nombre)
                .WithErrorMessage("El nombre debe tener entre 2 y 60 caracteres.");
    }

    [Fact]
    public void Nombre_ShouldHaveError_WhenContainsNumbers()
    {
        // Arrange
        var cliente = new ClienteVM
        {
            Nombre = "Juan123",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        // Act & Assert
        var result = _validator.TestValidate(cliente);
        result.ShouldHaveValidationErrorFor(c => c.Nombre)
                .WithErrorMessage("El nombre no puede contener números.");
    }

    [Fact]
    public void Nombre_ShouldHaveError_WhenContainsSpecialCharacters()
    {
        // Arrange
        var cliente = new ClienteVM
        {
            Nombre = "Juan@",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        // Act & Assert
        var result = _validator.TestValidate(cliente);
        result.ShouldHaveValidationErrorFor(c => c.Nombre)
                .WithErrorMessage("El nombre solo puede contener letras y espacios.");
    }

    [Fact]
    public void Nombre_ShouldNotHaveError_WhenValid()
    {
        // Arrange
        var cliente = new ClienteVM
        {
            Nombre = "Juan Carlos",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        _mockBusinessRules.Setup(br => br.IsCedulaUniqueAsync(It.IsAny<string>(), It.IsAny<int?>()))
                            .ReturnsAsync(true);

        // Act & Assert
        var result = _validator.TestValidate(cliente);
        result.ShouldNotHaveValidationErrorFor(c => c.Nombre);
    }

    #endregion

    #region Apellido Tests

    [Fact]
    public void Apellido_ShouldHaveError_WhenEmpty()
    {
        // Arrange
        var cliente = new ClienteVM
        {
            Nombre = "Juan",
            Apellido = "",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        // Act & Assert
        var result = _validator.TestValidate(cliente);
        result.ShouldHaveValidationErrorFor(c => c.Apellido)
                .WithErrorMessage("El apellido es obligatorio.");
    }

    [Fact]
    public void Apellido_ShouldNotHaveError_WhenValid()
    {
        // Arrange
        var cliente = new ClienteVM
        {
            Nombre = "Juan",
            Apellido = "Pérez González",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        _mockBusinessRules.Setup(br => br.IsCedulaUniqueAsync(It.IsAny<string>(), It.IsAny<int?>()))
                            .ReturnsAsync(true);

        // Act & Assert
        var result = _validator.TestValidate(cliente);
        result.ShouldNotHaveValidationErrorFor(c => c.Apellido);
    }

    #endregion

    #region CedulaIdentidad Tests

    [Fact]
    public void CedulaIdentidad_ShouldHaveError_WhenEmpty()
    {
        // Arrange
        var cliente = new ClienteVM
        {
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        // Act & Assert
        var result = _validator.TestValidate(cliente);
        result.ShouldHaveValidationErrorFor(c => c.CedulaIdentidad)
                .WithErrorMessage("La cédula de identidad es obligatoria.");
    }

    [Fact]
    public void CedulaIdentidad_ShouldHaveError_WhenFormatIsInvalid()
    {
        // Arrange
        var cliente = new ClienteVM
        {
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "123", // Muy corta
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        // Act & Assert
        var result = _validator.TestValidate(cliente);
        result.ShouldHaveValidationErrorFor(c => c.CedulaIdentidad)
                .WithErrorMessage("La cédula debe tener formato válido (ej: 1.234.567-8).");
    }

    [Fact]
    public async Task CedulaIdentidad_ShouldHaveError_WhenNotUnique()
    {
        // Arrange
        var cliente = new ClienteVM
        {
            ClienteId = 0,
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        _mockBusinessRules.Setup(br => br.IsCedulaUniqueAsync("12345678", null))
                            .ReturnsAsync(false);

        // Act & Assert
        var result = await _validator.TestValidateAsync(cliente);
        result.ShouldHaveValidationErrorFor(c => c.CedulaIdentidad)
                .WithErrorMessage("Ya existe un cliente con esta cédula de identidad.");
    }

    [Theory]
    [InlineData("1234567")]  // 7 dígitos
    [InlineData("12345678")]  // 8 dígitos
    [InlineData("1.234.567-8")]  // Con formato
    [InlineData("12.345.678-9")]  // Con formato
    public async Task CedulaIdentidad_ShouldNotHaveError_WhenValidAndUnique(string cedula)
    {
        // Arrange
        var cliente = new ClienteVM
        {
            ClienteId = 0,
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = cedula,
            Domicilio = "Calle 123",
            Telefono = "098765432",
            Activo = true
        };

        _mockBusinessRules.Setup(br => br.IsCedulaUniqueAsync(It.IsAny<string>(), It.IsAny<int?>()))
                            .ReturnsAsync(true);

        // Act & Assert
        var result = await _validator.TestValidateAsync(cliente);
        result.ShouldNotHaveValidationErrorFor(c => c.CedulaIdentidad);
    }

    #endregion

    #region Telefono Tests

    [Fact]
    public void Telefono_ShouldHaveError_WhenEmpty()
    {
        // Arrange
        var cliente = new ClienteVM
        {
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "",
            Activo = true
        };

        // Act & Assert
        var result = _validator.TestValidate(cliente);
        result.ShouldHaveValidationErrorFor(c => c.Telefono)
                .WithErrorMessage("El teléfono es obligatorio.");
    }

    [Fact]
    public void Telefono_ShouldHaveError_WhenTooFewDigits()
    {
        // Arrange
        var cliente = new ClienteVM
        {
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "123456", // Solo 6 dígitos
            Activo = true
        };

        // Act & Assert
        var result = _validator.TestValidate(cliente);
        result.ShouldHaveValidationErrorFor(c => c.Telefono)
                .WithErrorMessage("El teléfono debe tener al menos 8 dígitos.");
    }

    [Fact]
    public void Telefono_ShouldHaveError_WhenContainsInvalidCharacters()
    {
        // Arrange
        var cliente = new ClienteVM
        {
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = "098765abc",
            Activo = true
        };

        // Act & Assert
        var result = _validator.TestValidate(cliente);
        result.ShouldHaveValidationErrorFor(c => c.Telefono)
                .WithErrorMessage("El teléfono debe contener solo números, espacios, guiones o paréntesis.");
    }

    [Theory]
    [InlineData("098765432")]
    [InlineData("091-234-567")]
    [InlineData("(099) 876-543")]
    [InlineData("+598 98 765 432")]
    public void Telefono_ShouldNotHaveError_WhenValid(string telefono)
    {
        // Arrange
        var cliente = new ClienteVM
        {
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123",
            Telefono = telefono,
            Activo = true
        };

        _mockBusinessRules.Setup(br => br.IsCedulaUniqueAsync(It.IsAny<string>(), It.IsAny<int?>()))
                            .ReturnsAsync(true);

        // Act & Assert
        var result = _validator.TestValidate(cliente);
        result.ShouldNotHaveValidationErrorFor(c => c.Telefono);
    }

    #endregion

    #region Domicilio Tests

    [Fact]
    public void Domicilio_ShouldHaveError_WhenEmpty()
    {
        // Arrange
        var cliente = new ClienteVM
        {
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "",
            Telefono = "098765432",
            Activo = true
        };

        // Act & Assert
        var result = _validator.TestValidate(cliente);
        result.ShouldHaveValidationErrorFor(c => c.Domicilio)
                .WithErrorMessage("El domicilio es obligatorio.");
    }

    [Fact]
    public void Domicilio_ShouldHaveError_WhenTooShort()
    {
        // Arrange
        var cliente = new ClienteVM
        {
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "123", // Solo 3 caracteres
            Telefono = "098765432",
            Activo = true
        };

        // Act & Assert
        var result = _validator.TestValidate(cliente);
        result.ShouldHaveValidationErrorFor(c => c.Domicilio)
                .WithErrorMessage("El domicilio debe tener entre 5 y 120 caracteres.");
    }

    [Fact]
    public void Domicilio_ShouldHaveError_WhenTooLong()
    {
        // Arrange
        var cliente = new ClienteVM
        {
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = new string('A', 121), // 121 caracteres
            Telefono = "098765432",
            Activo = true
        };

        // Act & Assert
        var result = _validator.TestValidate(cliente);
        result.ShouldHaveValidationErrorFor(c => c.Domicilio)
                .WithErrorMessage("El domicilio debe tener entre 5 y 120 caracteres.");
    }

    [Fact]
    public void Domicilio_ShouldNotHaveError_WhenValid()
    {
        // Arrange
        var cliente = new ClienteVM
        {
            Nombre = "Juan",
            Apellido = "Pérez",
            CedulaIdentidad = "12345678",
            Domicilio = "Calle 123, Montevideo",
            Telefono = "098765432",
            Activo = true
        };

        _mockBusinessRules.Setup(br => br.IsCedulaUniqueAsync(It.IsAny<string>(), It.IsAny<int?>()))
                            .ReturnsAsync(true);

        // Act & Assert
        var result = _validator.TestValidate(cliente);
        result.ShouldNotHaveValidationErrorFor(c => c.Domicilio);
    }

    #endregion
}

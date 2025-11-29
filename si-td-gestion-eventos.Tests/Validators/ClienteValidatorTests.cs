using FluentValidation.TestHelper;
using Moq;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;
using si_td_gestion_eventos.Validators;
using Xunit;

namespace si_td_gestion_eventos.Tests.Validators
{
    public class ClienteValidatorTests
    {
        private readonly Mock<IClienteBusinessRules> _mockBusinessRules;
        private readonly ClienteValidator _validator;

        public ClienteValidatorTests()
        {
            _mockBusinessRules = new Mock<IClienteBusinessRules>();
            _validator = new ClienteValidator(_mockBusinessRules.Object);
        }

        #region Tipo Cliente Tests

        [Fact]
        public async Task Validate_TipoClienteVacio_DeberiaFallar()
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = null,
                Nombre = "Juan",
                Telefono = "099123456",
                Domicilio = "Calle 123",
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.Tipo)
                .WithErrorMessage("Debe seleccionar un tipo de cliente.");
        }

        #endregion

        #region Teléfono Tests

        [Theory]
        [InlineData("099123456")]
        [InlineData("(+598) 099-123-456")]
        [InlineData("2601-1234")]
        [InlineData("+598 99 123 456")]
        public async Task Validate_TelefonoValido_DeberiaSerExitoso(string telefono)
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaFisica,
                Nombre = "Juan",
                Apellido = "Pérez",
                CedulaIdentidad = "12345678",
                Telefono = telefono,
                Domicilio = "Calle 123 esquina",
                Activo = true
            };

            _mockBusinessRules.Setup(br => br.IsCedulaUniqueAsync(It.IsAny<string>(), It.IsAny<int?>()))
                .ReturnsAsync(true);

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldNotHaveValidationErrorFor(c => c.Telefono);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task Validate_TelefonoVacio_DeberiaFallar(string? telefono)
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaFisica,
                Nombre = "Juan",
                Telefono = telefono!,
                Domicilio = "Calle 123",
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.Telefono)
                .WithErrorMessage("El teléfono es obligatorio.");
        }

        [Theory]
        [InlineData("ABC123")]
        [InlineData("12@34")]
        [InlineData("tel:123")]
        [InlineData("099-ABC-DEF")]
        public async Task Validate_TelefonoConCaracteresInvalidos_DeberiaFallar(string telefono)
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaFisica,
                Nombre = "Juan",
                Telefono = telefono,
                Domicilio = "Calle 123",
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.Telefono)
                .WithErrorMessage("El teléfono debe contener solo números, espacios, guiones o paréntesis.");
        }

        [Theory]
        [InlineData("123456")]
        [InlineData("12-34-56")]
        [InlineData("(12) 345")]
        public async Task Validate_TelefonoMenosDe8Digitos_DeberiaFallar(string telefono)
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaFisica,
                Nombre = "Juan",
                Telefono = telefono,
                Domicilio = "Calle 123",
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.Telefono)
                .WithErrorMessage("El teléfono debe tener al menos 8 dígitos.");
        }

        #endregion

        #region Domicilio Tests

        [Fact]
        public async Task Validate_DomicilioVacio_DeberiaFallar()
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaFisica,
                Nombre = "Juan",
                Telefono = "099123456",
                Domicilio = "",
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.Domicilio)
                .WithErrorMessage("El domicilio es obligatorio.");
        }

        [Theory]
        [InlineData("Cal")]
        [InlineData("A")]
        [InlineData("Ab")]
        public async Task Validate_DomicilioMuyCorto_DeberiaFallar(string domicilio)
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaFisica,
                Nombre = "Juan",
                Telefono = "099123456",
                Domicilio = domicilio,
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.Domicilio)
                .WithErrorMessage("El domicilio debe tener entre 5 y 120 caracteres.");
        }

        [Fact]
        public async Task Validate_DomicilioMuyLargo_DeberiaFallar()
        {
            // Arrange
            var domicilioLargo = new string('A', 121);
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaFisica,
                Nombre = "Juan",
                Telefono = "099123456",
                Domicilio = domicilioLargo,
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.Domicilio);
        }

        #endregion

        #region Persona Física Tests

        [Fact]
        public async Task Validate_PersonaFisicaCompleta_DeberiaSerExitoso()
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaFisica,
                Nombre = "Juan",
                Apellido = "Pérez",
                CedulaIdentidad = "1.234.567-8",
                Telefono = "099123456",
                Domicilio = "Calle 123 esquina Avenida",
                Activo = true
            };

            _mockBusinessRules.Setup(br => br.IsCedulaUniqueAsync("1.234.567-8", null))
                .ReturnsAsync(true);

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public async Task Validate_PersonaFisicaSinNombre_DeberiaFallar()
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaFisica,
                Nombre = "",
                Apellido = "Pérez",
                CedulaIdentidad = "12345678",
                Telefono = "099123456",
                Domicilio = "Calle 123",
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.Nombre)
                .WithErrorMessage("El nombre es obligatorio.");
        }

        [Theory]
        [InlineData("Juan123")]
        [InlineData("Pedro9")]
        [InlineData("Ana1234")]
        public async Task Validate_NombreConNumeros_DeberiaFallar(string nombre)
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaFisica,
                Nombre = nombre,
                Apellido = "Pérez",
                CedulaIdentidad = "12345678",
                Telefono = "099123456",
                Domicilio = "Calle 123",
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.Nombre)
                .WithErrorMessage("El nombre no puede contener números.");
        }

        [Theory]
        [InlineData("J")]
        [InlineData("")]
        public async Task Validate_NombreMuyCorto_DeberiaFallar(string nombre)
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaFisica,
                Nombre = nombre,
                Apellido = "Pérez",
                Telefono = "099123456",
                Domicilio = "Calle 123",
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.Nombre);
        }

        [Fact]
        public async Task Validate_NombreMuyLargo_DeberiaFallar()
        {
            // Arrange
            var nombreLargo = new string('A', 61);
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaFisica,
                Nombre = nombreLargo,
                Apellido = "Pérez",
                Telefono = "099123456",
                Domicilio = "Calle 123",
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.Nombre);
        }

        [Fact]
        public async Task Validate_PersonaFisicaSinApellido_DeberiaFallar()
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaFisica,
                Nombre = "Juan",
                Apellido = "",
                CedulaIdentidad = "12345678",
                Telefono = "099123456",
                Domicilio = "Calle 123",
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.Apellido)
                .WithErrorMessage("El apellido es obligatorio.");
        }

        [Theory]
        [InlineData("12345678")]
        [InlineData("1234567")]
        [InlineData("1.234.567-8")]
        [InlineData("12345678")]
        public async Task Validate_CedulaValidaPersonaFisica_DeberiaSerExitoso(string cedula)
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaFisica,
                Nombre = "Juan",
                Apellido = "Pérez",
                CedulaIdentidad = cedula,
                Telefono = "099123456",
                Domicilio = "Calle 123",
                Activo = true
            };

            _mockBusinessRules.Setup(br => br.IsCedulaUniqueAsync(cedula, null))
                .ReturnsAsync(true);

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldNotHaveValidationErrorFor(c => c.CedulaIdentidad);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task Validate_CedulaVaciaPersonaFisica_DeberiaFallar(string? cedula)
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaFisica,
                Nombre = "Juan",
                Apellido = "Pérez",
                CedulaIdentidad = cedula,
                Telefono = "099123456",
                Domicilio = "Calle 123",
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.CedulaIdentidad)
                .WithErrorMessage("La cédula de identidad es obligatoria.");
        }

        [Theory]
        [InlineData("123")]
        [InlineData("123456")]
        [InlineData("123456789")]
        public async Task Validate_CedulaFormatoInvalido_DeberiaFallar(string cedula)
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaFisica,
                Nombre = "Juan",
                Apellido = "Pérez",
                CedulaIdentidad = cedula,
                Telefono = "099123456",
                Domicilio = "Calle 123",
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.CedulaIdentidad)
                .WithErrorMessage("La cédula debe tener formato válido (ej: 1.234.567-8).");
        }

        [Fact]
        public async Task Validate_CedulaDuplicada_DeberiaFallar()
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaFisica,
                Nombre = "Juan",
                Apellido = "Pérez",
                CedulaIdentidad = "12345678",
                Telefono = "099123456",
                Domicilio = "Calle 123",
                Activo = true
            };

            _mockBusinessRules.Setup(br => br.IsCedulaUniqueAsync("12345678", null))
                .ReturnsAsync(false);

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.CedulaIdentidad)
                .WithErrorMessage("Ya existe un cliente con esta cédula de identidad.");
        }

        [Fact]
        public async Task Validate_PersonaFisicaConRUT_DeberiaFallar()
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaFisica,
                Nombre = "Juan",
                Apellido = "Pérez",
                CedulaIdentidad = "12345678",
                RUT = "123456789012",
                Telefono = "099123456",
                Domicilio = "Calle 123",
                Activo = true
            };

            _mockBusinessRules.Setup(br => br.IsCedulaUniqueAsync(It.IsAny<string>(), It.IsAny<int?>()))
                .ReturnsAsync(true);

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.RUT)
                .WithErrorMessage("El RUT debe estar vacío para una persona física.");
        }

        #endregion

        #region Persona Jurídica Tests

        [Fact]
        public async Task Validate_PersonaJuridicaCompleta_DeberiaSerExitoso()
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaJuridica,
                Nombre = "Empresa ABC S.A.",
                RUT = "123456789012",
                Telefono = "2601-1234",
                Domicilio = "Av. Principal 1234",
                Activo = true
            };

            _mockBusinessRules.Setup(br => br.IsRUTUniqueAsync("123456789012", null))
                .ReturnsAsync(true);

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public async Task Validate_PersonaJuridicaSinRazonSocial_DeberiaFallar()
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaJuridica,
                Nombre = "",
                RUT = "123456789012",
                Telefono = "2601-1234",
                Domicilio = "Av. Principal 1234",
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.Nombre)
                .WithErrorMessage("La Razón Social es obligatoria.");
        }

        [Theory]
        [InlineData("A")]
        [InlineData("")]
        public async Task Validate_RazonSocialMuyCorta_DeberiaFallar(string razonSocial)
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaJuridica,
                Nombre = razonSocial,
                RUT = "123456789012",
                Telefono = "2601-1234",
                Domicilio = "Av. Principal 1234",
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.Nombre);
        }

        [Fact]
        public async Task Validate_RazonSocialMuyLarga_DeberiaFallar()
        {
            // Arrange
            var razonSocialLarga = new string('A', 101);
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaJuridica,
                Nombre = razonSocialLarga,
                RUT = "123456789012",
                Telefono = "2601-1234",
                Domicilio = "Av. Principal 1234",
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.Nombre);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task Validate_RUTVacio_DeberiaFallar(string? rut)
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaJuridica,
                Nombre = "Empresa ABC S.A.",
                RUT = rut,
                Telefono = "2601-1234",
                Domicilio = "Av. Principal 1234",
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.RUT)
                .WithErrorMessage("El RUT es obligatorio.");
        }

        [Theory]
        [InlineData("12345678901")]
        [InlineData("1234567890123")]
        [InlineData("12345")]
        public async Task Validate_RUTFormatoInvalido_DeberiaFallar(string rut)
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaJuridica,
                Nombre = "Empresa ABC S.A.",
                RUT = rut,
                Telefono = "2601-1234",
                Domicilio = "Av. Principal 1234",
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.RUT)
                .WithErrorMessage("El RUT debe tener un formato válido (12 dígitos).");
        }

        [Theory]
        [InlineData("12345678901A")]
        [InlineData("ABC123456789")]
        public async Task Validate_RUTConLetras_DeberiaFallar(string rut)
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaJuridica,
                Nombre = "Empresa ABC S.A.",
                RUT = rut,
                Telefono = "2601-1234",
                Domicilio = "Av. Principal 1234",
                Activo = true
            };

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.RUT);
        }

        [Fact]
        public async Task Validate_RUTDuplicado_DeberiaFallar()
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaJuridica,
                Nombre = "Empresa ABC S.A.",
                RUT = "123456789012",
                Telefono = "2601-1234",
                Domicilio = "Av. Principal 1234",
                Activo = true
            };

            _mockBusinessRules.Setup(br => br.IsRUTUniqueAsync("123456789012", null))
                .ReturnsAsync(false);

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.RUT)
                .WithErrorMessage("Ya existe un cliente con este RUT.");
        }

        [Fact]
        public async Task Validate_PersonaJuridicaConCedula_DeberiaFallar()
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaJuridica,
                Nombre = "Empresa ABC S.A.",
                RUT = "123456789012",
                CedulaIdentidad = "12345678",
                Telefono = "2601-1234",
                Domicilio = "Av. Principal 1234",
                Activo = true
            };

            _mockBusinessRules.Setup(br => br.IsRUTUniqueAsync(It.IsAny<string>(), It.IsAny<int?>()))
                .ReturnsAsync(true);

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldHaveValidationErrorFor(c => c.CedulaIdentidad)
                .WithErrorMessage("La Cédula debe estar vacía para una persona jurídica.");
        }

        #endregion

        #region Nombres con Tildes y Caracteres Especiales

        [Theory]
        [InlineData("José")]
        [InlineData("María")]
        [InlineData("Raúl")]
        [InlineData("Ñoño")]
        public async Task Validate_NombreConTildes_DeberiaSerExitoso(string nombre)
        {
            // Arrange
            var cliente = new ClienteVM
            {
                Tipo = TipoCliente.PersonaFisica,
                Nombre = nombre,
                Apellido = "Pérez",
                CedulaIdentidad = "12345678",
                Telefono = "099123456",
                Domicilio = "Calle 123",
                Activo = true
            };

            _mockBusinessRules.Setup(br => br.IsCedulaUniqueAsync(It.IsAny<string>(), It.IsAny<int?>()))
                .ReturnsAsync(true);

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldNotHaveValidationErrorFor(c => c.Nombre);
        }

        #endregion

        #region Edición de Cliente

        [Fact]
        public async Task Validate_EditarClienteConMismaCedula_DeberiaSerExitoso()
        {
            // Arrange
            var cliente = new ClienteVM
            {
                ClienteId = 1,
                Tipo = TipoCliente.PersonaFisica,
                Nombre = "Juan",
                Apellido = "Pérez",
                CedulaIdentidad = "12345678",
                Telefono = "099123456",
                Domicilio = "Calle 123",
                Activo = true
            };

            _mockBusinessRules.Setup(br => br.IsCedulaUniqueAsync("12345678", 1))
                .ReturnsAsync(true);

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public async Task Validate_EditarClienteConMismoRUT_DeberiaSerExitoso()
        {
            // Arrange
            var cliente = new ClienteVM
            {
                ClienteId = 1,
                Tipo = TipoCliente.PersonaJuridica,
                Nombre = "Empresa ABC S.A.",
                RUT = "123456789012",
                Telefono = "2601-1234",
                Domicilio = "Av. Principal 1234",
                Activo = true
            };

            _mockBusinessRules.Setup(br => br.IsRUTUniqueAsync("123456789012", 1))
                .ReturnsAsync(true);

            // Act
            var result = await _validator.TestValidateAsync(cliente);

            // Assert
            result.ShouldNotHaveAnyValidationErrors();
        }

        #endregion
    }
}
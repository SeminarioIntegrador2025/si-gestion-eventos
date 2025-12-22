using FluentValidation.TestHelper;
using Moq;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;
using si_td_gestion_eventos.Validators;
using Xunit;

namespace si_td_gestion_eventos.Tests.Validators
{
    public class EventoValidatorTests
    {
        private readonly Mock<IEventoBusinessRules> _mockBusinessRules;
        private readonly EventoValidator _validator;

        public EventoValidatorTests()
        {
            _mockBusinessRules = new Mock<IEventoBusinessRules>();
            _validator = new EventoValidator(_mockBusinessRules.Object);
        }

        #region Cliente Tests

        [Fact]
        public async Task Validate_ClienteIdCero_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.ClienteId = 0;

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.ClienteId)
                .WithErrorMessage("Debe seleccionar un cliente.");
        }

        [Fact]
        public async Task Validate_ClienteInactivo_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.ClienteId = 1;

            _mockBusinessRules.Setup(br => br.IsClienteActiveAsync(1))
                .ReturnsAsync(false);

            // NO llamar a SetupDefaultMocks() o llamarlo con clienteActive: false
            SetupDefaultMocks(clienteActive: false);

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.ClienteId)
                .WithErrorMessage("El cliente seleccionado no está activo.");
        }

        [Fact]
        public async Task Validate_ClienteActivo_DeberiaSerExitoso()
        {
            // Arrange
            var evento = CreateValidEventoVM();

            _mockBusinessRules.Setup(br => br.IsClienteActiveAsync(evento.ClienteId))
                .ReturnsAsync(true);

            SetupDefaultMocks();

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldNotHaveValidationErrorFor(e => e.ClienteId);
        }

        #endregion

        #region Tipo de Evento Tests

        [Fact]
        public async Task Validate_TipoEventoNull_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.Tipo = null;

            // Configurar mocks mínimos para que solo falle el Tipo
            SetupDefaultMocks();

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.Tipo)
                .WithErrorMessage("Debe seleccionar un tipo de evento válido.");
        }

        [Theory]
        [InlineData(TipoEvento.Cumpleaños)]
        [InlineData(TipoEvento.Casamiento)]
        [InlineData(TipoEvento.Corporativo)]
        [InlineData(TipoEvento.Otro)]
        public async Task Validate_TipoEventoValido_DeberiaSerExitoso(TipoEvento tipo)
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.Tipo = tipo;

            SetupDefaultMocks();

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldNotHaveValidationErrorFor(e => e.Tipo);
        }

        #endregion

        #region Cantidad de Personas Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Validate_CantidadPersonasCeroONegativo_DeberiaFallar(int cantidad)
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.CantidadPersonas = cantidad;

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.CantidadPersonas)
                .WithErrorMessage("La cantidad de personas debe ser mayor a cero.");
        }

        [Fact]
        public async Task Validate_CantidadPersonasMayorA400_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.CantidadPersonas = 401;

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.CantidadPersonas)
                .WithErrorMessage("La cantidad de personas no puede exceder 400.");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(50)]
        [InlineData(200)]
        [InlineData(400)]
        public async Task Validate_CantidadPersonasValida_DeberiaSerExitoso(int cantidad)
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.CantidadPersonas = cantidad;

            SetupDefaultMocks();

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldNotHaveValidationErrorFor(e => e.CantidadPersonas);
        }

        #endregion

        #region Fechas Tests

        [Fact]
        public async Task Validate_FechaContratoVacia_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.FechaContrato = default;

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.FechaContrato)
                .WithErrorMessage("La fecha del contrato es obligatoria.");
        }

        [Fact]
        public async Task Validate_FechaInicioVacia_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.Inicio = default;

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.Inicio)
                .WithErrorMessage("La fecha de inicio es obligatoria.");
        }

        [Fact]
        public async Task Validate_FechaFinVacia_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.Fin = default;

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.Fin)
                .WithErrorMessage("La fecha de fin es obligatoria.");
        }

        [Fact]
        public async Task Validate_FechaFinAnteriorAInicio_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.Inicio = DateTime.Today.AddDays(10);
            evento.Fin = DateTime.Today.AddDays(9);

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.Fin)
                .WithErrorMessage("La fecha de fin no puede ser anterior a la fecha de inicio.");
        }

        #endregion

        #region Horas Tests

        [Fact]
        public async Task Validate_HoraInicioVacia_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.HoraInicio = null;

            // Configurar mocks para que pasen otras validaciones
            SetupDefaultMocks();

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.HoraInicio)
                .WithErrorMessage("La hora de inicio es obligatoria.");
        }

        [Fact]
        public async Task Validate_HoraFinVacia_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.HoraFin = null;

            // Configurar mocks para que pasen otras validaciones
            SetupDefaultMocks();

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.HoraFin)
                .WithErrorMessage("La hora de fin es obligatoria.");
        }

        [Fact]
        public async Task Validate_MismoDiaHoraFinAnteriorAInicio_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.Inicio = DateTime.Today.AddDays(10);
            evento.Fin = DateTime.Today.AddDays(10);
            evento.HoraInicio = TimeSpan.FromHours(20);
            evento.HoraFin = TimeSpan.FromHours(18);

            SetupDefaultMocks();

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.HoraFin)
                .WithErrorMessage("Si es el mismo día, la hora de fin debe ser posterior a la hora de inicio.");
        }

        [Fact]
        public async Task Validate_DiferentesDiasHoraFinAnterior_DeberiaSerExitoso()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.Inicio = DateTime.Today.AddDays(10);
            evento.Fin = DateTime.Today.AddDays(11);
            evento.HoraInicio = TimeSpan.FromHours(20);
            evento.HoraFin = TimeSpan.FromHours(2);

            SetupDefaultMocks();

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldNotHaveValidationErrorFor(e => e.HoraFin);
        }

        #endregion

        #region Costos Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-100)]
        public async Task Validate_CostoAlquilerCeroONegativo_DeberiaFallar(decimal costo)
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.CostoAlquiler = costo;

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.CostoAlquiler)
                .WithErrorMessage("El costo del alquiler debe ser mayor a cero.");
        }

        [Fact]
        public async Task Validate_MontoReservaNegativo_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.MontoReserva = -100;

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.MontoReserva)
                .WithErrorMessage("El monto de reserva no puede ser negativo.");
        }

        [Fact]
        public async Task Validate_MontoReservaMayorQueCosto_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.CostoAlquiler = 2000;
            evento.MontoReserva = 2500;

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.MontoReserva)
                .WithErrorMessage("El monto de reserva no puede ser mayor al costo del alquiler.");
        }

        [Fact]
        public async Task Validate_MontoAireAcondicionadoNegativo_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.MontoAireAcondicionado = -50;

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.MontoAireAcondicionado)
                .WithErrorMessage("El monto del aire acondicionado no puede ser negativo.");
        }

        #endregion

        #region Responsable Tests

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task Validate_ResponsableNombreVacio_DeberiaFallar(string? nombre)
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.ResponsableNombre = nombre!;

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.ResponsableNombre)
                .WithErrorMessage("El nombre del responsable es obligatorio.");
        }

        [Theory]
        [InlineData("A")]
        public async Task Validate_ResponsableNombreMuyCorto_DeberiaFallar(string nombre)
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.ResponsableNombre = nombre;

            // Configurar mocks mínimos
            _mockBusinessRules.Setup(br => br.IsClienteActiveAsync(It.IsAny<int>()))
                .ReturnsAsync(true);
            _mockBusinessRules.Setup(br => br.IsValidDateRangeAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()
            )).ReturnsAsync(true);
            _mockBusinessRules.Setup(br => br.IsDateRangeAvailableAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<int?>()
            )).ReturnsAsync(true);

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.ResponsableNombre)
                .WithErrorMessage("El nombre del responsable debe tener entre 2 y 100 caracteres.");
        }

        [Theory]
        [InlineData("Juan123")]
        [InlineData("Pedro@")]
        [InlineData("Ana#")]
        public async Task Validate_ResponsableNombreConCaracteresInvalidos_DeberiaFallar(string nombre)
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.ResponsableNombre = nombre;

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.ResponsableNombre)
                .WithErrorMessage("El nombre del responsable solo puede contener letras y espacios.");
        }

        [Fact]
        public async Task Validate_ResponsableNombreConEspaciosConsecutivos_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.ResponsableNombre = "Juan  Pérez";

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.ResponsableNombre)
                .WithErrorMessage("El nombre no puede contener espacios consecutivos.");
        }

        [Fact]
        public async Task Validate_ResponsableNombreConEspaciosAlPrincipio_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.ResponsableNombre = " Juan Pérez";

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.ResponsableNombre)
                .WithErrorMessage("El nombre no puede comenzar o terminar con espacios.");
        }

        [Theory]
        [InlineData("Juan Pérez")]
        [InlineData("María José")]
        [InlineData("José-Luis")]
        [InlineData("Ana O'Connor")]
        public async Task Validate_ResponsableNombreValido_DeberiaSerExitoso(string nombre)
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.ResponsableNombre = nombre;

            SetupDefaultMocks();

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldNotHaveValidationErrorFor(e => e.ResponsableNombre);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task Validate_ResponsableTelefonoVacio_DeberiaFallar(string? telefono)
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.ResponsableTelefono = telefono!;

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.ResponsableTelefono)
                .WithErrorMessage("El teléfono del responsable es obligatorio.");
        }

        [Theory]
        [InlineData("099123456")]
        [InlineData("(+598) 099-123-456")]
        [InlineData("2601-1234")]
        public async Task Validate_ResponsableTelefonoValido_DeberiaSerExitoso(string telefono)
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.ResponsableTelefono = telefono;

            SetupDefaultMocks();

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldNotHaveValidationErrorFor(e => e.ResponsableTelefono);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task Validate_ResponsableCedulaVacia_DeberiaFallar(string? cedula)
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.ResponsableCedula = cedula!;

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.ResponsableCedula)
                .WithErrorMessage("La cédula del responsable es obligatoria.");
        }

        [Theory]
        [InlineData("12345678")]
        [InlineData("1234567")]
        [InlineData("1.234.567-8")]
        public async Task Validate_ResponsableCedulaValida_DeberiaSerExitoso(string cedula)
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.ResponsableCedula = cedula;

            SetupDefaultMocks();

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldNotHaveValidationErrorFor(e => e.ResponsableCedula);
        }

        #endregion

        #region RuleSet Create Tests

        [Fact]
        public async Task ValidateCreate_FechaContratoFutura_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.FechaContrato = DateTime.Today.AddDays(1);

            SetupDefaultMocks();

            // Act
            var result = await _validator.TestValidateAsync(evento, options =>
            {
                options.IncludeRuleSets("Create");
            });

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.FechaContrato)
                .WithErrorMessage("La fecha del contrato no puede ser futura.");
        }

        [Fact]
        public async Task ValidateCreate_FechaInicioPasada_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.Inicio = DateTime.Today.AddDays(-1);
            evento.Fin = DateTime.Today.AddDays(-1);

            SetupDefaultMocks();

            // Act
            var result = await _validator.TestValidateAsync(evento, options =>
            {
                options.IncludeRuleSets("Create");
            });

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.Inicio)
                .WithErrorMessage("La fecha de inicio debe ser hoy o una fecha futura.");
        }

        [Fact]
        public async Task ValidateCreate_EventoHoyHoraPasada_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.Inicio = DateTime.Today;
            evento.Fin = DateTime.Today;
            evento.HoraInicio = DateTime.Now.TimeOfDay.Subtract(TimeSpan.FromHours(1));
            evento.HoraFin = DateTime.Now.TimeOfDay.Add(TimeSpan.FromHours(1));

            SetupDefaultMocks();

            // Act
            var result = await _validator.TestValidateAsync(evento, options =>
            {
                options.IncludeRuleSets("Create");
            });

            // Assert
            result.ShouldHaveValidationErrorFor(e => e.HoraInicio)
                .WithErrorMessage("Si el evento es hoy, la hora de inicio no puede ser anterior a la hora actual.");
        }

        [Fact]
        public async Task ValidateCreate_MontoReservaInsuficiente_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();
            evento.CostoAlquiler = 10000;
            evento.MontoReserva = 100;

            _mockBusinessRules.Setup(br => br.IsReservationAmountValidAsync(100, 10000))
                .ReturnsAsync(false);

            // Configurar otros mocks para que pasen las validaciones previas
            _mockBusinessRules.Setup(br => br.IsClienteActiveAsync(It.IsAny<int>()))
                .ReturnsAsync(true);
            _mockBusinessRules.Setup(br => br.IsValidDateRangeAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()
            )).ReturnsAsync(true);
            _mockBusinessRules.Setup(br => br.IsDateRangeAvailableAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<int?>()
            )).ReturnsAsync(true);

            // Act
            var result = await _validator.TestValidateAsync(evento, options =>
            {
                options.IncludeRuleSets("Create");
            });

            // Assert
            result.ShouldHaveValidationErrorFor(e => e)
                .WithErrorMessage("El monto de reserva no cumple con el mínimo requerido para el costo del alquiler.");
        }

        #endregion

        #region Disponibilidad Tests

        [Fact]
        public async Task Validate_RangoFechasInvalido_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();

            _mockBusinessRules.Setup(br => br.IsValidDateRangeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>()
            )).ReturnsAsync(false);

            SetupDefaultMocks(validateDateRange: false);

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e)
                .WithErrorMessage("El rango de fechas y horarios no es válido (ej: duración negativa o excesiva).");
        }

        [Fact]
        public async Task Validate_HorarioNoDisponible_DeberiaFallar()
        {
            // Arrange
            var evento = CreateValidEventoVM();

            _mockBusinessRules.Setup(br => br.IsDateRangeAvailableAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int?>()
            )).ReturnsAsync(false);

            SetupDefaultMocks(dateRangeAvailable: false);

            // Act
            var result = await _validator.TestValidateAsync(evento);

            // Assert
            result.ShouldHaveValidationErrorFor(e => e)
                .WithErrorMessage("Ya existe otro evento programado en este horario. Por favor, seleccione otra fecha u horario.");
        }

        #endregion

        #region Helper Methods

        private EventoVM CreateValidEventoVM()
        {
            return new EventoVM
            {
                EventoId = 0,
                ClienteId = 1,
                Tipo = TipoEvento.Cumpleaños,
                FechaContrato = DateTime.Today,
                Inicio = DateTime.Today.AddDays(10),
                Fin = DateTime.Today.AddDays(10),
                HoraInicio = TimeSpan.FromHours(18),
                HoraFin = TimeSpan.FromHours(23),
                CantidadPersonas = 50,
                CostoAlquiler = 2000,
                MontoReserva = 500,
                MontoAireAcondicionado = 0,
                ResponsableNombre = "Juan Pérez",
                ResponsableTelefono = "099123456",
                ResponsableCedula = "12345678"
            };
        }

        private void SetupDefaultMocks(
            bool clienteActive = true,
            bool validateDateRange = true,
            bool dateRangeAvailable = true,
            bool reservationValid = true)
        {
            // Setup SIEMPRE, incluso si no se llama
            _mockBusinessRules.Setup(br => br.IsClienteActiveAsync(It.IsAny<int>()))
        .ReturnsAsync(clienteActive);

            _mockBusinessRules.Setup(br => br.IsValidDateRangeAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>()
            )).ReturnsAsync(validateDateRange);

            _mockBusinessRules.Setup(br => br.IsDateRangeAvailableAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<int?>()
            )).ReturnsAsync(dateRangeAvailable);

            _mockBusinessRules.Setup(br => br.IsReservationAmountValidAsync(
                It.IsAny<decimal>(),
                It.IsAny<decimal>()
            )).ReturnsAsync(reservationValid);
        }

        #endregion
    }
}
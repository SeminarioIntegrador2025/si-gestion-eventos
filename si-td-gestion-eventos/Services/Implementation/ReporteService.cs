using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Context;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Contracts;

namespace si_td_gestion_eventos.Services.Implementation
{
    public class ReporteService : IReporteService
    {
        private readonly AppDbContext _context;

        public ReporteService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ReportesVM> GetReportesConsolidadosAsync()
        {
            var reportes = new ReportesVM
            {
                Eventos = await GetReporteEventosAsync(),
                Ingresos = await GetReporteIngresosAsync(),
                Pagos = await GetReportePagosAsync(),
                Fianzas = await GetReporteFianzasAsync(),
                Clientes = await GetReporteClientesAsync(),
                TopClientes = await GetTopClientesAsync(),
                EventosPorTipo = await GetEventosPorTipoAsync()
            };

            return reportes;
        }

        public async Task<ReporteEventosVM> GetReporteEventosAsync()
        {
            var hoy = DateTime.Today;
            var inicioMesActual = new DateTime(hoy.Year, hoy.Month, 1);
            var finMesActual = inicioMesActual.AddMonths(1).AddDays(-1);
            var inicioMesAnterior = inicioMesActual.AddMonths(-1);
            var finMesAnterior = inicioMesActual.AddDays(-1);
            var inicioProximoMes = inicioMesActual.AddMonths(1);
            var finProximoMes = inicioProximoMes.AddMonths(1).AddDays(-1);

            var eventosEsteMes = await _context.Evento
                .Where(e => e.Inicio >= inicioMesActual && e.Inicio <= finMesActual)
                .ToListAsync();

            var eventosMesAnterior = await _context.Evento
                .Where(e => e.Inicio >= inicioMesAnterior && e.Inicio <= finMesAnterior)
                .CountAsync();

            var eventosProximoMes = await _context.Evento
                .Where(e => e.Inicio >= inicioProximoMes && e.Inicio <= finProximoMes)
                .CountAsync();

            var totalEsteMes = eventosEsteMes.Count;
            var realizados = eventosEsteMes.Count(e => e.Estado == EventoEstado.Realizado);
            var pendientes = eventosEsteMes.Count(e => e.Estado != EventoEstado.Realizado && e.Estado != EventoEstado.Cancelado);
            var cancelados = eventosEsteMes.Count(e => e.Estado == EventoEstado.Cancelado);

            var porcentajeCambio = eventosMesAnterior > 0
                ? (decimal)(totalEsteMes - eventosMesAnterior) / eventosMesAnterior * 100
                : 0;

            // Calcular ocupación general
            var diasMes = DateTime.DaysInMonth(hoy.Year, hoy.Month);
            var diasOcupados = eventosEsteMes
                .SelectMany(e => Enumerable.Range(0, (e.Fin - e.Inicio).Days + 1)
                    .Select(offset => e.Inicio.AddDays(offset).Date))
                .Distinct()
                .Count();

            var tasaOcupacion = diasMes > 0 ? (decimal)diasOcupados / diasMes * 100 : 0;

            // ====== NUEVO: Calcular ocupación de fines de semana ======
            var finesDeSemanaOcupados = CalcularFinesDeSemanaOcupados(eventosEsteMes);
            var totalFinesDeSemana = CalcularTotalFinesDeSemana(inicioMesActual, finMesActual);
            var tasaOcupacionFinDeSemana = totalFinesDeSemana > 0 
                ? (decimal)finesDeSemanaOcupados / totalFinesDeSemana * 100 
                : 0;

            return new ReporteEventosVM
            {
                EventosEsteMes = totalEsteMes,
                EventosRealizadosEsteMes = realizados,
                EventosPendientesEsteMes = pendientes,
                EventosCanceladosEsteMes = cancelados,
                EventosMesAnterior = eventosMesAnterior,
                PorcentajeCambioEventos = Math.Round(porcentajeCambio, 2),
                CrecimientoPositivo = porcentajeCambio >= 0,
                EventosProximoMes = eventosProximoMes,
                TasaOcupacionEsteMes = Math.Round(tasaOcupacion, 2),
                DiasDisponiblesEsteMes = diasMes - diasOcupados,
                TasaOcupacionFinesDeSemana = Math.Round(tasaOcupacionFinDeSemana, 2),
                FinesDeSemanaOcupados = finesDeSemanaOcupados,
                TotalFinesDeSemanaDisponibles = totalFinesDeSemana
            };
        }

        public async Task<ReporteIngresosVM> GetReporteIngresosAsync()
        {
            var hoy = DateTime.Today;
            var inicioMesActual = new DateTime(hoy.Year, hoy.Month, 1);
            var inicioMesAnterior = inicioMesActual.AddMonths(-1);
            var inicioAnio = new DateTime(hoy.Year, 1, 1);

            // ====== CORREGIDO: Ingresos basados en PAGOS RECIBIDOS ======
            // Obtener pagos del mes actual con información del evento
            var pagosEsteMes = await _context.Pago
                .Include(p => p.Evento)
                .Where(p => p.Fecha >= inicioMesActual && p.Evento.Estado != EventoEstado.Cancelado)
                .ToListAsync();

            // Clasificar pagos por concepto basándose en la proporción del evento
            decimal totalReservas = 0;
            decimal totalAlquileres = 0;
            decimal totalAire = 0;

            foreach (var pago in pagosEsteMes)
            {
                var evento = pago.Evento;
                var totalEvento = (decimal)(evento.MontoReserva + evento.CostoAlquiler + (evento.MontoAireAcondicionado ?? 0));
                
                if (totalEvento > 0)
                {
                    // Distribuir el pago proporcionalmente
                    var proporcionReserva = (decimal)evento.MontoReserva / totalEvento;
                    var proporcionAlquiler = (decimal)evento.CostoAlquiler / totalEvento;
                    var proporcionAire = evento.MontoAireAcondicionado.HasValue 
                        ? (decimal)evento.MontoAireAcondicionado.Value / totalEvento 
                        : 0;

                    totalReservas += (decimal)pago.Monto * proporcionReserva;
                    totalAlquileres += (decimal)pago.Monto * proporcionAlquiler;
                    totalAire += (decimal)pago.Monto * proporcionAire;
                }
            }

            var totalEsteMes = totalReservas + totalAlquileres + totalAire;

            // Mes anterior
            var pagosMesAnterior = await _context.Pago
                .Include(p => p.Evento)
                .Where(p => p.Fecha >= inicioMesAnterior && p.Fecha < inicioMesActual && p.Evento.Estado != EventoEstado.Cancelado)
                .ToListAsync();

            var totalMesAnterior = pagosMesAnterior.Sum(p => (decimal)p.Monto);

            // Año actual
            var pagosAnio = await _context.Pago
                .Include(p => p.Evento)
                .Where(p => p.Fecha >= inicioAnio && p.Evento.Estado != EventoEstado.Cancelado)
                .ToListAsync();

            var totalAnio = pagosAnio.Sum(p => (decimal)p.Monto);
            var mesesTranscurridos = hoy.Month;
            var promedioMensual = mesesTranscurridos > 0 ? totalAnio / mesesTranscurridos : 0;

            var porcentajeCambio = totalMesAnterior > 0
                ? (totalEsteMes - totalMesAnterior) / totalMesAnterior * 100
                : 0;

            return new ReporteIngresosVM
            {
                TotalIngresosEsteMes = Math.Round(totalEsteMes, 2),
                TotalReservasEsteMes = Math.Round(totalReservas, 2),
                TotalAlquileresEsteMes = Math.Round(totalAlquileres, 2),
                TotalAireAcondicionadoEsteMes = Math.Round(totalAire, 2),
                TotalIngresosMesAnterior = Math.Round(totalMesAnterior, 2),
                PorcentajeCambioIngresos = Math.Round(porcentajeCambio, 2),
                CrecimientoPositivo = porcentajeCambio >= 0,
                TotalIngresosAnioActual = Math.Round(totalAnio, 2),
                PromedioIngresosMensual = Math.Round(promedioMensual, 2),
                PorcentajeReservas = totalEsteMes > 0 ? Math.Round(totalReservas / totalEsteMes * 100, 2) : 0,
                PorcentajeAlquileres = totalEsteMes > 0 ? Math.Round(totalAlquileres / totalEsteMes * 100, 2) : 0,
                PorcentajeAireAcondicionado = totalEsteMes > 0 ? Math.Round(totalAire / totalEsteMes * 100, 2) : 0
            };
        }

        public async Task<ReportePagosVM> GetReportePagosAsync()
        {
            var hoy = DateTime.Today;
            var inicioMesActual = new DateTime(hoy.Year, hoy.Month, 1);
            var inicioMesAnterior = inicioMesActual.AddMonths(-1);

            var pagosEsteMes = await _context.Pago
                .Where(p => p.Fecha >= inicioMesActual)
                .ToListAsync();

            var pagosMesAnterior = await _context.Pago
                .Where(p => p.Fecha >= inicioMesAnterior && p.Fecha < inicioMesActual)
                .CountAsync();

            var totalPagadoEsteMes = pagosEsteMes.Sum(p => (decimal)p.Monto);
            var pagosEfectivo = pagosEsteMes.Where(p => p.Metodo == MetodoPago.Efectivo).ToList();
            var pagosTransferencia = pagosEsteMes.Where(p => p.Metodo == MetodoPago.Transferencia).ToList();

            // Calcular adeudos
            var eventos = await _context.Evento
                .Include(e => e.Pagos)
                .Where(e => e.Estado != EventoEstado.Cancelado)
                .ToListAsync();

            var totalAdeudado = eventos.Sum(e =>
            {
                var totalEvento = (decimal)(e.MontoReserva + e.CostoAlquiler + (e.MontoAireAcondicionado ?? 0));
                var totalPagado = e.Pagos.Sum(p => (decimal)p.Monto);
                return Math.Max(0, totalEvento - totalPagado);
            });

            var eventosConAdeudo = eventos.Count(e =>
            {
                var totalEvento = (decimal)(e.MontoReserva + e.CostoAlquiler + (e.MontoAireAcondicionado ?? 0));
                var totalPagado = e.Pagos.Sum(p => (decimal)p.Monto);
                return totalEvento > totalPagado;
            });

            var totalEsperado = eventos.Sum(e => (decimal)(e.MontoReserva + e.CostoAlquiler + (e.MontoAireAcondicionado ?? 0)));
            var totalPagadoGeneral = eventos.Sum(e => e.Pagos.Sum(p => (decimal)p.Monto));
            var porcentajeRecuperacion = totalEsperado > 0 ? totalPagadoGeneral / totalEsperado * 100 : 0;

            var porcentajeCambio = pagosMesAnterior > 0
                ? (decimal)(pagosEsteMes.Count - pagosMesAnterior) / pagosMesAnterior * 100
                : 0;

            return new ReportePagosVM
            {
                TotalPagosEsteMes = pagosEsteMes.Count,
                MontoTotalPagadoEsteMes = Math.Round(totalPagadoEsteMes, 2),
                PagosEfectivoEsteMes = pagosEfectivo.Count,
                MontoEfectivoEsteMes = Math.Round(pagosEfectivo.Sum(p => (decimal)p.Monto), 2),
                PagosTransferenciaEsteMes = pagosTransferencia.Count,
                MontoTransferenciaEsteMes = Math.Round(pagosTransferencia.Sum(p => (decimal)p.Monto), 2),
                TotalPagosMesAnterior = pagosMesAnterior,
                PorcentajeCambioPagos = Math.Round(porcentajeCambio, 2),
                TotalAdeudado = Math.Round(totalAdeudado, 2),
                EventosConAdeudo = eventosConAdeudo,
                PorcentajeRecuperacion = Math.Round(porcentajeRecuperacion, 2)
            };
        }

        public async Task<ReporteFianzasVM> GetReporteFianzasAsync()
        {
            var hoy = DateTime.Today;
            var inicioMesActual = new DateTime(hoy.Year, hoy.Month, 1);

            var todasFianzas = await _context.Fianza.ToListAsync();
            var fianzasEsteMes = todasFianzas.Where(f => f.FechaRegistro >= inicioMesActual).ToList();

            // ====== CORREGIDO: Cálculos de fianzas ======
            var totalRegistradas = todasFianzas.Sum(f => f.Monto);
            
            // Total devuelto: suma de MontoDevuelto de todas las fianzas (independiente del estado)
            var totalDevueltas = todasFianzas.Sum(f => f.MontoDevuelto);
            
            // Pendientes de devolución: Monto original menos lo devuelto, solo para fianzas no completamente devueltas
            var totalPendientesDevolucion = todasFianzas
                .Where(f => f.Estado == EstadoFianza.Registrada || f.Estado == EstadoFianza.DevueltaParcialmente)
                .Sum(f => f.Monto - f.MontoDevuelto);

            var porcentajeDevolucion = totalRegistradas > 0 ? totalDevueltas / totalRegistradas * 100 : 0;
            var montoPromedio = todasFianzas.Count > 0 ? todasFianzas.Average(f => f.Monto) : 0;

            // Fianzas vencidas: registradas o parcialmente devueltas con fecha de devolución pasada
            var fianzasVencidas = todasFianzas.Count(f => 
                (f.Estado == EstadoFianza.Registrada || f.Estado == EstadoFianza.DevueltaParcialmente) 
                && f.FechaDevolucion < hoy);

            return new ReporteFianzasVM
            {
                TotalFianzasRegistradas = Math.Round(totalRegistradas, 2),
                TotalFianzasDevueltas = Math.Round((decimal)totalDevueltas, 2),
                TotalFianzasPendientesDevolucion = Math.Round((decimal)totalPendientesDevolucion, 2),
                FianzasRegistradasEsteMes = fianzasEsteMes.Count,
                FianzasDevueltasEsteMes = fianzasEsteMes.Count(f => 
                    f.Estado == EstadoFianza.DevueltaTotalmente || 
                    f.Estado == EstadoFianza.DevueltaParcialmente),
                PorcentajeDevolucionTotal = Math.Round((decimal)porcentajeDevolucion, 2),
                MontoPromedioFianza = Math.Round(montoPromedio, 2),
                FianzasVencidas = fianzasVencidas
            };
        }

        public async Task<ReporteClientesVM> GetReporteClientesAsync()
        {
            var hoy = DateTime.Today;
            var inicioMesActual = new DateTime(hoy.Year, hoy.Month, 1);

            var clientes = await _context.Cliente
                .Include(c => c.Eventos)
                .ToListAsync();

            var clientesActivos = clientes.Count(c => c.Activo);
            var clientesInactivos = clientes.Count(c => !c.Activo);

            // Clientes nuevos: primera vez que contrataron fue este mes
            var nuevosClientes = clientes.Count(c => 
                c.Eventos.Any() && c.Eventos.Min(e => e.FechaContrato) >= inicioMesActual);

            // Clientes recurrentes: tienen eventos anteriores y contrataron este mes
            var recurrentes = clientes.Count(c => 
                c.Eventos.Any(e => e.FechaContrato < inicioMesActual) && 
                c.Eventos.Any(e => e.FechaContrato >= inicioMesActual));

            var totalEventos = clientes.Sum(c => c.Eventos.Count);
            var promedioEventos = clientes.Count > 0 ? (decimal)totalEventos / clientes.Count : 0;

            var totalClientesConEventosEsteMes = nuevosClientes + recurrentes;
            var porcentajeRecurrencia = totalClientesConEventosEsteMes > 0 
                ? (decimal)recurrentes / totalClientesConEventosEsteMes * 100 
                : 0;

            return new ReporteClientesVM
            {
                TotalClientes = clientes.Count,
                ClientesActivos = clientesActivos,
                ClientesInactivos = clientesInactivos,
                NuevosClientesEsteMes = nuevosClientes,
                ClientesRecurrentesEsteMes = recurrentes,
                PorcentajeRecurrencia = Math.Round(porcentajeRecurrencia, 2),
                PromedioEventosPorCliente = Math.Round(promedioEventos, 2)
            };
        }

        private async Task<List<TopClienteVM>> GetTopClientesAsync()
        {
            var clientes = await _context.Cliente
                .Include(c => c.Eventos.Where(e => e.Estado != EventoEstado.Cancelado))
                .ThenInclude(e => e.Pagos)
                .Where(c => c.Eventos.Any(e => e.Estado != EventoEstado.Cancelado))
                .ToListAsync();

            var topClientes = clientes
                .Select(c => new TopClienteVM
                {
                    ClienteId = c.ClienteId,
                    NombreCompleto = c.Tipo == TipoCliente.PersonaFisica 
                        ? $"{c.Nombre} {c.Apellido}".Trim() 
                        : c.Nombre,
                    TotalEventos = c.Eventos.Count,
                    // Total ingresos basado en pagos recibidos
                    TotalIngresos = c.Eventos.Sum(e => e.Pagos.Sum(p => (decimal)p.Monto)),
                    PromedioGasto = c.Eventos.Count > 0 
                        ? c.Eventos.Average(e => e.Pagos.Sum(p => (decimal)p.Monto))
                        : 0,
                    UltimoEvento = c.Eventos.Any() ? c.Eventos.Max(e => e.Inicio) : DateTime.MinValue
                })
                .OrderByDescending(c => c.TotalIngresos)
                .Take(5)
                .ToList();

            return topClientes;
        }

        private async Task<List<EventoPorTipoVM>> GetEventosPorTipoAsync()
        {
            var eventos = await _context.Evento
                .Include(e => e.Pagos)
                .Where(e => e.Estado != EventoEstado.Cancelado)
                .ToListAsync();

            var totalEventos = eventos.Count;

            var eventosPorTipo = eventos
                .GroupBy(e => e.Tipo)
                .Select(g => new EventoPorTipoVM
                {
                    TipoEvento = g.Key.ToString(),
                    Cantidad = g.Count(),
                    PorcentajeDelTotal = totalEventos > 0 ? Math.Round((decimal)g.Count() / totalEventos * 100, 2) : 0,
                    // Ingresos basados en pagos recibidos
                    IngresoTotal = Math.Round(g.Sum(e => e.Pagos.Sum(p => (decimal)p.Monto)), 2)
                })
                .OrderByDescending(e => e.Cantidad)
                .ToList();

            return eventosPorTipo;
        }

        // ====== MÉTODOS AUXILIARES PARA FINES DE SEMANA ======
        
        /// <summary>
        /// Calcula cuántos fines de semana están ocupados por eventos
        /// </summary>
        private int CalcularFinesDeSemanaOcupados(List<Evento> eventos)
        {
            var finesDeSemanaConEventos = new HashSet<DateTime>();

            foreach (var evento in eventos.Where(e => e.Estado != EventoEstado.Cancelado))
            {
                // Obtener todos los días del evento
                var diasEvento = Enumerable.Range(0, (evento.Fin - evento.Inicio).Days + 1)
                    .Select(offset => evento.Inicio.AddDays(offset).Date);

                // Filtrar solo sábados y domingos
                foreach (var dia in diasEvento)
                {
                    if (dia.DayOfWeek == DayOfWeek.Saturday || dia.DayOfWeek == DayOfWeek.Sunday)
                    {
                        // Agrupar por fin de semana (usar el sábado como identificador)
                        var sabado = dia.DayOfWeek == DayOfWeek.Saturday 
                            ? dia 
                            : dia.AddDays(-1);
                        finesDeSemanaConEventos.Add(sabado);
                    }
                }
            }

            return finesDeSemanaConEventos.Count;
        }

        /// <summary>
        /// Calcula el total de fines de semana en un rango de fechas
        /// </summary>
        private int CalcularTotalFinesDeSemana(DateTime inicio, DateTime fin)
        {
            var finesDeSemana = new HashSet<DateTime>();

            for (var fecha = inicio; fecha <= fin; fecha = fecha.AddDays(1))
            {
                if (fecha.DayOfWeek == DayOfWeek.Saturday || fecha.DayOfWeek == DayOfWeek.Sunday)
                {
                    var sabado = fecha.DayOfWeek == DayOfWeek.Saturday 
                        ? fecha 
                        : fecha.AddDays(-1);
                    finesDeSemana.Add(sabado);
                }
            }

            return finesDeSemana.Count;
        }
    }
}
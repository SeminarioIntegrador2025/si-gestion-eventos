using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.PDFTemplates;
using si_td_gestion_eventos.Services.Contracts;
using si_td_gestion_eventos.Services.Implementation;
using System.IO.Compression;
using System.IO.Compression;

namespace si_td_gestion_eventos.Controllers
{
    public class ReporteController : Controller
    {
        private readonly IReporteService _reporteService;

        public ReporteController(IReporteService reporteService)
        {
            _reporteService = reporteService;
        }

        // GET: Reporte/Index
        public async Task<IActionResult> Index()
        {
            var reportes = await _reporteService.GetReportesConsolidadosAsync();
            return View(reportes);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarEventos(string q)
        {
            var eventos = await _reporteService.BuscarEventosParaModalAsync(q);

            var resultados = eventos.Select(e => new
            {
                idEvento = e.EventoId,

                // LÓGICA CORREGIDA:
                // 1. Si e.Cliente es null (por error técnico), mostramos "Sin Datos" para no romper la app.
                // 2. Si es Física: Nombre + Apellido.
                // 3. Si es Jurídica: Solo Nombre (Razón Social).
                cliente = e.Cliente == null ? "---" :
                          (e.Cliente.Tipo == TipoCliente.PersonaFisica
                              ? $"{e.Cliente.Nombre} {e.Cliente.Apellido}".Trim()
                              : e.Cliente.Nombre), // Jurídica usa solo Nombre

                tipo = e.Tipo.ToString(),
                fecha = e.Inicio.ToString("dd/MM/yyyy"),
                estado = e.Estado.ToString()
            });

            return Json(resultados);
        }
        // GET: Reporte/DescargarPdfIndividual/5
        [HttpGet]
        public async Task<IActionResult> DescargarPdfIndividual(int id)
        {
            // 1. Reutilizamos la lógica del servicio que ya creamos
            var datosReporte = await _reporteService.GenerarFichaCompletaAsync(id);

            if (datosReporte == null)
            {
                return NotFound(); // O redirigir con un mensaje de error
            }

            // 2. Generamos el PDF usando QuestPDF (igual que en el ZIP)
            var documento = new ReporteEventoDocument(datosReporte);
            byte[] pdfBytes = documento.GeneratePdf();

            // 3. Devolver archivo PDF directo (no ZIP)
            // Limpiamos el nombre del archivo para evitar caracteres inválidos
            string nombreClienteLimpio = datosReporte.NombreCliente.Replace(" ", "_");
            string nombreArchivo = $"Ficha_{nombreClienteLimpio}_{id}.pdf";

            return File(pdfBytes, "application/pdf", nombreArchivo);
        }

        [HttpPost]
        public async Task<IActionResult> DescargarReportes(List<int> eventosSeleccionados, string formato)
        {
            if (eventosSeleccionados == null || !eventosSeleccionados.Any())
                return RedirectToAction("Index");

            if (formato == "Excel")
            {
                return Content("Implementación Excel pendiente");
            }

            // LÓGICA PDF (QuestPDF)
            using (var memoryStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    foreach (var id in eventosSeleccionados)
                    {
                        // CORRECCIÓN AQUÍ: Llamada asíncrona correcta
                        var datosReporte = await _reporteService.GenerarFichaCompletaAsync(id);

                        if (datosReporte == null) continue;

                        // Generar PDF
                        var documento = new ReporteEventoDocument(datosReporte);
                        byte[] pdfBytes = documento.GeneratePdf();

                        // Agregar al ZIP
                        string nombreLimpio = datosReporte.NombreCliente.Replace(" ", "_");
                        var nombreArchivo = $"Reporte_{nombreLimpio}_{id}.pdf";

                        var entry = archive.CreateEntry(nombreArchivo);

                        using (var entryStream = entry.Open())
                        using (var fileStream = new MemoryStream(pdfBytes))
                        {
                            await fileStream.CopyToAsync(entryStream);
                        }
                    }
                }

                memoryStream.Position = 0;
                return File(memoryStream.ToArray(), "application/zip", $"Reportes_{DateTime.Now:ddMMyy}.zip");
            }
        }
    }
}
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using si_td_gestion_eventos.Models.ViewModels; 
namespace si_td_gestion_eventos.PDFTemplates
{
    public class ReporteEventoDocument : IDocument
    {
        private readonly ReporteCompletoViewModel _model;
        private readonly string _logoPath;

        public ReporteEventoDocument(ReporteCompletoViewModel model)
        {
            _model = model;
            // Usamos la misma lógica del logo que en tu recibo
            _logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/logo.png");
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
        public DocumentSettings GetSettings() => DocumentSettings.Default;

        public void Compose(IDocumentContainer container)
        {
            container
                .Page(page =>
                {
                    page.Margin(40);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.TimesNewRoman));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(ComposeContent);
                    page.Footer().Element(ComposeFooter);
                });
        }

        // --- 1. CABECERA ---
        void ComposeHeader(IContainer container)
        {
            container.Row(row =>
            {
                // Logo a la izquierda
                if (File.Exists(_logoPath))
                {
                    row.ConstantItem(100).Image(_logoPath).FitWidth();
                }

                // Título a la derecha
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(_model.TituloReporte).FontSize(20).SemiBold();
                    col.Item().Text($"Generado el: {_model.FechaGeneracion:dd/MM/yyyy HH:mm}").FontSize(10).FontColor(Colors.Grey.Medium);
                    col.Item().Text("Salón de Eventos \"Tata David\"").FontSize(12).Bold();
                });
            });
        }

        // --- 2. CONTENIDO PRINCIPAL ---
        void ComposeContent(IContainer container)
        {
            container.PaddingVertical(20).Column(col =>
            {
                // Sección A: Datos Generales
                col.Item().Element(c => SectionTitle(c, "Información General"));
                col.Item().Element(ComposeDatosGenerales);

                // Sección B: Responsable
                col.Item().PaddingTop(10).Element(c => SectionTitle(c, "Responsable del Salón"));
                col.Item().Element(ComposeResponsable);

                // Sección C: Historial de Pagos
                col.Item().PaddingTop(10).Element(c => SectionTitle(c, "Historial de Pagos"));
                col.Item().Element(ComposeHistorialPagos);

                col.Item().PaddingTop(20).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                // Sección D: Resumen Financiero y Fianza (Lado a lado)
                col.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Element(ComposeFianza); // Izquierda
                    row.ConstantItem(20); // Espacio
                    row.RelativeItem().Element(ComposeResumenFinanciero); // Derecha
                });
            });
        }

        // --- MÉTODOS AUXILIARES DE DISEÑO ---

        void SectionTitle(IContainer container, string title)
        {
            container.PaddingBottom(5).BorderBottom(1).BorderColor(Colors.Blue.Medium).Row(row =>
            {
                row.RelativeItem().Text(title).FontSize(14).SemiBold().FontColor(Colors.Blue.Darken2);
            });
        }

        void ComposeDatosGenerales(IContainer container)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(100); // Etiqueta
                    columns.RelativeColumn();    // Valor
                    columns.ConstantColumn(100); // Etiqueta
                    columns.RelativeColumn();    // Valor
                });
                // Fila 1: Cliente y Documento
                table.Cell().Text("Cliente:").Bold();
                table.Cell().Text(_model.NombreCliente);
                table.Cell().Text("Evento:").Bold();
                table.Cell().Text(_model.TipoEvento);

                table.Cell().Text($"{_model.LabelDocumento}:").Bold();
                table.Cell().Text(_model.CI_RUT);
                table.Cell().Text("F. Evento:").Bold();
                table.Cell().Text($"{_model.FechaEvento} ({_model.Horario})");

      
                table.Cell().Text("Tel. Cliente:").Bold();
                table.Cell().Text(_model.TelefonoCliente);
                table.Cell().Text("Invitados:").Bold();
                table.Cell().Text($"{_model.CantidadInvitados} personas");

                table.Cell().Text("F. Contrato:").Bold();
                table.Cell().Text(_model.FechaContrato);
                table.Cell().ColumnSpan(2); // Espacio vacío para completar la fila
            });
        }

        void ComposeResponsable(IContainer container)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(100);
                    columns.RelativeColumn();
                });

                table.Cell().Text("Nombre:").Bold();
                table.Cell().Text(_model.ResponsableNombre);
                table.Cell().Text("C.I.:").Bold();
                table.Cell().Text(_model.ResponsableCI);
                table.Cell().Text("Contacto:").Bold();
                table.Cell().Text(_model.ResponsableTelefono);
            });
        }

        void ComposeHistorialPagos(IContainer container)
        {
            if (_model.HistorialPagos != null && _model.HistorialPagos.Any())
            {
                container.Table(table =>
                {
                    // Definición de columnas
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(80); // Fecha
                        columns.ConstantColumn(100); // Método
                        columns.RelativeColumn();    // Obs
                        columns.ConstantColumn(80); // Monto
                    });

                    // Cabecera de Tabla
                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("Fecha");
                        header.Cell().Element(CellStyle).Text("Método");
                        header.Cell().Element(CellStyle).Text("Observación");
                        header.Cell().Element(CellStyle).AlignRight().Text("Monto");

                        static IContainer CellStyle(IContainer container)
                        {
                            return container.DefaultTextStyle(x => x.SemiBold().Color(Colors.White))
                                            .Background(Colors.Blue.Medium)
                                            .Padding(5);
                        }
                    });

                    // Filas
                    foreach (var pago in _model.HistorialPagos)
                    {
                        // Definimos el color según si es anulado
                        var colorTexto = pago.EsAnulado ? Colors.Red.Medium : Colors.Black;

                        // Si está anulado, agregamos "(Anulado)" al método para mayor claridad
                        var textoMetodo = pago.EsAnulado ? $"{pago.Metodo} (Anulado)" : pago.Metodo;

                        table.Cell().Element(CellStyle).Text(pago.Fecha).FontColor(colorTexto);
                        table.Cell().Element(CellStyle).Text(textoMetodo).FontColor(colorTexto);
                        table.Cell().Element(CellStyle).Text(pago.Observacion).FontColor(colorTexto);

                        // === AQUÍ ESTÁ EL CAMBIO CLAVE PARA QUE TACHE Y COMPILE ===
                        // Usamos una función lambda (txt => ...) para configurar el estilo
                        table.Cell().Element(CellStyle).AlignRight().Text(txt =>
                        {
                            var span = txt.Span($"{pago.Monto:C}").FontColor(colorTexto);

                            // Si es anulado, aplicamos el tachado al span directamente
                            if (pago.EsAnulado)
                            {
                                span.Strikethrough();
                            }
                        });

                        static IContainer CellStyle(IContainer container)
                        {
                            return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
                        }
                    }
                });
            }
            else
            {
                container.Text("No hay pagos registrados.").FontColor(Colors.Grey.Medium).Italic();
            }
        }

        void ComposeFianza(IContainer container)
        {
            container.Background(Colors.Grey.Lighten4).Padding(10).Column(col =>
            {
                col.Item().Text("Garantía (Fianza)").Bold().FontSize(12);
                col.Item().PaddingTop(5).Text($"Estado: {_model.EstadoFianza}");
                col.Item().Text($"Monto Retenido: {_model.MontoFianza:C}").Bold();
                if (_model.ObservacionesFianza != "-")
                {
                    col.Item().PaddingTop(5).Text($"Obs: {_model.ObservacionesFianza}").FontSize(9);
                }
                col.Item().PaddingTop(10).Text("Nota: Este monto es independiente del alquiler y se gestiona por separado.").FontSize(8).Italic();
            });
        }

        void ComposeResumenFinanciero(IContainer container)
        {
            container.Padding(10).Border(1).BorderColor(Colors.Grey.Lighten2).Column(col =>
            {
                col.Item().Text("Resumen Financiero").Bold().FontSize(12).AlignRight();

                col.Item().PaddingTop(5).Row(row => {
                    row.RelativeItem().Text("Alquiler Base:");
                    row.RelativeItem().AlignRight().Text($"{_model.CostoAlquilerBase:C}");
                });

                col.Item().Row(row => {
                    row.RelativeItem().Text("Aire Acond.:");
                    row.RelativeItem().AlignRight().Text($"{_model.CostoAireAcondicionado:C}");
                });

                col.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);

                col.Item().Row(row => {
                    row.RelativeItem().Text("Total Evento:").Bold();
                    row.RelativeItem().AlignRight().Text($"{_model.TotalGeneral:C}").Bold();
                });

                col.Item().Row(row => {
                    row.RelativeItem().Text("Total Pagado:").FontColor(Colors.Green.Medium);
                    row.RelativeItem().AlignRight().Text($"- {_model.TotalPagado:C}").FontColor(Colors.Green.Medium);
                });

                col.Item().PaddingTop(5).Background(Colors.Grey.Lighten3).Padding(5).Row(row => {
                    row.RelativeItem().Text("Saldo Pendiente:").Bold().FontSize(12);

                    var colorSaldo = _model.SaldoPendiente > 0 ? Colors.Red.Medium : Colors.Black;
                    row.RelativeItem().AlignRight().Text($"{_model.SaldoPendiente:C}").Bold().FontSize(12).FontColor(colorSaldo);
                });
            });
        }

        void ComposeFooter(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }
    }
}
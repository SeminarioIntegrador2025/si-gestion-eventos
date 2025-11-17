using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using si_td_gestion_eventos.Models.ViewModels;

namespace si_td_gestion_eventos.PDFTemplates
{

    public class ReciboPagoDocument : IDocument
    {
        
        private readonly PagoVM _pago;
        private readonly string _logoPath;

        
        public ReciboPagoDocument(PagoVM pago)
        {
            _pago = pago;
            _logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/logo.png");
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
        public DocumentSettings GetSettings() => DocumentSettings.Default;

        //Diseño pdf
        public void Compose(IDocumentContainer container)
        {
            container
                .Page(page =>
                {
                    page.Margin(50);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(style => style.FontSize(11));

                   
                    page.Header()
                        .Column(col => 
                        {
                           
                            if (File.Exists(_logoPath))
                            {
                                col.Item().AlignCenter().Width(400).Image(_logoPath).FitWidth();
                            }

                          
                            col.Item().AlignCenter().PaddingTop(10).Text("Recibo de Pago")
                                .SemiBold().FontSize(24).FontColor(Colors.Grey.Darken2);


                            col.Item().AlignCenter().PaddingTop(20).Column(colDatos => 
                            {
                                colDatos.Item().Text($"Recibo N°: {_pago.PagoId}");
                                colDatos.Item().Text($"Fecha de Emisión: {DateTime.Now:dd/MM/yyyy}"); 
                            });
                        });


                    page.Content()
    .PaddingVertical(20) 
    .AlignCenter()      
    .Column(col =>      
    {
 
        col.Item().PaddingBottom(10).Text("Detalles del Pago").Bold().FontSize(16);

 
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {

                columns.ConstantColumn(150);
                columns.RelativeColumn();
            });

            table.Cell().Text("Evento:");
            table.Cell().Text(_pago.EventoDescripcion);

            table.Cell().Text("Cliente:");
            table.Cell().Text(_pago.ClienteNombre);

            table.Cell().Text("Fecha de Pago:");
            table.Cell().Text(_pago.Fecha.ToString("dd/MM/yyyy"));

            table.Cell().Text("Método de Pago:");
            table.Cell().Text(_pago.Metodo.ToString());

            if (!string.IsNullOrEmpty(_pago.Observaciones))
            {
                table.Cell().Text("Observaciones:");
                table.Cell().Text(_pago.Observaciones);
            }

            table.Cell().PaddingTop(10).Text("Monto Pagado:").Bold();
            table.Cell().PaddingTop(10).Text($"{_pago.Monto:C}").Bold().FontSize(14);

            
        });
        //   col.Item().PaddingTop(80).Row(row =>  ESTO ES PARA PONER UN CAMPO PARA FIRMAS PERO TENDRIAMOS QUE PERSISTIR EL ARCHIVO Y NO HACERLO ON DEMAND
        //   {
        //       row.RelativeItem().Column(colEmpresa =>
        //       {
        //                colEmpresa.Item().AlignCenter().Text("_________________________"); 
        //        colEmpresa.Item().AlignCenter().Text("Firma Tata David");
        //       colEmpresa.Item().AlignCenter().Text("Fiestas y Eventos");
        //  });
        //       row.RelativeItem().Column(colCliente =>
        //  {
        //     colCliente.Item().AlignCenter().Text("_________________________"); 
        //     colCliente.Item().AlignCenter().Text("Firma Cliente");
        //      colCliente.Item().AlignCenter().Text($"{_pago.ClienteNombre}");
        //  });
        //  }); 
    });


                    page.Footer()
                        .Column(col =>
                        {
                        
                            col.Item().AlignCenter().Text("Gracias por su pago."); 
                            col.Item().AlignCenter().Text("Tata David - Fiestas y Eventos"); 
                        });
                });
        }
    }
}
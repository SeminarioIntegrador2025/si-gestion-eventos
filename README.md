# UTN - Seminario Integrador - Trabajo práctico – Gestión de Eventos (ASP.NET MVC + MySQL)
Sistema MVC para automatizar **control de pagos** y **gestión de eventos** de los eventos de un salón real, además de su constatación de servicios esenciales. 

## Funcionalidad
- Gestión de **Clientes** (ABM + búsqueda)
- Gestión de **Eventos** (ABM, disponibilidad, estado)
- Gestión de **Pagos** (ABM, adjuntar comprobantes, generar comprobante en efectivo, exportar)
- Gestión de **Fianzas** (registro, devolución total/parcial)
- Gestión de Servicios Esenciales y permisos adecuados. (adjuntar/validar; estado auto “Confirmado” al adjuntar)
- Reportes por evento (total alquiler, pagado, saldo, fianza, servicios), exportables a **PDF**.

## Arquitectura:
- **MVC ASP.NET** (Views Razor)
- **Domain–Application–Infrastructure–Web** (capas limpias)
- **Entity Framework Core**
- Repositorios + Unit of Work
- Storage de archivos local.
- Export
- Validaciones 



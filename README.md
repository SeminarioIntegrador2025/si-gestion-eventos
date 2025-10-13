# UTN - Seminario Integrador - Trabajo práctico – Gestión de Eventos (ASP.NET MVC + SQLServer)
Sistema MVC para automatizar **control de pagos** y **gestión de eventos y clientes** de los eventos de un salón real, además de su constatación de servicios esenciales. 

## Objetivos de aprendizaje (académico)
Este trabajo está enmarcado dentro de la cátedra Seminario Integrador, cuyo objetivo es desarrollar una aplicación real, que sea de valor para el cliente. En nuestro caso decidimos aplicar ASP.NET Core con la arquitectura MVC y Entity Framework Core.
### Dentro de los objetivos tenemos:
- Diseñar capas limpias (Domain–Application–Infrastructure–Web) y aplicar repositorios + Unit of Work.
- Modelar reglas de negocio (disponibilidad de fechas, 48 h de pago total, fianza).
- Implementar validaciones, archivos adjuntos, exportaciones y baja lógica.
- Practicar migraciones EF, seed data, configuración de SQL Server y buenas prácticas de git (Conventional Commits).

## Funcionalidades principales
- Gestión de **Clientes** (ABM + búsqueda)
- Gestión de **Eventos** (ABM, disponibilidad, estado)
- Gestión de **Pagos** (ABM, adjuntar comprobantes, generar comprobante en efectivo, exportar)
- Gestión de **Fianzas** (registro, devolución total/parcial)
- Gestión de Servicios Esenciales y permisos adecuados. (adjuntar/validar; estado auto “Confirmado” al adjuntar)
- Reportes por evento (total alquiler, pagado, saldo, fianza, servicios), exportables a **PDF**.

## Arquitectura:
- **MVC ASP.NET** (Views Razor)
- **Domain–Application–Infrastructure–Web** (clean architecture)
- **Entity Framework Core + SQL Server**
- Repositorios + Unit of Work
- Storage de archivos local (comprobantes/adjuntos).
- Export
- Validaciones y reglas de negocio (48 h previas pagado, no superposición, etc.) 

## Requisitos 
- .NET 9
- SQL Server local o en contenedor


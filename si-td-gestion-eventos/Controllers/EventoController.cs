using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Context;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Services;
using System.Threading.Tasks; 

namespace si_td_gestion_eventos.Controllers
{
    public class EventoController : Controller
    {
        private readonly AppDbContext _dbContext;
        private readonly IClienteService _clienteService;

        public EventoController(AppDbContext dbContext, IClienteService clienteService)
        {
            _dbContext = dbContext;
            _clienteService = clienteService;
        }

        public async Task<IActionResult> Index(string q, int page = 1, int pageSize = 10)
        {
           
            var query = _dbContext.Evento
                                .Include(e => e.Cliente)
                                .OrderByDescending(e => e.FechaContrato)
                                .AsQueryable();


            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(e =>
                    e.Cliente.Nombre.Contains(q) ||
                    e.Cliente.Apellido.Contains(q) ||
                    e.Tipo.ToString().Contains(q) ||
                    e.ResponsableNombre.Contains(q)
                );
            }

            
            ViewBag.Search = q;
            ViewBag.PageSize = pageSize;

           
            ViewBag.Ultimos = await _dbContext.Evento
                                        .Include(e => e.Cliente)
                                        .OrderByDescending(e => e.EventoId)
                                        .Take(5)
                                        .ToListAsync();

           
            var paginatedList = await Infrastructure.PaginatedList<Evento>.CreateAsync(query, page, pageSize);
            return View(paginatedList);
        }

        // GET: Evento/Create
        public IActionResult Create()
        {
            ViewBag.Clientes = _clienteService.GetClientesActivosParaDropdown();
            return View();
        }

        // POST: Evento/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FechaContrato,Inicio,Fin,HoraInicio,HoraFin,Tipo,CostoAlquiler,MontoReserva,CantidadPersonas,MontoAireAcondicionado,ResponsableNombre,ResponsableTelefono,ResponsableCedula,ClienteId")] Evento evento)
        {
            // El 'Estado' ya no viene del formulario, así que lo eliminamos del 'ModelState'
            // para que no falle la validación por estar ausente.
            ModelState.Remove("Estado");

            if (ModelState.IsValid)
            {
                // Asignamos el estado inicial por defecto ANTES de guardar.
                evento.Estado = Models.Enums.EventoEstado.PendienteAConfirmar;

                _dbContext.Add(evento);
                await _dbContext.SaveChangesAsync();
                TempData["Ok"] = "Evento creado exitosamente.";
                return RedirectToAction(nameof(Index));
            }

            // Si la validación falla por otra razón, recargamos el ViewBag.
            ViewBag.Clientes = _clienteService.GetClientesActivosParaDropdown();
            return View(evento);
        }

        // GET: Evento/Cancel/5
        public async Task<IActionResult> Cancel(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var evento = await _dbContext.Evento
                .Include(e => e.Cliente) 
                .FirstOrDefaultAsync(m => m.EventoId == id);

            if (evento == null)
            {
                return NotFound();
            }
            return View(evento);
        }

        // POST: Evento/Cancelar/{EventoId}
        [HttpPost, ActionName("Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var evento = await _dbContext.Evento.FindAsync(id);
            if (evento is not null)
            {
                if (evento.Estado != Models.Enums.EventoEstado.Cancelado)
                {
                    evento.Estado = Models.Enums.EventoEstado.Cancelado;
                    await _dbContext.SaveChangesAsync();
                    TempData["Ok"] = "El Evento fue eliminado correctamente (Baja Logica).";
                }
                else
                {
                    if (evento.Estado == Models.Enums.EventoEstado.Cancelado)
                    {
                        TempData["Error"] = "El evento ya esta cancelado (Bajado Logicamente).";
                    }
                }
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Evento/Details/{EventoId}

        public async Task<IActionResult> Details(int? id)
        {
            if (id is null)
            {
                return NotFound();
            }

            // 1. Agregamos .Include() para cargar el Cliente
            // 2. Usamos la versión Async para no bloquear el servidor
            var evento = await _dbContext.Evento
                .Include(e => e.Cliente)
                .FirstOrDefaultAsync(c => c.EventoId == id);

            if (evento is null)
            {
                return NotFound();
            }

            return View(evento);
        }


        // GET: Evento/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Buscamos el evento que coincida con el ID de la URL
            // E INCLUIMOS los datos del cliente asociado.
            var evento = await _dbContext.Evento
                .Include(e => e.Cliente)
                .FirstOrDefaultAsync(e => e.EventoId == id); // <-- LÍNEA CLAVE

            if (evento == null)
            {
                // Esto pasaría si se accede a una URL con un ID que no existe (ej: /Evento/Edit/999)
                return NotFound();
            }

            // Cargamos la lista de clientes para el dropdown.
            ViewBag.Clientes = _clienteService.GetClientesActivosParaDropdown();
            return View(evento);
        }


        // POST: Evento/Edit/5

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("EventoId,FechaContrato,Inicio,Fin,HoraInicio,HoraFin,Tipo,CostoAlquiler,MontoReserva,CantidadPersonas,MontoAireAcondicionado,ResponsableNombre,ResponsableTelefono,ResponsableCedula,Estado,ClienteId")] Evento evento)
        {         
            if (id != evento.EventoId)
            {
                return NotFound();
            }
            if (ModelState.IsValid)
            {
                try
                {                   
                    _dbContext.Update(evento);
                    await _dbContext.SaveChangesAsync();
                    TempData["Ok"] = "Evento actualizado correctamente.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    TempData["Error"] = "El registro fue modificado por otro usuario. Intente de nuevo.";
                    return View(evento);
                }
                return RedirectToAction(nameof(Index));
            }           
            ViewBag.Clientes = _clienteService.GetClientesActivosParaDropdown();
            return View(evento);
        }

    }
}
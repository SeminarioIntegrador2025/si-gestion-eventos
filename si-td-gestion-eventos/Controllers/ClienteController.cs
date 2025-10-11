using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Context;
using si_td_gestion_eventos.Entities;

namespace si_td_gestion_eventos.Controllers
{
    public class ClienteController(AppDbContext _dbContext) : Controller
    {
        // GET: Cliente
        // ordenados por apellido y nombre, y además la opción de ver por los últimos 5 creados
        public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 10)
        {
            // sidebar: últimos agregados
            ViewBag.Ultimos = await _dbContext.Cliente
                .OrderByDescending(c => c.ClienteId)
                .Take(5)
                .AsNoTracking()
                .ToListAsync();

            // consulta principal con búsqueda
            var query = _dbContext.Cliente.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(c =>
                    c.Nombre.Contains(q) ||
                    c.Apellido.Contains(q) ||
                    c.CedulaIdentidad.Contains(q) ||
                    c.Telefono.Contains(q) ||
                    c.Domicilio.Contains(q));
            }

            query = query.OrderBy(c => c.Apellido).ThenBy(c => c.Nombre);

            var model = await Infrastructure.PaginatedList<Cliente>.CreateAsync(query, page, pageSize);
            ViewBag.Search = q;
            ViewBag.PageSize = pageSize;

            return View(model);
        }



        // GET: Cliente/Details/{idCliente}
        public IActionResult Details(int? id)
        {
            if (id is null) return NotFound();
            var cliente = _dbContext.Cliente.FirstOrDefault(c => c.ClienteId == id);
            if (cliente is null) return NotFound();
            return View(cliente);
        }

        // GET: Cliente/Create
        public IActionResult Create() => View();

        // POST: Cliente/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create([Bind("Nombre,Apellido,CedulaIdentidad,Domicilio,Telefono,Activo")] Cliente cliente)
        {
            if (!ModelState.IsValid) return View(cliente);

            _dbContext.Cliente.Add(cliente);
            _dbContext.SaveChanges();
            TempData["Ok"] = "Cliente creado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Cliente/Edit/{idCliente}
        public IActionResult Edit(int? id)
        {
            if (id is null) return NotFound();
            var cliente = _dbContext.Cliente.Find(id);
            if (cliente is null) return NotFound();
            return View(cliente);
        }

        // POST: Cliente/Edit/{idCliente}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, [Bind("ClienteId,Nombre,Apellido,CedulaIdentidad,Domicilio,Telefono,Activo")] Cliente cliente)
        {
            if (id != cliente.ClienteId) return NotFound();
            if (!ModelState.IsValid) return View(cliente);

            try
            {
                _dbContext.Update(cliente);
                _dbContext.SaveChanges();
                TempData["Ok"] = "Cliente actualizado.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "No se pudo guardar. Verificá datos duplicados (Cédula) o vuelve a intentar.");
                return View(cliente);
            }
        }

        // GET: Cliente/Delete/{idCliente}
        public IActionResult Delete(int? id)
        {
            if (id is null) return NotFound();
            var cliente = _dbContext.Cliente.FirstOrDefault(c => c.ClienteId == id);
            if (cliente is null) return NotFound();
            return View(cliente);
        }

        // POST: Cliente/Delete/{idCliente}
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var cliente = await _dbContext.Cliente.FindAsync(id);
            if (cliente is not null)
            {
                if (cliente.Activo != false)
                {
                    cliente.Activo = false;
                    await _dbContext.SaveChangesAsync();
                    TempData["Ok"] = "El cliente fue eliminado correctamente (Baja Logica).";
                }
                else {
                    TempData["Error"] = "El cliente ya esta eliminado (Bajado Logicamente).";

                }
            }
            return RedirectToAction(nameof(Index));
        }
    }
}

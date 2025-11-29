using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuestPDF.Infrastructure;
using si_td_gestion_eventos.Context;
using si_td_gestion_eventos.Mapping;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Repositories;
using si_td_gestion_eventos.Services.Contracts;
using si_td_gestion_eventos.Services.Implementation;
using si_td_gestion_eventos.Validators;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlString"))
);

// AutoMapper
builder.Services.AddAutoMapper(cfg =>
{
    // Se registran los perfiles para el mapeo de objetos
    cfg.AddProfile<ClienteProfile>();
    cfg.AddProfile<EventoProfile>();
    cfg.AddProfile<PagoProfile>();
    cfg.AddProfile<FianzaProfile>();
});

// Repositorios
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

// Servicios de Negocio (Lógica de la aplicación)
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IEventoService, EventoService>();
builder.Services.AddScoped<IPagoService, PagoService>();
builder.Services.AddScoped<IReporteService, ReporteService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IFianzaService, FianzaService>();
builder.Services.AddScoped<IServicioEsencialService, ServicioEsencialService>();


// Reglas de Negocio (Validaciones complejas)
builder.Services.AddScoped<IClienteBusinessRules, ClienteBusinessRules>();
builder.Services.AddScoped<IEventoBusinessRules, EventoBusinessRules>();

// Validadores con FluentValidation
builder.Services.AddScoped<IValidator<ClienteVM>, ClienteValidator>();
builder.Services.AddScoped<IValidator<EventoVM>, EventoValidator>();
builder.Services.AddScoped<IValidator<PagoVM>, PagoValidator>();
builder.Services.AddHostedService<si_td_gestion_eventos.BackgroundServices.PaymentDeadlineService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
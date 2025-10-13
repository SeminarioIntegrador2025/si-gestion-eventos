using FluentValidation;
using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Context;
using si_td_gestion_eventos.Repositories;
using si_td_gestion_eventos.Services;
using si_td_gestion_eventos.Services.Contracts;
using si_td_gestion_eventos.Services.Implementation;
using si_td_gestion_eventos.Validators;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Mapping;
using Microsoft.Extensions.DependencyInjection;

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
    cfg.AddProfile<ClienteProfile>();
});

// Repositorios
builder.Services.AddScoped(typeof(GenericRepository<>));

// Servicios de Negocio
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IClienteBusinessRules, ClienteBusinessRules>();

// Validadores FluentValidation
builder.Services.AddScoped<IValidator<ClienteVM>, ClienteValidator>();

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

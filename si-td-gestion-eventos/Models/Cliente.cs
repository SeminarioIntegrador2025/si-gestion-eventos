namespace si_td_gestion_eventos.Models
{
    public class Cliente
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Cedula { get; set; } = null!;
        public string? Domicilio { get; set; }
        public string? Telefono { get; set; }
        public bool Activo { get; set; } = true;

        // un cliente puede tener varios(lista) eventos asociados. 
        public ICollection<Evento> Eventos { get; set; } = new List<Evento>();
    }
}

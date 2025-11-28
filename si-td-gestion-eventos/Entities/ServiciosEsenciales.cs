using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace si_td_gestion_eventos.Entities
{
    // Definida como Abstracta
    public abstract class ServicioEsencial
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Evento")]
        public int EventoId { get; set; }
        public virtual Evento Evento { get; set; }

       
    }
}
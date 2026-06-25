using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Momentum.Models;

public class HabitLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int HabitId { get; set; }

    [ForeignKey(nameof(HabitId))]
    public Habit? Habit { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    public bool IsCompleted { get; set; } = true;
}

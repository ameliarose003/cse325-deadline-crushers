using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Momentum.Models;

public class Habit
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public int Streak { get; set; }

    [Required]
    public string Category { get; set; } = string.Empty;

    [Required]
    public string CategoryLabel { get; set; } = string.Empty;

    [Required]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public ICollection<HabitLog> Logs { get; set; } = new List<HabitLog>();
    public DateTime? UpdatedAt { get; set; }
}

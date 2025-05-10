using CinemaTest.Models;
using Microsoft.EntityFrameworkCore;
using CinemaTest.Models.Admin;
using CinemaTest.Models.User;

namespace CinemaTest.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<CinemaTest.Models.Admin.Movie> Movies { get; set; }
        public DbSet<CinemaTest.Models.Admin.Room> Rooms { get; set; }
        public DbSet<CinemaTest.Models.Admin.Showtime> Showtimes { get; set; }
        public DbSet<CinemaTest.Models.User.User> Users { get; set; }
        public DbSet<CinemaTest.Models.User.Transaction> Transactions { get; set; }
        public DbSet<CinemaTest.Models.User.Ticket> Tickets { get; set; }

    }
}

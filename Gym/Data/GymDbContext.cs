using Gym.Models;
using Microsoft.EntityFrameworkCore;

namespace Gym.Data
{
    public class GymDbContext : DbContext
    {
        public GymDbContext(DbContextOptions<GymDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Client> Clients { get; set; } = null!;
        public DbSet<Trainer> Trainers { get; set; } = null!;
        public DbSet<Session> Sessions { get; set; } = null!;
        public DbSet<ClientProgram> ClientPrograms { get; set; } = null!;
        public DbSet<SessionAttendee> SessionAttendees { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<Equipment> Equipments { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // TPT / TPH Inheritance Mapping for User Hierarchy
            modelBuilder.Entity<User>()
                .HasDiscriminator<string>("UserType")
                .HasValue<User>("BaseUser")
                .HasValue<Client>("Client")
                .HasValue<Trainer>("Trainer");

            // Precision configurations for monetary and rating fields
            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Trainer>()
                .Property(t => t.AverageRating)
                .HasPrecision(3, 2);

            // Session <-> Trainer Relationship
            modelBuilder.Entity<Session>()
                .HasOne(s => s.Trainer)
                .WithMany(u => u.TrainerSessions)
                .HasForeignKey(s => s.TrainerId)
                .OnDelete(DeleteBehavior.Restrict);

            // ClientProgram Relationships
            modelBuilder.Entity<ClientProgram>()
                .HasOne(cp => cp.Client)
                .WithMany()
                .HasForeignKey(cp => cp.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ClientProgram>()
                .HasOne(cp => cp.Trainer)
                .WithMany(t => t.ClientPrograms)
                .HasForeignKey(cp => cp.TrainerId)
                .OnDelete(DeleteBehavior.Restrict);

            // SessionAttendee Junction Configuration
            modelBuilder.Entity<SessionAttendee>()
                .HasOne(sa => sa.Session)
                .WithMany(s => s.Attendees)
                .HasForeignKey(sa => sa.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SessionAttendee>()
                .HasOne(sa => sa.Client)
                .WithMany()
                .HasForeignKey(sa => sa.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
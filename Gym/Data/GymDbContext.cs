using Gym.Models;
using Microsoft.EntityFrameworkCore;

namespace Gym.Data
{
    public class GymDbContext : DbContext
    {
        public GymDbContext(
            DbContextOptions<GymDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Client> Clients => Set<Client>();
        public DbSet<Trainer> Trainers => Set<Trainer>();
        public DbSet<TrainerAvailability> TrainerAvailabilities => Set<TrainerAvailability>();
        public DbSet<Session> Sessions => Set<Session>();
        public DbSet<ClientProgram> ClientPrograms =>
            Set<ClientProgram>();
        public DbSet<SessionAttendee> SessionAttendees =>
            Set<SessionAttendee>();
        public DbSet<Notification> Notifications =>
            Set<Notification>();
        public DbSet<FrontDeskEmailNotification> FrontDeskEmailNotifications =>
            Set<FrontDeskEmailNotification>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<Equipment> Equipments => Set<Equipment>();
        public DbSet<EquipmentNameCatalog> EquipmentNameCatalog => Set<EquipmentNameCatalog>();
        public DbSet<WalkInClient> WalkInClients => Set<WalkInClient>();
        public DbSet<WalkInPaymentType> WalkInPaymentTypes => Set<WalkInPaymentType>();
        public DbSet<MemberAttendanceLog> MemberAttendanceLogs => Set<MemberAttendanceLog>();
        public DbSet<MembershipPlanSetting> MembershipPlanSettings => Set<MembershipPlanSetting>();

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigureUserHierarchy(modelBuilder);
            ConfigureClient(modelBuilder);
            ConfigureTrainer(modelBuilder);
            ConfigureTrainerAvailability(modelBuilder);
            ConfigureSession(modelBuilder);
            ConfigureClientProgram(modelBuilder);
            ConfigureSessionAttendee(modelBuilder);
            ConfigurePayment(modelBuilder);
            ConfigureNotification(modelBuilder);
            ConfigureFrontDeskEmailNotification(modelBuilder);
            ConfigureEquipment(modelBuilder);
            ConfigureEquipmentNameCatalog(modelBuilder);
            ConfigureWalkInClient(modelBuilder);
            ConfigureMemberAttendanceLog(modelBuilder);
            modelBuilder.Entity<MembershipPlanSetting>(entity =>
            {
                entity.ToTable("MembershipPlanSettings");
                entity.HasKey(setting => setting.Plan);
                entity.Property(setting => setting.Plan).HasConversion<int>().ValueGeneratedNever();
                entity.Property(setting => setting.Name).HasMaxLength(100).IsRequired();
                entity.Property(setting => setting.Price).HasPrecision(18, 2);
            });
        }

        // =====================================================
        // USER INHERITANCE — TABLE-PER-HIERARCHY
        // =====================================================

        private static void ConfigureUserHierarchy(
            ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .ToTable("Users");

            modelBuilder.Entity<User>()
                .HasDiscriminator<string>("UserType")
                .HasValue<User>("BaseUser")
                .HasValue<Client>("Client")
                .HasValue<Trainer>("Trainer");

            modelBuilder.Entity<User>()
                .Property("UserType")
                .HasMaxLength(50)
                .IsRequired();

            modelBuilder.Entity<User>()
                .Property(user => user.Role)
                .HasConversion<int>();

            modelBuilder.Entity<User>()
                .Property(user => user.FirstName)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<User>()
                .Property(user => user.LastName)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<User>()
                .Property(user => user.Email)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<User>()
                .Property(user => user.PhoneNumber)
                .HasMaxLength(20)
                .IsRequired();

            modelBuilder.Entity<User>()
                .Property(user => user.ProfileImageUrl)
                .IsRequired(false);

            modelBuilder.Entity<User>()
                .Property(user => user.Specialization)
                .HasMaxLength(500)
                .IsRequired(false);

            modelBuilder.Entity<User>()
                .Property(user => user.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<User>()
                .Property(user => user.CreatedDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<User>()
                .HasIndex(user => user.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(user => user.Role);
        }

        // =====================================================
        // CLIENT
        // =====================================================

        private static void ConfigureClient(
            ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Client>()
                .Property(client => client.MembershipNumber)
                .HasMaxLength(50)
                .IsRequired();

            modelBuilder.Entity<Client>()
                .Property(client => client.MembershipPlan)
                .HasConversion<int>();

            modelBuilder.Entity<Client>()
                .Property(client => client.Status)
                .HasConversion<int>();

            modelBuilder.Entity<Client>()
                .Property(client => client.IsActiveMember)
                // This column is nullable in the shared TPH table because
                // trainer rows do not use it. Do not mark it as generated by
                // SQL: member inserts must always send the CLR bool value.
                .ValueGeneratedNever();

            modelBuilder.Entity<Client>()
                .Property(client => client.PreferredDays)
                .HasMaxLength(500);

            modelBuilder.Entity<Client>()
                .Property(client => client.PreferredTimeSlot)
                .HasMaxLength(100);

            modelBuilder.Entity<Client>()
                .Property(client => client.Notes)
                .HasMaxLength(500);

            modelBuilder.Entity<Client>()
                .Property(client => client.GymGoal)
                .HasConversion<int>();

            modelBuilder.Entity<Client>()
                .Property(client => client.FollowUpPurpose)
                .HasMaxLength(100);

            modelBuilder.Entity<Client>()
                .HasIndex(client => client.MembershipNumber)
                .IsUnique();

            modelBuilder.Entity<Client>()
                .HasIndex(client => client.AssignedTrainerId);

            // Client's optional assigned trainer.
            modelBuilder.Entity<Client>()
                .HasOne<Trainer>()
                .WithMany()
                .HasForeignKey(client =>
                    client.AssignedTrainerId)
                // SQL Server rejects SET NULL here because Users is a TPH
                // table and the self-reference can introduce multiple cascade
                // paths with the other relationships that target Users.
                .OnDelete(DeleteBehavior.Restrict);
        }

        // =====================================================
        // TRAINER
        // =====================================================

        private static void ConfigureTrainer(
            ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Trainer>()
                .Property(trainer => trainer.Bio)
                .HasMaxLength(500)
                .IsRequired(false);

            modelBuilder.Entity<Trainer>()
                .Property(trainer => trainer.AverageRating)
                .HasPrecision(3, 2);
        }

        private static void ConfigureTrainerAvailability(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TrainerAvailability>()
                .ToTable("TrainerAvailabilities");
            modelBuilder.Entity<TrainerAvailability>()
                .HasOne<Trainer>()
                .WithOne()
                .HasForeignKey<TrainerAvailability>(item => item.TrainerId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        // =====================================================
        // SESSION
        // =====================================================

        private static void ConfigureSession(
            ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Session>()
                .ToTable("Sessions");

            modelBuilder.Entity<Session>()
                .Property(session => session.Status)
                .HasConversion<int>();

            modelBuilder.Entity<Session>()
                .Property(session => session.SessionType)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<Session>()
                .Property(session => session.Location)
                .HasMaxLength(100);

            modelBuilder.Entity<Session>()
                .Property(session => session.Room)
                .HasMaxLength(50);

            modelBuilder.Entity<Session>()
                .Property(session => session.Description)
                .HasMaxLength(500);

            modelBuilder.Entity<Session>()
                .Property(session => session.CreatedDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Session>()
                .HasOne(session => session.Trainer)
                .WithMany(trainer =>
                    trainer.TrainerSessions)
                .HasForeignKey(session =>
                    session.TrainerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Session>()
                .HasIndex(session => session.TrainerId);

            modelBuilder.Entity<Session>()
                .HasIndex(session => session.StartTime);
        }

        // =====================================================
        // CLIENT PROGRAM
        // =====================================================

        private static void ConfigureClientProgram(
            ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ClientProgram>()
                .ToTable("ClientPrograms");

            modelBuilder.Entity<ClientProgram>()
                .Property(program => program.ProgramName)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<ClientProgram>()
                .Property(program => program.Description)
                .HasMaxLength(500);

            modelBuilder.Entity<ClientProgram>()
                .HasOne(program => program.Client)
                .WithMany()
                .HasForeignKey(program =>
                    program.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ClientProgram>()
                .HasOne(program => program.Trainer)
                .WithMany(trainer =>
                    trainer.ClientPrograms)
                .HasForeignKey(program =>
                    program.TrainerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ClientProgram>()
                .HasIndex(program => program.ClientId);

            modelBuilder.Entity<ClientProgram>()
                .HasIndex(program => program.TrainerId);
        }

        // =====================================================
        // SESSION ATTENDEE
        // =====================================================

        private static void ConfigureSessionAttendee(
            ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SessionAttendee>()
                .ToTable("SessionAttendees");

            modelBuilder.Entity<SessionAttendee>()
                .Property(attendee => attendee.BookedDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<SessionAttendee>()
                .HasOne(attendee => attendee.Session)
                .WithMany(session => session.Attendees)
                .HasForeignKey(attendee =>
                    attendee.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SessionAttendee>()
                .HasOne(attendee => attendee.Client)
                .WithMany()
                .HasForeignKey(attendee =>
                    attendee.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SessionAttendee>()
                .HasIndex(attendee =>
                    attendee.SessionId);

            modelBuilder.Entity<SessionAttendee>()
                .HasIndex(attendee =>
                    attendee.ClientId);

            // Prevent the same client from being added to the
            // same session more than once.
            modelBuilder.Entity<SessionAttendee>()
                .HasIndex(attendee => new
                {
                    attendee.SessionId,
                    attendee.ClientId
                })
                .IsUnique();
        }

        // =====================================================
        // PAYMENT
        // =====================================================

        private static void ConfigurePayment(
            ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Payment>()
                .ToTable("Payments");

            modelBuilder.Entity<Payment>()
                .Property(payment => payment.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Payment>()
                .Property(payment => payment.Status)
                .HasConversion<int>();

            modelBuilder.Entity<Payment>()
                .Property(payment => payment.TransactionCode)
                .HasMaxLength(50);

            modelBuilder.Entity<Payment>()
                .Property(payment => payment.Notes)
                .HasMaxLength(500);

            modelBuilder.Entity<Payment>()
                .HasOne(payment => payment.Client)
                .WithMany(client => client.Payments)
                .HasForeignKey(payment =>
                    payment.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Payment>()
                .HasIndex(payment => payment.ClientId);

            modelBuilder.Entity<Payment>()
                .HasIndex(payment => payment.Status);
        }

        // =====================================================
        // NOTIFICATION
        // =====================================================

        private static void ConfigureNotification(
            ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Notification>()
                .ToTable("Notifications");

            modelBuilder.Entity<Notification>()
                .Property(notification => notification.Title)
                .HasMaxLength(200)
                .IsRequired();

            modelBuilder.Entity<Notification>()
                .Property(notification => notification.Message)
                .HasMaxLength(1000)
                .IsRequired();

            modelBuilder.Entity<Notification>()
                .Property(notification =>
                    notification.NotificationType)
                .HasMaxLength(100);

            modelBuilder.Entity<Notification>()
                .Property(notification =>
                    notification.CreatedDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Notification>()
                .HasOne(notification => notification.User)
                .WithMany()
                .HasForeignKey(notification =>
                    notification.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Notification>()
                .HasIndex(notification =>
                    notification.UserId);

            modelBuilder.Entity<Notification>()
                .HasIndex(notification =>
                    notification.IsRead);
        }

        // =====================================================
        // EQUIPMENT
        // =====================================================

        private static void ConfigureEquipment(
            ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Equipment>()
                .ToTable("Equipments");

            modelBuilder.Entity<Equipment>()
                .Property(equipment =>
                    equipment.EquipmentName)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<Equipment>()
                .Property(equipment =>
                    equipment.Description)
                .HasMaxLength(500);

            modelBuilder.Entity<Equipment>()
                .Property(equipment => equipment.Category)
                .HasMaxLength(50)
                .HasDefaultValue("General");

            modelBuilder.Entity<Equipment>()
                .Property(equipment => equipment.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Available");

            modelBuilder.Entity<Equipment>()
                .Property(equipment => equipment.EvidencePhotoPath)
                .HasMaxLength(300);

            modelBuilder.Entity<Equipment>()
                .HasIndex(equipment =>
                    equipment.EquipmentName);
        }

        private static void ConfigureEquipmentNameCatalog(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EquipmentNameCatalog>()
                .ToTable("EquipmentNameCatalog");
            modelBuilder.Entity<EquipmentNameCatalog>()
                .Property(item => item.Name)
                .HasMaxLength(100)
                .IsRequired();
            modelBuilder.Entity<EquipmentNameCatalog>()
                .HasIndex(item => item.Name)
                .IsUnique();
        }

        private static void ConfigureFrontDeskEmailNotification(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FrontDeskEmailNotification>()
                .ToTable("FrontDeskEmailNotifications");
            modelBuilder.Entity<FrontDeskEmailNotification>()
                .HasIndex(item => new { item.StaffId, item.CreatedDate });
            modelBuilder.Entity<FrontDeskEmailNotification>()
                .HasIndex(item => item.DeliveryKey);
        }

        private static void ConfigureWalkInClient(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<WalkInClient>().ToTable("WalkInClients");
            modelBuilder.Entity<WalkInClient>()
                .Property(item => item.AmountPaid).HasPrecision(18, 2);
            modelBuilder.Entity<WalkInClient>()
                .HasIndex(item => item.RegisteredAt);
        }

        private static void ConfigureMemberAttendanceLog(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MemberAttendanceLog>().ToTable("MemberAttendanceLogs");
            modelBuilder.Entity<MemberAttendanceLog>()
                .HasIndex(item => new { item.ClientId, item.CheckInAt });
        }
    }
}

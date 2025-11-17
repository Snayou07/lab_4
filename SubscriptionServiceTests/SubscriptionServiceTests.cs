using Moq;
using Subscription_Service.Models;
using Subscription_Service.Services;
using Subscription_Service.Services.Interfaces;


namespace SubscriptionServiceTests
{
    public class SubscriptionServiceTests
    {
        private readonly Mock<IMemberRepository> _repo = new();
        private readonly Mock<IPaymentService> _payment = new();
        private readonly Mock<INotificationService> _notify = new();
        private readonly SubscriptionService _service;

        public SubscriptionServiceTests()
        {
            _service = new SubscriptionService(_repo.Object, _payment.Object, _notify.Object);
        }

        /// <summary>
        /// Перевіряє успішне поновлення підписки: платіж пройшов,
        /// користувач оновлений, сповіщення надіслано.
        /// </summary>
        [Fact]
        public void RenewSubscription_ValidPayment_ReturnsTrue()
        {
            // Arrange
            var member = new Member 
            { 
                Id = 1, 
                Name = "Тестовий користувач", 
                IsActive = false,
                SubscriptionEnd = DateTime.Now.AddDays(-5)
            };
            _repo.Setup(r => r.GetById(1)).Returns(member);
            _payment.Setup(p => p.VerifyPayment(1, 100m)).Returns(true);

            // Act
            var result = _service.RenewSubscription(1, 100m, 30);

            // Assert
            Assert.True(result);
            Assert.True(member.IsActive);
            Assert.NotNull(member.SubscriptionEnd);
            Assert.True(member.SubscriptionEnd > DateTime.Now);
            _repo.Verify(r => r.Update(member), Times.Once);
            _notify.Verify(n => n.SendNotification("Subscription renewed!", 1), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що підписка не поновлюється, якщо платіж не пройшов.
        /// Оновлення та сповіщення не повинні викликатися.
        /// </summary>
        [Fact]
        public void RenewSubscription_InvalidPayment_ReturnsFalse()
        {
            // Arrange
            var member = new Member 
            { 
                Id = 2, 
                Name = "Користувач з невдалим платежем", 
                IsActive = false 
            };
            _repo.Setup(r => r.GetById(2)).Returns(member);
            _payment.Setup(p => p.VerifyPayment(2, 50m)).Returns(false);

            // Act
            var result = _service.RenewSubscription(2, 50m, 30);

            // Assert
            Assert.False(result);
            _repo.Verify(r => r.Update(It.IsAny<Member>()), Times.Never);
            _notify.Verify(n => n.SendNotification(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        /// <summary>
        /// Перевіряє, що сервіс кидає виняток ArgumentException,
        /// якщо спробувати поновити підписку для неіснуючого користувача.
        /// </summary>
        [Fact]
        public void RenewSubscription_NonExistentMember_ThrowsException()
        {
            // Arrange
            _repo.Setup(r => r.GetById(999)).Returns((Member?)null);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => 
                _service.RenewSubscription(999, 100m, 30));
            
            Assert.Equal("Member not found", exception.Message);
            _payment.Verify(p => p.VerifyPayment(It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
            _repo.Verify(r => r.Update(It.IsAny<Member>()), Times.Never);
        }

        /// <summary>
        /// Перевіряє, що дата закінчення підписки встановлюється коректно
        /// (з похибкою на час виконання тесту).
        /// </summary>
        [Fact]
        public void RenewSubscription_SetsCorrectSubscriptionEndDate()
        {
            // Arrange
            var member = new Member 
            { 
                Id = 3, 
                Name = "Тест дати", 
                IsActive = false 
            };
            var days = 60;
            var beforeCall = DateTime.Now;
            
            _repo.Setup(r => r.GetById(3)).Returns(member);
            _payment.Setup(p => p.VerifyPayment(3, 200m)).Returns(true);

            // Act
            _service.RenewSubscription(3, 200m, days);

            // Assert
            Assert.NotNull(member.SubscriptionEnd);
            var expectedEnd = beforeCall.AddDays(days);
            // Перевірка з похибкою в 1 секунду
            Assert.True(member.SubscriptionEnd.Value >= expectedEnd.AddSeconds(-1));
            Assert.True(member.SubscriptionEnd.Value <= expectedEnd.AddSeconds(1));
        }

        /// <summary>
        /// Перевіряє, що метод деактивації коректно знаходить і
        /// деактивує *лише* тих користувачів, чия підписка закінчилась.
        /// </summary>
        [Fact]
        public void DeactivateExpiredMembers_DeactivatesExpiredOnly()
        {
            // Arrange
            var expiredMember1 = new Member 
            { 
                Id = 1, 
                Name = "Прострочений 1", 
                IsActive = true, 
                SubscriptionEnd = DateTime.Now.AddDays(-10) 
            };
            var expiredMember2 = new Member 
            { 
                Id = 2, 
                Name = "Прострочений 2", 
                IsActive = true, 
                SubscriptionEnd = DateTime.Now.AddDays(-1) 
            };
            var activeMember = new Member 
            { 
                Id = 3, 
                Name = "Активний", 
                IsActive = true, 
                SubscriptionEnd = DateTime.Now.AddDays(30) 
            };
            var members = new List<Member> { expiredMember1, expiredMember2, activeMember };
            _repo.Setup(r => r.GetAll()).Returns(members);

            // Act
            _service.DeactivateExpiredMembers();

            // Assert
            Assert.False(expiredMember1.IsActive);
            Assert.False(expiredMember2.IsActive);
            Assert.True(activeMember.IsActive);
            _repo.Verify(r => r.Update(expiredMember1), Times.Once);
            _repo.Verify(r => r.Update(expiredMember2), Times.Once);
            _repo.Verify(r => r.Update(activeMember), Times.Never);
            _notify.Verify(n => n.SendNotification("Membership expired", 1), Times.Once);
            _notify.Verify(n => n.SendNotification("Membership expired", 2), Times.Once);
            _notify.Verify(n => n.SendNotification(It.IsAny<string>(), 3), Times.Never);
        }

        /// <summary>
        /// Перевіряє, що метод деактивації не викликає оновлень чи сповіщень,
        /// якщо в системі немає прострочених підписок.
        /// </summary>
        [Fact]
        public void DeactivateExpiredMembers_NoExpiredMembers_DoesNothing()
        {
            // Arrange
            var activeMember1 = new Member 
            { 
                Id = 1, 
                IsActive = true, 
                SubscriptionEnd = DateTime.Now.AddDays(15) 
            };
            var activeMember2 = new Member 
            { 
                Id = 2, 
                IsActive = true, 
                SubscriptionEnd = DateTime.Now.AddDays(60) 
            };
            var members = new List<Member> { activeMember1, activeMember2 };
            _repo.Setup(r => r.GetAll()).Returns(members);

            // Act
            _service.DeactivateExpiredMembers();

            // Assert
            _repo.Verify(r => r.Update(It.IsAny<Member>()), Times.Never);
            _notify.Verify(n => n.SendNotification(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        /// <summary>
        /// Перевіряє, що метод деактивації ігнорує користувачів,
        /// у яких дата закінчення підписки не встановлена (null).
        /// </summary>
        [Fact]
        public void DeactivateExpiredMembers_MemberWithoutSubscriptionEnd_IgnoresMember()
        {
            // Arrange
            var memberWithoutEnd = new Member 
            { 
                Id = 1, 
                Name = "Без дати закінчення", 
                IsActive = true, 
                SubscriptionEnd = null 
            };
            var members = new List<Member> { memberWithoutEnd };
            _repo.Setup(r => r.GetAll()).Returns(members);

            // Act
            _service.DeactivateExpiredMembers();

            // Assert
            Assert.True(memberWithoutEnd.IsActive);
            _repo.Verify(r => r.Update(It.IsAny<Member>()), Times.Never);
            _notify.Verify(n => n.SendNotification(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        /// <summary>
        /// Перевіряє, що при поновленні на 0 днів, дата закінчення 
        /// підписки коректно встановлюється на поточний час.
        /// </summary>
        [Fact]
        public void RenewSubscription_ZeroDays_SetsSubscriptionEndToToday()
        {
            // Arrange
            var member = new Member 
            { 
                Id = 5, 
                Name = "Тест нульової тривалості", 
                IsActive = false 
            };
            var beforeCall = DateTime.Now;
            
            _repo.Setup(r => r.GetById(5)).Returns(member);
            _payment.Setup(p => p.VerifyPayment(5, 50m)).Returns(true);

            // Act
            _service.RenewSubscription(5, 50m, 0);

            // Assert
            Assert.NotNull(member.SubscriptionEnd);
            Assert.True(member.SubscriptionEnd.Value >= beforeCall.AddSeconds(-1));
            Assert.True(member.SubscriptionEnd.Value <= beforeCall.AddSeconds(1));
        }

        /// <summary>
        /// Перевіряє, що вже активний користувач може успішно
        /// поновити свою підписку (наприклад, продовжити її).
        /// </summary>
        [Fact]
        public void RenewSubscription_AlreadyActiveMember_RenewsSuccessfully()
        {
            // Arrange
            var member = new Member 
            { 
                Id = 6, 
                Name = "Вже активний користувач", 
                IsActive = true,
                SubscriptionEnd = DateTime.Now.AddDays(10)
            };
            
            _repo.Setup(r => r.GetById(6)).Returns(member);
            _payment.Setup(p => p.VerifyPayment(6, 150m)).Returns(true);

            // Act
            var result = _service.RenewSubscription(6, 150m, 30);

            // Assert
            Assert.True(result);
            Assert.True(member.IsActive);
            _repo.Verify(r => r.Update(member), Times.Once);
            _notify.Verify(n => n.SendNotification("Subscription renewed!", 6), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що якщо користувач прострочений і вже неактивний,
        /// метод деактивації все одно відпрацює (оновить дані та надішле сповіщення).
        /// </summary>
        [Fact]
        public void DeactivateExpiredMembers_ExpiredButAlreadyInactive_StillUpdatesAndNotifies()
        {
            // Arrange
            var alreadyInactiveMember = new Member 
            { 
                Id = 7, 
                Name = "Вже неактивний але прострочений", 
                IsActive = false, 
                SubscriptionEnd = DateTime.Now.AddDays(-5) 
            };
            var members = new List<Member> { alreadyInactiveMember };
            _repo.Setup(r => r.GetAll()).Returns(members);

            // Act
            _service.DeactivateExpiredMembers();

            // Assert
            Assert.False(alreadyInactiveMember.IsActive);
            _repo.Verify(r => r.Update(alreadyInactiveMember), Times.Once);
            _notify.Verify(n => n.SendNotification("Membership expired", 7), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що сервіс коректно обробляє поновлення,
        /// навіть якщо сума платежу від'ємна (за умови, що платіжна система це підтвердить).
        /// </summary>
        [Fact]
        public void RenewSubscription_NegativeAmount_ProcessesIfPaymentVerified()
        {
            // Arrange
            var member = new Member 
            { 
                Id = 8, 
                Name = "Тест від'ємної суми", 
                IsActive = false 
            };
            
            _repo.Setup(r => r.GetById(8)).Returns(member);
            _payment.Setup(p => p.VerifyPayment(8, -10m)).Returns(true);

            // Act
            var result = _service.RenewSubscription(8, -10m, 30);

            // Assert
            Assert.True(result);
            _payment.Verify(p => p.VerifyPayment(8, -10m), Times.Once);
        }

        /// <summary>
        /// Перевіряє, що метод деактивації не кидає помилок
        /// і коректно відпрацьовує, якщо репозиторій повертає порожній список.
        /// </summary>
        [Fact]
        public void DeactivateExpiredMembers_EmptyMemberList_NoErrors()
        {
            // Arrange
            var emptyList = new List<Member>();
            _repo.Setup(r => r.GetAll()).Returns(emptyList);

            // Act
            _service.DeactivateExpiredMembers();

            // Assert
            _repo.Verify(r => r.Update(It.IsAny<Member>()), Times.Never);
            _notify.Verify(n => n.SendNotification(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }
    }
}
